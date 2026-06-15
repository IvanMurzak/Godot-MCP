import * as fs from 'fs';
import * as path from 'path';
import { spawn } from 'child_process';
import { homedir, platform } from 'os';

/**
 * Environment variables that, when set, point directly at a Godot editor
 * binary. Checked before PATH and common install dirs. `GODOT_BIN` is the
 * conventional name; `GODOT4_BIN` is honored for setups that pin a Godot 4
 * binary alongside a legacy Godot 3 one.
 */
export const GODOT_BIN_ENV_VARS = ['GODOT_BIN', 'GODOT4_BIN'] as const;

/**
 * Candidate executable base-names searched on `PATH`. The Windows mono build
 * uses a `_console.exe` companion that surfaces stdout; the mono editor binary
 * itself ships under several historical names, so we probe a small set.
 */
function pathCandidateNames(os: NodeJS.Platform): string[] {
  if (os === 'win32') {
    // Prefer the mono build; the `_console` variant surfaces GD.Print/stdout.
    return [
      'godot_mono.exe',
      'godot.exe',
      'Godot_mono.exe',
      'Godot.exe',
    ];
  }
  if (os === 'darwin') {
    return ['godot', 'godot_mono', 'Godot'];
  }
  return ['godot', 'godot_mono', 'Godot'];
}

/**
 * Maximum directory depth (inclusive) to descend when shallow-recursively
 * scanning a common install root for a Godot editor binary. Depth 0 is the root
 * itself; depth 1 its immediate children, etc. Official Windows zips extract to
 * `<root>/Godot_v<ver>-stable_mono_win64/Godot_v<ver>-stable_mono_win64/<exe>`
 * (binary two levels below `Downloads`), so we need depth >= 2; we use 3 to also
 * cover an extra wrapper folder without scanning unbounded trees.
 */
const SCAN_MAX_DEPTH = 3;

/**
 * Whether a file name looks like a Godot editor binary. Matches both the fixed
 * historical names (`Godot.exe`, `godot`, `godot_mono`) AND the official
 * version-stamped release names, e.g.
 *   - `Godot_v4.5.1-stable_mono_win64.exe`
 *   - `Godot_v4.5.1-stable_mono_win64_console.exe`
 *   - `Godot_v4.5.1-stable_win64.exe`
 *   - `Godot_v4.3-stable_mono_linux.x86_64`
 *   - `Godot_v4.5.1-stable_macos.universal`
 * The match is case-insensitive (Windows) and intentionally loose on the
 * platform/arch suffix so future arch names still resolve.
 */
function isGodotBinaryName(name: string, os: NodeJS.Platform): boolean {
  const lower = name.toLowerCase();

  if (os === 'win32') {
    // Fixed names.
    if (lower === 'godot.exe' || lower === 'godot_mono.exe') return true;
    // Version-stamped: Godot_v<...>...win64[...]( _console)?.exe
    return /^godot_v.*win.*\.exe$/.test(lower);
  }

  if (os === 'darwin') {
    if (lower === 'godot' || lower === 'godot_mono') return true;
    return /^godot_v.*macos.*$/.test(lower) || /^godot_v.*osx.*$/.test(lower);
  }

  // linux + others
  if (lower === 'godot' || lower === 'godot_mono') return true;
  return /^godot_v.*(linux|x11).*$/.test(lower);
}

/**
 * Rank a Godot binary name so we can prefer the build that surfaces the most
 * useful output. Higher score = more preferred:
 *   - mono `_console` build (mono + stdout)  → 3
 *   - mono build                              → 2
 *   - non-mono `_console` build               → 1
 *   - everything else                         → 0
 * Used to pick the best match within a single directory of candidates.
 */
function godotBinaryRank(name: string): number {
  const lower = name.toLowerCase();
  const mono = lower.includes('mono');
  const console = lower.includes('_console');
  if (mono && console) return 3;
  if (mono) return 2;
  if (console) return 1;
  return 0;
}

/**
 * Recursively scan `root` for files whose name looks like a Godot editor binary,
 * descending at most `maxDepth` levels. Returns absolute paths, best-ranked
 * first (so a caller can take the first hit). Symlinks are not followed and any
 * unreadable directory is skipped silently — this is a best-effort discovery
 * scan, never a hard failure.
 */
