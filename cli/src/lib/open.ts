import * as fs from 'fs';
import * as path from 'path';
import { findGodotBinary, launchEditor } from '../utils/godot-editor.js';
import { findGodotProcess } from '../utils/godot-process.js';
import {
  ENV_AUTH_OPTION,
  ENV_CLOUD_URL,
  ENV_CONNECTION_MODE,
  ENV_HOST,
  ENV_LOG_LEVEL,
  ENV_TOKEN,
} from '../utils/connection.js';
import { emitProgress } from './progress.js';
import { buildProject } from './build.js';
import type {
  OpenEnvInputs,
  OpenProjectAuthOption,
  OpenProjectConnectionMode,
  OpenProjectOptions,
  OpenProjectResult,
} from './types.js';

/**
 * Resolve the project path from an explicit option or fall back to the supplied
 * cwd. Returns an absolute path plus a flag indicating whether the cwd fallback
 * was used. Pure / no I/O.
 */
export function resolveProjectPath(
  optionPath: string | undefined,
  cwd: string,
): { projectPath: string; usedCwdFallback: boolean } {
  const explicit = optionPath;
  const resolvedPath = explicit ?? cwd;
  return {
    projectPath: path.resolve(resolvedPath),
    usedCwdFallback: explicit === undefined,
  };
}

/**
 * Returns true if `projectPath` looks like a Godot project — i.e. it contains a
 * `project.godot` file. Pure / no I/O beyond `fs.existsSync`.
 */
export function isGodotProjectDir(projectPath: string): boolean {
  return fs.existsSync(path.join(projectPath, 'project.godot'));
}

function isValidAuth(v: unknown): v is OpenProjectAuthOption {
  return v === 'None' || v === 'Required';
}

function isValidMode(v: unknown): v is OpenProjectConnectionMode {
  return v === 'Cloud' || v === 'Custom';
}

/**
 * Build the `GODOT_MCP_*` env-var map propagated to the editor process when
 * `noConnect !== true`. Pure (returns a fresh object); validates `auth` /
 * `mode` enums and throws on bad input — the `openProject` boundary catches the
 * throw and returns it as a `{ kind: 'failure' }` result.
 *
 * Exported for tests so the env-var assembly can be exercised without a real
 * editor launch.
 */
export function buildOpenEnv(options: OpenEnvInputs): Record<string, string> | undefined {
  if (options.noConnect === true) return undefined;

  const env: Record<string, string> = {};

  if (options.url !== undefined) env[ENV_HOST] = options.url;
  if (options.cloudUrl !== undefined) env[ENV_CLOUD_URL] = options.cloudUrl;
  if (options.token !== undefined) env[ENV_TOKEN] = options.token;
  if (options.logLevel !== undefined) env[ENV_LOG_LEVEL] = options.logLevel;

  if (options.auth !== undefined) {
    if (!isValidAuth(options.auth)) {
      throw new Error('auth must be "None" or "Required"');
    }
    env[ENV_AUTH_OPTION] = options.auth;
  }

  if (options.mode !== undefined) {
    if (!isValidMode(options.mode)) {
      throw new Error('mode must be "Cloud" or "Custom"');
    }
    env[ENV_CONNECTION_MODE] = options.mode;
  }

  return Object.keys(env).length > 0 ? env : undefined;
}

/**
 * Open a Godot project in the Godot editor — the library-callable equivalent of
 * the `open` CLI command. Library-safe: never calls `process.exit`, never
 * prints to stdout/stderr, never throws past the public boundary.
 *
 * Launches `<godot> --editor --path <project>` with the `GODOT_MCP_*` connection
 * env vars wired on.
 */
