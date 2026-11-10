// Shared plumbing of the editor-less headless commands (check, import, test, export, run-script): binary and project
// resolution, the "project is open" guard, timeout budgets and the `--import` step. Library-safe.

import * as fs from 'fs';
import * as path from 'path';
import { findGodotBinary } from '../utils/godot-editor.js';
import { findGodotProcess } from '../utils/godot-process.js';
import { GODOT_BANNER_RE, parseGodotLog, type Diagnostic } from './godot-log.js';
import { runGodot, type GodotRunOptions, type GodotRunResult } from './godot-run.js';

export type HeadlessFailureReason =
  | 'invalid-options'
  | 'not-found'
  | 'project-open'
  | 'timeout'
  | 'script-errors'
  | 'compilation-failed'
  | 'import-failed'
  | 'export-failed'
  | 'tests-failed'
  | 'test-run-error';

/** One Godot / dotnet process a headless command ran. */
export interface HeadlessStep {
  name: string;
  command: string[];
  exitCode: number | null;
  timedOut: boolean;
  durationMs: number;
}

export interface HeadlessBaseOptions {
  projectPath: string;
  /** Explicit Godot executable (else GODOT_BIN / PATH / common install locations). */
  editorPath?: string;
  /** Total budget for the whole command, in ms. */
  timeoutMs: number;
  /** Run even when an editor has the project open. */
  allowOpenProject?: boolean;
  onProgress?: (message: string) => void;
  /** Tests. */
  deps?: Partial<HeadlessDeps>;
}

export interface HeadlessDeps {
  runGodot: (options: GodotRunOptions) => Promise<GodotRunResult>;
  findBinary: (editorPath?: string) => string | null;
  findOpenEditor: (projectPath: string) => { pid: number } | null;
}

export const defaultHeadlessDeps: HeadlessDeps = {
  runGodot,
  findBinary: (editorPath) => findGodotBinary(editorPath),
  findOpenEditor: (projectPath) => findGodotProcess(projectPath),
};

export interface HeadlessFailure {
  kind: 'failure';
  success: false;
  reason: HeadlessFailureReason;
  message: string;
}

export function headlessFailure(reason: HeadlessFailureReason, message: string): HeadlessFailure {
  return { kind: 'failure', success: false, reason, message };
}

export interface HeadlessContext {
  kind: 'context';
  projectPath: string;
  binary: string;
  deps: HeadlessDeps;
  /** `<project>/.godot/godot-cli`: scratch scripts and logs (ignored by Godot's file system and by VCS). */
  workDir: string;
  deadline: number;
  steps: HeadlessStep[];
}

/** The scratch folder of the headless commands inside a project. */
export function cliWorkDir(projectPath: string): string {
  return path.join(projectPath, '.godot', 'godot-cli');
}

/**
 * Validate the project, find the binary and refuse when an editor has the project open (an `--import` against an
 * open project fights the editor over `.godot/`).
 */
export function prepareHeadless(options: HeadlessBaseOptions): HeadlessContext | HeadlessFailure {
  const deps: HeadlessDeps = { ...defaultHeadlessDeps, ...options.deps };
  const projectPath = path.resolve(options.projectPath);
  if (!fs.existsSync(path.join(projectPath, 'project.godot'))) {
    return headlessFailure('invalid-options', `Not a Godot project (missing project.godot): ${projectPath}`);
  }
  if (!Number.isFinite(options.timeoutMs) || options.timeoutMs <= 0) {
    return headlessFailure('invalid-options', 'timeout must be a positive number of milliseconds');
  }
  const binary = deps.findBinary(options.editorPath);
  if (!binary) {
    return headlessFailure('not-found', 'No Godot editor binary found: pass --editor-path or set GODOT_BIN.');
  }
  if (!options.allowOpenProject) {
    const open = deps.findOpenEditor(projectPath);
    if (open) {
      return headlessFailure(
        'project-open',
        `The project is open in a Godot editor (PID ${open.pid}); close it first (\`godot-cli close\`), or pass --allow-open.`,
      );
    }
  }
  return { kind: 'context', projectPath, binary, deps, workDir: cliWorkDir(projectPath), deadline: Date.now() + options.timeoutMs, steps: [] };
}