function scanForGodotBinaries(root: string, os: NodeJS.Platform, maxDepth: number): string[] {
  const found: string[] = [];

  const walk = (dir: string, depth: number): void => {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return; // unreadable / nonexistent — skip
    }
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isFile()) {
        if (isGodotBinaryName(entry.name, os)) {
          found.push(full);
        }
      } else if (entry.isDirectory() && depth < maxDepth) {
        walk(full, depth + 1);
      }
    }
  };

  walk(root, 0);

  // Prefer the best build (mono _console > mono > _console > plain); stable
  // secondary sort by path so the result is deterministic.
  found.sort((a, b) => {
    const rank = godotBinaryRank(path.basename(b)) - godotBinaryRank(path.basename(a));
    return rank !== 0 ? rank : a.localeCompare(b);
  });
  return found;
}

/**
 * Per-OS common install roots to shallow-recursively scan for a Godot editor
 * binary when neither an env override nor a PATH hit resolves one. The official
 * release zips extract to a version-stamped nested folder, so we scan roots
 * recursively (bounded by `SCAN_MAX_DEPTH`) rather than probing fixed leaf
 * paths.
 *   - Windows: Downloads, Program Files (+ x86), LocalAppData Programs, home,
 *     plus package-manager locations (Scoop / Chocolatey / winget / Steam).
 *   - macOS: `/Applications`, `~/Applications`, `~/Downloads` (the `.app`
 *     bundle's inner `Contents/MacOS/Godot` binary is matched by the scan).
 *   - Linux: common extracted-binary dirs + Downloads + Steam.
 */
function commonInstallRoots(os: NodeJS.Platform): string[] {
  const home = homedir();
  const roots: string[] = [];

  if (os === 'win32') {
    const programFiles = process.env['PROGRAMFILES'] ?? 'C:\\Program Files';
    const programFilesX86 = process.env['PROGRAMFILES(X86)'] ?? 'C:\\Program Files (x86)';
    const localAppData = process.env['LOCALAPPDATA'] ?? path.join(home, 'AppData', 'Local');
    const userProfile = process.env['USERPROFILE'] ?? home;
    roots.push(
      path.join(userProfile, 'Downloads'),
      path.join(home, 'Downloads'),
      path.join(programFiles, 'Godot'),
      path.join(programFilesX86, 'Godot'),
      path.join(localAppData, 'Programs', 'Godot'),
      // Scoop installs shims + apps under ~/scoop.
      path.join(userProfile, 'scoop', 'apps', 'godot'),
      path.join(userProfile, 'scoop', 'apps', 'godot-mono'),
      // Chocolatey lib.
      path.join(process.env['CHOCOLATEYINSTALL'] ?? 'C:\\ProgramData\\chocolatey', 'lib', 'godot'),
      // winget links / Steam common.
      path.join(localAppData, 'Microsoft', 'WinGet', 'Packages'),
      path.join(programFiles, 'Steam', 'steamapps', 'common', 'Godot Engine'),
      path.join(programFilesX86, 'Steam', 'steamapps', 'common', 'Godot Engine'),
    );
    return roots;
  }

  if (os === 'darwin') {
    roots.push(
      '/Applications',
      path.join(home, 'Applications'),
      path.join(home, 'Downloads'),
      // Steam.
      path.join(home, 'Library', 'Application Support', 'Steam', 'steamapps', 'common', 'Godot Engine'),
    );
    return roots;
  }

  // linux + others
  roots.push(
    '/usr/local/bin',
    '/usr/bin',
    '/opt',
    path.join(home, '.local', 'bin'),
    path.join(home, '.local', 'share', 'godot'),
    path.join(home, 'Applications'),
    path.join(home, 'bin'),
    path.join(home, 'Downloads'),
    // Steam.
    path.join(home, '.local', 'share', 'Steam', 'steamapps', 'common', 'Godot Engine'),
    path.join(home, '.steam', 'steam', 'steamapps', 'common', 'Godot Engine'),
  );
  return roots;
}

/**
 * Search the directories on `PATH` for a matching Godot binary. Pure filesystem
 * lookup — never spawns anything. Each PATH dir is first probed for the fixed
 * candidate names (cheap), then, if none hit, its immediate entries are scanned
 * for a version-stamped name (`Godot_v...`). Within a single PATH dir the
 * best-ranked build (mono `_console` > mono > …) wins.
 */
