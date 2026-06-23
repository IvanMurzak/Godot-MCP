import * as fs from 'fs';
import * as path from 'path';
import { spawn } from 'child_process';
import { resolveProjectPath, isGodotProjectDir } from './open.js';
import { emitProgress } from './progress.js';
import type { BuildProjectOptions, BuildProjectResult } from './types.js';

/**
 * Locate the single consumer `.csproj` directly at the Godot project root — the
 * `.csproj` Godot compiles every project `.cs` (addon included) into. A
 * `--dotnet` scaffold writes `<AssemblyName>.csproj` there. Returns `null` when
 * the project has no root csproj (a GDScript-only project — nothing to build).
 *
 * When more than one csproj sits at the root, the first alphabetically is
 * returned and the ambiguity is pushed onto `warnings` (all root csprojs compile
 * into the same Godot assembly, so building one builds the dependency graph the
 * editor will load). Pure / read-only `fs`. Exported for unit tests.
 */
export function findProjectCsproj(
  projectPath: string,
  warnings: string[],
): string | null {
  let names: string[];
  try {
    names = fs.readdirSync(projectPath).filter((n) => n.toLowerCase().endsWith('.csproj'));
  } catch {
    return null;
  }
  if (names.length === 0) return null;
  names.sort();
  if (names.length > 1) {
    warnings.push(
      `Multiple .csproj files found at the project root (${names.join(', ')}); built ${names[0]}.`,
    );
  }
  return path.join(projectPath, names[0]);
}

/** Default MSBuild configuration the build step compiles. */
const DEFAULT_CONFIGURATION = 'Debug';

/**
 * Build the C# assembly for a Godot project BEFORE the editor opens, so the
 * editor finds a compiled assembly when it instantiates enabled `EditorPlugin`s
 * (otherwise a fresh first open fails with "Unable to load addon script … —
 * Disabling the addon", godotengine/godot#112701-class behavior).
 *
 * Library-safe: never calls `process.exit`, never throws past the public
 * boundary, never writes to stdout/stderr (observability is via `onProgress`).
 *
 * Behavior:
 *   - Validates the path is an existing Godot project (`project.godot`).
 *   - GDScript-only projects (no root `.csproj`) are a SUCCESS with
 *     `skipped: true` / `skipReason: 'no-csproj'` — there is nothing to compile.
 *   - C# projects (a root `.csproj`) are built ALWAYS via `dotnet build <csproj>`.
 *     `dotnet build` is itself incremental, so an up-to-date project is a fast
 *     no-op; building unconditionally avoids the staleness-detection bugs that a
 *     "build-if-missing/stale" heuristic would introduce (the exact failure mode
 *     this fix exists to prevent — a stale/absent assembly the editor can't load).
 *   - A non-zero `dotnet build` exit (or a spawn error such as a missing
 *     `dotnet`) is a structured `{ kind: 'failure' }` carrying the captured
 *     output, so the caller can surface why the editor would disable the addon.
 */
export async function buildProject(options: BuildProjectOptions): Promise<BuildProjectResult> {
  const warnings: string[] = [];
  let resolvedProjectPath: string | undefined;

  try {
    const { projectPath } = resolveProjectPath(options.projectPath, process.cwd());
    resolvedProjectPath = projectPath;

    if (!fs.existsSync(projectPath)) {
      throw new Error(`Project path does not exist: ${projectPath}`);
    }

    if (!isGodotProjectDir(projectPath)) {
      throw new Error(`Not a Godot project (missing project.godot): ${projectPath}`);
    }

    emitProgress(options.onProgress, {
      phase: 'start',
      message: `Building C# for Godot project at ${projectPath}`,
    });

    const csprojPath = findProjectCsproj(projectPath, warnings);
    if (csprojPath === null) {
      // GDScript-only project — nothing to compile. The editor opens fine
      // without an assembly when no C# is present.
      emitProgress(options.onProgress, {
        phase: 'build-skipped',
        message: 'No .csproj at the project root — GDScript-only project, skipping build.',
        reason: 'no-csproj',
      });
      emitProgress(options.onProgress, { phase: 'done', message: 'Build skipped (no C#).' });
      return {
        kind: 'success',
        success: true,
        projectPath,
        skipped: true,
        skipReason: 'no-csproj',
        warnings,
      };
    }

    const configuration = options.configuration ?? DEFAULT_CONFIGURATION;
    const dotnetBin = options.dotnetPath ?? 'dotnet';
    const args = ['build', csprojPath, '--configuration', configuration];

    emitProgress(options.onProgress, {
      phase: 'build-running',
      message: `Building ${path.basename(csprojPath)} (${configuration})`,
      csprojPath,
      command: [dotnetBin, ...args].join(' '),
    });

    const run = await runDotnetBuild(dotnetBin, args, projectPath, options.spawnImpl);

    if (run.code !== 0) {
      const detail = run.output.trim();
      throw new Error(
        `dotnet build failed (exit ${run.code ?? 'null'}) for ${csprojPath}.` +
          (detail.length > 0 ? `\n${detail}` : ''),
      );
    }

    emitProgress(options.onProgress, {
      phase: 'done',
      message: `Built ${path.basename(csprojPath)} (${configuration}).`,
    });

    return {
      kind: 'success',
      success: true,
      projectPath,
      csprojPath,
      configuration,
      skipped: false,
      output: run.output,
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

/** A finished `dotnet build` invocation: its exit code + combined output. */
interface DotnetBuildRun {
  code: number | null;
  output: string;
}

/**
 * Spawn `dotnet build`, capturing stdout+stderr, and resolve once it exits.
 * Unlike the editor launch (which detaches), the build is awaited to completion
 * so the assembly exists before the caller proceeds. Library-safe: a spawn
 * `error` (e.g. `dotnet` not on PATH) resolves as a non-zero run rather than
 * throwing past the boundary. `spawnImpl` is injectable for unit tests.
 */
function runDotnetBuild(
  bin: string,
  args: string[],
  cwd: string,
  spawnImpl: typeof spawn = spawn,
): Promise<DotnetBuildRun> {
  return new Promise((resolve) => {
    let settled = false;
    const finish = (run: DotnetBuildRun): void => {
      if (settled) return;
      settled = true;
      resolve(run);
    };

    let child: import('child_process').ChildProcess;
    try {
      child = spawnImpl(bin, args, { cwd, stdio: ['ignore', 'pipe', 'pipe'] });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      finish({ code: 127, output: `Failed to spawn "${bin}": ${msg}` });
      return;
    }

    let output = '';
    child.stdout?.on('data', (chunk: Buffer | string) => {
      output += chunk.toString();
    });
    child.stderr?.on('data', (chunk: Buffer | string) => {
      output += chunk.toString();
    });
    child.on('error', (err: Error) => {
      finish({ code: 127, output: `${output}\nFailed to run "${bin}": ${err.message}` });
    });
    child.on('close', (code: number | null) => {
      finish({ code, output });
    });
  });
}
