import * as fs from 'fs';
import * as path from 'path';
import { resolveProjectPath } from './open.js';
import { emitProgress } from './progress.js';
import type { CreateProjectOptions, CreateProjectResult } from './types.js';

/**
 * `Godot.NET.Sdk` version pinned into a scaffolded `--dotnet` csproj. Matches
 * the addon's own min-version floor (`Godot-MCP.csproj` pins `4.3.0`) and the
 * `config/features` "4.3" floor written into `project.godot`. A consumer can
 * bump this to their editor version freely; the scaffold targets the floor so
 * the generated project opens in any Godot 4.3+ editor.
 */
export const GODOT_SDK_VERSION = '4.3.0';

/**
 * Default `icon.svg` written into a scaffolded project — a self-contained,
 * valid SVG using Godot's signature rounded-rect blue background. Mirrors the
 * shape of the icon Godot writes for a fresh project (the exact glyph differs;
 * what matters is a valid default icon the editor can load at `res://icon.svg`).
 */
export const DEFAULT_ICON_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">
  <rect width="124" height="124" x="2" y="2" fill="#363d52" stroke="#212532" stroke-width="4" rx="14"/>
  <g fill="#fff">
    <circle cx="48" cy="58" r="10"/>
    <circle cx="80" cy="58" r="10"/>
    <rect x="40" y="82" width="48" height="8" rx="4"/>
  </g>
</svg>
`;

/** Engine project marker detected when refusing to scaffold over an existing project. */
export type ProjectMarkerEngine = 'godot' | 'unity' | 'unreal';

/**
 * Derive the project name from an explicit option, falling back to the target
 * folder's base name. Returns `GodotProject` only when both are empty. Pure.
 */
export function deriveProjectName(projectPath: string, explicitName?: string): string {
  const trimmed = explicitName?.trim();
  if (trimmed) return trimmed;
  const base = path.basename(path.resolve(projectPath));
  return base.length > 0 ? base : 'GodotProject';
}

/**
 * The `[dotnet] project/assembly_name` value AND the csproj filename. Godot
 * itself permits hyphens here (the infra testbed uses `Godot-Test-Project`), so
 * the name is kept verbatim apart from trimming; falls back when empty. Pure.
 */
export function toAssemblyName(projectName: string): string {
  const cleaned = projectName.trim();
  return cleaned.length > 0 ? cleaned : 'GodotProject';
}

/**
 * The csproj `<RootNamespace>` — must be a valid C# identifier, so every char
 * that is not a letter/digit/underscore is stripped and a leading digit is
 * prefixed with `_`. Mirrors the testbed's `Godot-Test-Project` ->
 * `GodotTestProject` derivation. Pure.
 */
export function toRootNamespace(projectName: string): string {
  const cleaned = projectName.replace(/[^A-Za-z0-9_]/g, '');
  if (cleaned.length === 0) return 'GodotProject';
  return /^[0-9]/.test(cleaned) ? `_${cleaned}` : cleaned;
}

/**
 * Escape a string for embedding inside a double-quoted Godot `ConfigFile`
 * value (the `project.godot` format): backslash -> `\\` and double-quote ->
 * `\"`. Without this, a name containing `"` or `\` produces a `project.godot`
 * Godot cannot parse — or, worse, lets a crafted name inject arbitrary config
 * keys. Pure. Exported for unit tests.
 */
export function escapeGodotConfigString(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

/**
 * Validate a project name before any filesystem write. Rejected when the name
 * would corrupt the generated `project.godot` (line breaks inside a quoted
 * `ConfigFile` string) or would escape / break the `--dotnet` csproj filename
 * (path separators, `..` traversal, or characters illegal in a filename).
 * Returns a human-readable reason when invalid, or `null` when safe. Pure.
 */
export function validateProjectName(projectName: string): string | null {
  if (/[\r\n]/.test(projectName)) return 'must not contain line breaks';
  if (/[/\\]/.test(projectName)) return 'must not contain path separators ("/" or "\\")';
  if (projectName.includes('..')) return 'must not contain ".." path segments';
  if (/[<>:"|?*]/.test(projectName)) return 'must not contain reserved characters (< > : " | ? *)';
  return null;
}

/**
 * Detect whether `dir` already hosts a project for any supported engine and, if
 * so, which marker file proves it. Checks (in order) a Godot `project.godot`, a
 * Unity `ProjectSettings/ProjectVersion.txt`, and any Unreal `*.uproject` file
 * directly inside `dir`. Returns `null` when no marker is present (including
 * when `dir` does not exist). Side-effect-free beyond read-only `fs` probing.
 */
export function detectExistingProjectMarker(
  dir: string,
): { engine: ProjectMarkerEngine; markerPath: string } | null {
  const godot = path.join(dir, 'project.godot');
  if (fs.existsSync(godot)) return { engine: 'godot', markerPath: godot };

  const unity = path.join(dir, 'ProjectSettings', 'ProjectVersion.txt');
  if (fs.existsSync(unity)) return { engine: 'unity', markerPath: unity };

  if (fs.existsSync(dir)) {
    try {
      const uproject = fs
        .readdirSync(dir)
        .find((entry) => entry.toLowerCase().endsWith('.uproject'));
      if (uproject) return { engine: 'unreal', markerPath: path.join(dir, uproject) };
    } catch {
      // A readdir failure here is not a marker; the scaffold's own mkdir/write
      // below will surface any real filesystem error as a structured failure.
    }
  }

  return null;
}

/**
 * Render the body of a minimal valid Godot 4.x `project.godot`
 * (`config_version=5`). When `dotnet` is set, adds the `"C#"` feature and a
 * `[dotnet]` section carrying the assembly name. Pure. Exported for unit tests.
 */
export function renderProjectGodot(projectName: string, dotnet: boolean): string {
  const features = dotnet ? 'PackedStringArray("4.3", "C#")' : 'PackedStringArray("4.3")';
  const dotnetSection = dotnet
    ? `\n[dotnet]\n\nproject/assembly_name="${escapeGodotConfigString(toAssemblyName(projectName))}"\n`
    : '';

  return `; Engine configuration file.
