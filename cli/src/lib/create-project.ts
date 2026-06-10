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
    ? `\n[dotnet]\n\nproject/assembly_name="${toAssemblyName(projectName)}"\n`
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

config/name="${projectName}"
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
  let resolvedProjectPath: string | undefined;

  try {
    const { projectPath } = resolveProjectPath(options.projectPath, process.cwd());
    resolvedProjectPath = projectPath;
    const dotnet = options.dotnet === true;
    const projectName = deriveProjectName(projectPath, options.name);

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

    // Create the target directory (and any parents) if absent.
    fs.mkdirSync(projectPath, { recursive: true });

    const filesWritten: string[] = [];

    const projectGodotPath = path.join(projectPath, 'project.godot');
    fs.writeFileSync(projectGodotPath, renderProjectGodot(projectName, dotnet));
    filesWritten.push(projectGodotPath);

    const iconPath = path.join(projectPath, 'icon.svg');
    fs.writeFileSync(iconPath, DEFAULT_ICON_SVG);
    filesWritten.push(iconPath);

    if (dotnet) {
      const csprojPath = path.join(projectPath, `${toAssemblyName(projectName)}.csproj`);
      fs.writeFileSync(csprojPath, renderCsproj(projectName));
      filesWritten.push(csprojPath);
    }

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
    const errorObj = err instanceof Error ? err : new Error(String(err));
    return {
      kind: 'failure',
      success: false,
      projectPath: resolvedProjectPath,
      warnings,
      errorMessage: errorObj.message,
      error: errorObj,
    };
  }
}
