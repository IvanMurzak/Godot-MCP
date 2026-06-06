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
 * Per-OS common install directories to scan for a Godot editor binary when
 * neither an env override nor a PATH hit resolves one.
 *   - Windows: prefer the mono build under Program Files / common download dirs.
 *   - macOS: the `.app` bundle's inner `Contents/MacOS/Godot` binary.
 *   - Linux: extracted binaries under common app dirs.
 */
function commonInstallCandidates(os: NodeJS.Platform): string[] {
  const home = homedir();
  const candidates: string[] = [];

  if (os === 'win32') {
    const programFiles = process.env['PROGRAMFILES'] ?? 'C:\\Program Files';
    const localAppData = process.env['LOCALAPPDATA'] ?? path.join(home, 'AppData', 'Local');
    for (const root of [
      path.join(programFiles, 'Godot'),
      path.join(localAppData, 'Programs', 'Godot'),
      path.join(home, 'Downloads'),
    ]) {
      // Prefer the mono console build (surfaces stdout), then the plain mono build.
      candidates.push(path.join(root, 'Godot_mono.exe'));
      candidates.push(path.join(root, 'Godot.exe'));
    }
    return candidates;
  }

  if (os === 'darwin') {
    for (const app of [
      '/Applications/Godot_mono.app',
      '/Applications/Godot.app',
      path.join(home, 'Applications', 'Godot_mono.app'),
      path.join(home, 'Applications', 'Godot.app'),
    ]) {
      candidates.push(path.join(app, 'Contents', 'MacOS', 'Godot'));
    }
    return candidates;
  }

  // linux + others
  for (const dir of [
    '/usr/local/bin',
    '/usr/bin',
    path.join(home, '.local', 'bin'),
    path.join(home, 'Applications'),
    path.join(home, 'bin'),
  ]) {
    candidates.push(path.join(dir, 'godot'));
    candidates.push(path.join(dir, 'godot_mono'));
    candidates.push(path.join(dir, 'Godot'));
  }
  return candidates;
}

/**
 * Search the directories on `PATH` for the first matching Godot binary name.
 * Pure filesystem lookup — never spawns anything.
 */
function findOnPath(os: NodeJS.Platform): string | null {
  const pathEnv = process.env['PATH'] ?? '';
  if (!pathEnv) return null;
  const sep = os === 'win32' ? ';' : ':';
  const dirs = pathEnv.split(sep).filter((d) => d.length > 0);
  const names = pathCandidateNames(os);
  for (const dir of dirs) {
    for (const name of names) {
      const candidate = path.join(dir, name);
      if (existsAsFile(candidate)) {
        return candidate;
      }
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
 *   3. The first matching Godot binary on `PATH`.
 *   4. Per-OS common install directories (Windows prefers the mono build;
 *      macOS resolves the `.app/Contents/MacOS` binary; Linux scans common
 *      extracted-binary locations).
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

  // 4. Common install dirs
  for (const candidate of commonInstallCandidates(os)) {
    if (existsAsFile(candidate)) {
      return candidate;
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