export function remainingMs(ctx: HeadlessContext): number {
  return Math.max(0, ctx.deadline - Date.now());
}

/** Run one headless Godot step in the project within the remaining budget and record it. */
export async function runGodotStep(
  ctx: HeadlessContext,
  name: string,
  args: string[],
  extra: Pick<GodotRunOptions, 'env' | 'onLine'> = {},
): Promise<GodotRunResult> {
  const fullArgs = ['--headless', '--path', ctx.projectPath, ...args];
  const timeoutMs = remainingMs(ctx);
  if (timeoutMs <= 0) {
    ctx.steps.push({ name, command: [ctx.binary, ...fullArgs], exitCode: null, timedOut: true, durationMs: 0 });
    return { exitCode: null, timedOut: true, output: '', durationMs: 0 };
  }
  const result = await ctx.deps.runGodot({
    binary: ctx.binary,
    args: fullArgs,
    cwd: ctx.projectPath,
    timeoutMs,
    logFile: path.join(ctx.workDir, 'logs', `${name}.log`),
    ...extra,
  });
  ctx.steps.push({ name, command: [ctx.binary, ...fullArgs], exitCode: result.exitCode, timedOut: result.timedOut, durationMs: result.durationMs });
  return result;
}

/** Whether the run printed Godot's start banner: without it nothing ran and the log proves nothing. */
export function engineRan(result: GodotRunResult): boolean {
  return result.spawnError === undefined && GODOT_BANNER_RE.test(result.output);
}

export interface ImportStepResult {
  result: GodotRunResult;
  diagnostics: Diagnostic[];
  /** Set when the step decides the command's verdict. */
  failure?: HeadlessFailure;
}

/**
 * `--import`: materialise `.godot/` (imported resources, the global class cache every later step needs). Fails on a
 * timeout, an engine that never started, script errors or `Error importing` lines; the exit code is not trusted.
 */
export async function runImportStep(ctx: HeadlessContext, onLine?: (line: string) => void): Promise<ImportStepResult> {
  const result = await runGodotStep(ctx, 'import', ['--import'], onLine ? { onLine } : {});
  const diagnostics = parseGodotLog(result.output);
  if (result.timedOut) return { result, diagnostics, failure: headlessFailure('timeout', 'The import timed out (the Godot process tree was killed).') };
  if (!engineRan(result)) {
    return { result, diagnostics, failure: headlessFailure('import-failed', `Godot did not start${result.spawnError ? `: ${result.spawnError}` : ''} (exit ${result.exitCode}).`) };
  }
  const scriptErrors = diagnostics.filter((d) => d.severity === 'error' && d.source === 'gdscript');
  if (scriptErrors.length > 0) return { result, diagnostics, failure: headlessFailure('script-errors', `${scriptErrors.length} script error(s) during import.`) };
  const importErrors = diagnostics.filter((d) => d.severity === 'error' && /Error importing|Failed to import|Cannot import/i.test(d.message));
  if (importErrors.length > 0) return { result, diagnostics, failure: headlessFailure('import-failed', `${importErrors.length} resource(s) failed to import.`) };
  if (result.exitCode !== 0) return { result, diagnostics, failure: headlessFailure('import-failed', `Godot exited with code ${result.exitCode} during the import.`) };
  return { result, diagnostics };
}

/** Write a GDScript the CLI runs with `-s` into the work dir; returns its `res://` path. */
export function writeCliScript(ctx: HeadlessContext, name: string, source: string): string {
  fs.mkdirSync(ctx.workDir, { recursive: true });
  fs.writeFileSync(path.join(ctx.workDir, name), source, 'utf-8');
  return `res://.godot/godot-cli/${name}`;
}