export async function openProject(options: OpenProjectOptions): Promise<OpenProjectResult> {
  const warnings: string[] = [];
  let resolvedProjectPath: string | undefined;
  let resolvedEditorPath: string | undefined;
  let built = false;

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
      message: `Opening Godot project at ${projectPath}`,
    });

    // Validate auth/mode BEFORE editor resolution so a typo fails fast.
    const env = buildOpenEnv(options);

    // Already-running short-circuit.
    const existingProcess = findGodotProcess(projectPath);
    if (existingProcess) {
      warnings.push(
        `Godot is already running with this project (PID: ${existingProcess.pid}). Skipping launch.`,
      );
      emitProgress(options.onProgress, {
        phase: 'done',
        message: 'Godot is already running for this project — launch skipped.',
      });
      return {
        kind: 'success',
        success: true,
        editorPath: existingProcess.commandLine.split(/\s+/)[0] ?? '',
        editorPid: existingProcess.pid,
        projectPath,
        warnings,
        alreadyRunning: true,
        built: false,
      };
    }

    // Build the C# assembly BEFORE launching the editor, so a fresh first open
    // finds a compiled assembly when it instantiates enabled EditorPlugins
    // (otherwise the editor shows "Unable to load addon script … Disabling the
    // addon"). GDScript-only projects are skipped inside buildProject. Opt out
    // with `build: false`. A build failure aborts the open — launching anyway
    // would reproduce the very disable-addon failure this guards against.
    if (options.build !== false) {
      const buildResult = await buildProject({
        projectPath,
        configuration: options.buildConfiguration,
        dotnetPath: options.dotnetPath,
        spawnImpl: options.buildSpawnImpl,
        onProgress: options.onProgress,
      });
      if (buildResult.kind === 'failure') {
        throw new Error(
          `Build before open failed; not launching the editor (it would disable the addon).\n${buildResult.errorMessage}`,
        );
      }
      for (const w of buildResult.warnings) warnings.push(w);
      built = !buildResult.skipped;
      if (!buildResult.skipped && buildResult.csprojPath !== undefined) {
        emitProgress(options.onProgress, {
          phase: 'build-succeeded',
          message: 'C# assembly built before launch.',
          csprojPath: buildResult.csprojPath,
          configuration: buildResult.configuration ?? 'Debug',
        });
      }
    }

    // Locate the Godot binary.
    const editorPath = findGodotBinary(options.editorPath);
    if (!editorPath) {
      throw new Error(
        'No Godot editor binary found.\n' +
          'Searched: --editor-path, the GODOT_BIN / GODOT4_BIN env vars, your PATH, and common\n' +
          'install locations (Downloads, Program Files, LocalAppData, Scoop, Chocolatey, winget,\n' +
          'Steam) including version-stamped release names like\n' +
          '"Godot_v<version>-stable_mono_win64.exe" nested in their extracted folder.\n' +
          'To fix, do one of:\n' +
          '  • pass the full path: godot-cli open --editor-path "<path-to-Godot-executable>"\n' +
          '  • set an env var: GODOT_BIN=<path-to-Godot-executable> (or GODOT4_BIN)\n' +
          '  • add the Godot binary to your PATH.',
      );
    }
    resolvedEditorPath = editorPath;

    emitProgress(options.onProgress, {
      phase: 'editor-resolved',
      message: `Resolved Godot editor at ${editorPath}`,
      editorPath,
    });

    emitProgress(options.onProgress, {
      phase: 'connection-details',
      message: env
        ? 'MCP connection environment variables prepared'
        : 'No MCP connection environment variables (--no-connect or no options provided)',
      projectPath,
      editorPath,
      envVars: env ?? {},
    });

    emitProgress(options.onProgress, {
      phase: 'launching-editor',
      message: 'Launching Godot editor',
      editorPath,
      projectPath,
    });

    const child = launchEditor(editorPath, projectPath, env, {
      onSpawn: (pid) => {
        emitProgress(options.onProgress, {
          phase: 'editor-launched',
          message: `Launched Godot editor (PID: ${pid ?? 'unknown'})`,
          pid,
        });
      },
      onError: (err) => {
        warnings.push(`Editor spawn reported error: ${err.message}`);
      },
    });

    const pid = await waitForSpawn(child);

    emitProgress(options.onProgress, { phase: 'done', message: 'Editor launched.' });

    return {
      kind: 'success',
      success: true,
      editorPath,
      editorPid: pid,
      projectPath,
      warnings,
      built,
    };
  } catch (err: unknown) {
    const errorObj = err instanceof Error ? err : new Error(String(err));
    return {
      kind: 'failure',
      success: false,
      projectPath: resolvedProjectPath,
      editorPath: resolvedEditorPath,
      warnings,
      errorMessage: errorObj.message,
      error: errorObj,
    };
  }
}

/**
 * Resolve to the spawned PID once the child process emits its `spawn` event, or
 * to `undefined` if a short timeout elapses or the process emits `error` first.
 * Library-safe: never throws, never blocks process exit (the caller detached).
 */
function waitForSpawn(
  child: import('child_process').ChildProcess,
  timeoutMs = 2000,
): Promise<number | undefined> {
  return new Promise((resolve) => {
    if (child.pid !== undefined && child.pid !== null) {
      resolve(child.pid);
      return;
    }
    let settled = false;
    const finish = (pid: number | undefined): void => {
      if (settled) return;
      settled = true;
      resolve(pid);
    };
    child.once('spawn', () => finish(child.pid ?? undefined));
    child.once('error', () => finish(undefined));
    setTimeout(() => finish(child.pid ?? undefined), timeoutMs).unref();
  });
}