function findOnPath(os: NodeJS.Platform): string | null {
  const pathEnv = process.env['PATH'] ?? '';
  if (!pathEnv) return null;
  const sep = os === 'win32' ? ';' : ':';
  const dirs = pathEnv.split(sep).filter((d) => d.length > 0);
  const names = pathCandidateNames(os);
  for (const dir of dirs) {
    // Fast path: fixed candidate names.
    for (const name of names) {
      const candidate = path.join(dir, name);
      if (existsAsFile(candidate)) {
        return candidate;
      }
    }
    // Slow path: version-stamped binary sitting directly on a PATH dir.
    const stamped = scanForGodotBinaries(dir, os, 0);
    if (stamped.length > 0) {
      return stamped[0];
    }
  }
  return null;
}

function existsAsFile(p: string): boolean {
  try {
    return fs.statSync(p).isFile();
  } catch {
    return false;
  }
}

/**
 * Resolve the Godot editor binary path.
 *
 * Resolution order (first hit wins):
 *   1. An explicit `editorPath` argument (validated to be an existing file).
 *   2. `GODOT_BIN` / `GODOT4_BIN` environment variables.
 *   3. The first matching Godot binary on `PATH` (fixed name or version-stamped).
 *   4. A bounded shallow-recursive scan of per-OS common install roots
 *      (Downloads, Program Files, LocalAppData Programs, home, plus Scoop /
 *      Chocolatey / winget / Steam locations). Matches both fixed and
 *      version-stamped names (`Godot_v<ver>-stable_mono_win64[_console].exe`),
 *      preferring the mono `_console` build. This is what resolves the common
 *      case of an official zip extracted into a nested version-stamped folder.
 *
 * Returns the absolute path, or `null` when no Godot binary can be located.
 * Pure / no spawn — exported for unit tests (the `os`/env are injectable via
 * the running process; tests stub `fs` + env).
 */
export function findGodotBinary(
  editorPath?: string,
  os: NodeJS.Platform = platform(),
): string | null {
  // 1. Explicit path
  if (editorPath !== undefined) {
    const trimmed = editorPath.trim();
    if (trimmed.length === 0) return null;
    const resolved = path.resolve(trimmed);
    return existsAsFile(resolved) ? resolved : null;
  }

  // 2. Env overrides
  for (const envVar of GODOT_BIN_ENV_VARS) {
    const raw = process.env[envVar];
    if (raw && raw.trim().length > 0) {
      const resolved = path.resolve(raw.trim());
      if (existsAsFile(resolved)) {
        return resolved;
      }
    }
  }

  // 3. PATH
  const onPath = findOnPath(os);
  if (onPath) return onPath;

  // 4. Bounded shallow-recursive scan of common install roots. Roots are tried
  //    in priority order; within a root the best-ranked build wins.
  for (const root of commonInstallRoots(os)) {
    const hits = scanForGodotBinaries(root, os, SCAN_MAX_DEPTH);
    if (hits.length > 0) {
      return hits[0];
    }
  }

  return null;
}

export interface LaunchEditorCallbacks {
  /** Fired once the OS reports the child process has spawned. */
  onSpawn?: (pid: number | undefined) => void;
  /** Fired if the spawn itself fails (binary missing, permission denied, …). */
  onError?: (err: Error) => void;
}

/**
 * Spawn the Godot editor binary opening the given project, optionally with
 * `GODOT_MCP_*` connection environment variables. Mirrors the Unity CLI's
 * `launchEditor`: spawns detached, with `stdio: 'ignore'`, and `unref()`s so
 * the parent process can exit without waiting on the editor.
 *
 * Godot's editor-open invocation is `--editor --path <project>` (the
 * `--editor` flag forces the project to open in the editor rather than run).
 *
 * Library-safe (no process.exit, no stdout/stderr writes — observability is
 * the caller's responsibility via the optional callbacks).
 */
export function launchEditor(
  editorPath: string,
  projectPath: string,
  env?: Record<string, string>,
  callbacks?: LaunchEditorCallbacks,
): import('child_process').ChildProcess {
  const args = ['--editor', '--path', path.resolve(projectPath)];

  const child = spawn(editorPath, args, {
    detached: true,
    stdio: 'ignore',
    env: { ...process.env, ...env },
  });

  child.on('spawn', () => {
    callbacks?.onSpawn?.(child.pid);
  });

  child.on('error', (err) => {
    callbacks?.onError?.(err);
  });

  child.unref();
  return child;
}