; It's best edited using the editor UI and not directly,
; since the parameters that go here are not all obvious.
;
; Format:
;   [section] ; section goes between []
;   param=value ; assign values to parameters

config_version=5

[application]

config/name="${escapeGodotConfigString(projectName)}"
config/features=${features}
config/icon="res://icon.svg"
${dotnetSection}`;
}

/**
 * Render a `Godot.NET.Sdk` csproj matching the infra `Godot-Test-Project/`
 * reference shape (Sdk + `net8.0` + `Nullable` + `RootNamespace`). The testbed's
 * addon-specific `PackageReference`s are intentionally NOT included — a freshly
 * scaffolded project is not necessarily an addon consumer. Pure. Exported for
 * unit tests.
 */
export function renderCsproj(projectName: string): string {
  return `<Project Sdk="Godot.NET.Sdk/${GODOT_SDK_VERSION}">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
    <RootNamespace>${toRootNamespace(projectName)}</RootNamespace>
  </PropertyGroup>

</Project>
`;
}

/**
 * Scaffold a minimal valid Godot 4.x project — the library-callable equivalent
 * of the `create-project` CLI command. Library-safe: never calls
 * `process.exit`, never prints, never throws past the public boundary.
 *
 * Writes `project.godot` (`config_version=5`) + `icon.svg`, plus a
 * `Godot.NET.Sdk` csproj when `dotnet` is set. Refuses (structured failure, no
 * writes) when the target already contains a Godot/Unity/Unreal project marker.
 * Pure `fs` operations only — no Godot binary required.
 */
export async function createProject(options: CreateProjectOptions): Promise<CreateProjectResult> {
  const warnings: string[] = [];
  const filesWritten: string[] = [];
  // Original bytes of any pre-existing file we overwrite, so a failed scaffold
  // can RESTORE the caller's file rather than delete it during rollback.
  const overwrittenOriginals = new Map<string, Buffer>();
  let resolvedProjectPath: string | undefined;
  let projectDirExistedBefore = true;

  try {
    const { projectPath } = resolveProjectPath(options.projectPath, process.cwd());
    resolvedProjectPath = projectPath;
    const dotnet = options.dotnet === true;
    const projectName = deriveProjectName(projectPath, options.name);

    // Validate the name BEFORE any writes — line breaks would corrupt the
    // quoted `project.godot` strings, and path separators / `..` / reserved
    // chars would let a crafted `--dotnet` csproj filename escape the target.
    const nameError = validateProjectName(projectName);
    if (nameError) {
      const hint = options.name?.trim()
        ? ''
        : ' (derived from the target folder name — pass --name to override)';
      throw new Error(`Invalid project name "${projectName}": ${nameError}.${hint}`);
    }

    emitProgress(options.onProgress, {
      phase: 'start',
      message: `Creating Godot project at ${projectPath}`,
    });

    // Refuse to scaffold over an existing project of ANY engine BEFORE writing.
    const existing = detectExistingProjectMarker(projectPath);
    if (existing) {
      throw new Error(
        `Refusing to scaffold: target already contains a ${existing.engine} project marker (${existing.markerPath}).`,
      );
    }

    // Create the target directory (and any parents) if absent. Record whether
    // it pre-existed so a failed scaffold can remove a directory it created
    // (but never one the caller already had).
    projectDirExistedBefore = fs.existsSync(projectPath);
    fs.mkdirSync(projectPath, { recursive: true });

    // Write one file, recording it for rollback and warning when an existing
    // non-marker file is clobbered.
    const writeFile = (filePath: string, contents: string): void => {
      // Only a pre-existing regular FILE is an overwrite worth warning about /
      // snapshotting; a directory at this path isn't a file we can clobber (the
      // write below will throw), so it must not produce a phantom warning.
      if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
        warnings.push(`Overwrote existing file: ${filePath}`);
        // Snapshot the caller's original bytes (once) so rollback restores them
        // instead of deleting a file they already had. If the read fails, fall
        // back to delete-on-rollback (best-effort).
        if (!overwrittenOriginals.has(filePath)) {
          try {
            overwrittenOriginals.set(filePath, fs.readFileSync(filePath));
          } catch {
            // Unreadable: treat as freshly-written (rmSync on rollback).
          }
        }
      }
      fs.writeFileSync(filePath, contents);
      filesWritten.push(filePath);
    };

    // Write the icon and (optional) csproj first, then `project.godot` LAST so
    // the engine marker only exists once the scaffold fully succeeds — a
    // mid-write failure leaves no marker to brick a corrected retry.
    const iconPath = path.join(projectPath, 'icon.svg');
    writeFile(iconPath, DEFAULT_ICON_SVG);

    if (dotnet) {
      const csprojPath = path.join(projectPath, `${toAssemblyName(projectName)}.csproj`);
      writeFile(csprojPath, renderCsproj(projectName));
    }

    const projectGodotPath = path.join(projectPath, 'project.godot');
    writeFile(projectGodotPath, renderProjectGodot(projectName, dotnet));

    emitProgress(options.onProgress, {
      phase: 'project-scaffolded',
      message: `Scaffolded ${filesWritten.length} file(s)`,
      projectPath,
    });
    emitProgress(options.onProgress, { phase: 'done', message: 'Project created.' });

    return {
      kind: 'success',
      success: true,
      projectPath,
      projectName,
      dotnet,
      filesWritten,
      warnings,
    };
  } catch (err: unknown) {
    // Roll back any files written before the failure (best-effort, reverse
    // order) so a failed/refused scaffold leaves the target as it was found.
    // A file we OVERWROTE is restored to its original bytes (never deleted —
    // that would leave the caller with less than they started with); a file we
    // freshly created is removed. Only entries whose cleanup FAILED stay in
    // `leftover` — honouring the CreateProjectFailure.filesWritten contract
    // (non-empty only when a partial scaffold could not be fully cleaned up).
    const leftover: string[] = [];
    for (const filePath of [...filesWritten].reverse()) {
      try {
        const original = overwrittenOriginals.get(filePath);
        if (original !== undefined) {
          fs.writeFileSync(filePath, original);
          // The overwrite was reverted, so drop the now-inaccurate warning.
          const idx = warnings.indexOf(`Overwrote existing file: ${filePath}`);
          if (idx !== -1) warnings.splice(idx, 1);
        } else {
          fs.rmSync(filePath, { force: true });
        }
      } catch {
        warnings.push(`Failed to roll back partially-written file: ${filePath}`);
        leftover.push(filePath);
      }
    }
    // We iterated in reverse for cleanup; restore write order for the contract.
    leftover.reverse();

    // If we created the target directory and rollback emptied it, remove it too
    // so a failed scaffold leaves the target as it was found. Best-effort: a
    // non-empty dir (pre-existing sibling content) keeps the rmdir from firing.
    if (resolvedProjectPath && !projectDirExistedBefore && leftover.length === 0) {
      try {
        fs.rmdirSync(resolvedProjectPath);
      } catch {
        // Directory not empty or already gone — leave it; cleanup is best-effort.
      }
    }

    const errorObj = err instanceof Error ? err : new Error(String(err));
    return {
      kind: 'failure',
      success: false,
      projectPath: resolvedProjectPath,
      warnings,
      filesWritten: leftover,
      errorMessage: errorObj.message,
      error: errorObj,
    };
  }
}
