// `runScript`: a one-shot SceneTree / MainLoop GDScript (`godot --headless --path <project> -s <file>`) with a hard
// timeout. The verdict is the exit code the script passed to `quit(code)`, checked against the log: a script that
// failed to parse makes Godot exit 0 without running anything. Library-safe.

import * as fs from 'fs';
import * as path from 'path';
import { parseGodotLog, type Diagnostic } from './godot-log.js';
import {
  engineRan,
  headlessFailure,
  prepareHeadless,
  runGodotStep,
  type HeadlessBaseOptions,
  type HeadlessFailure,
  type HeadlessStep,
} from './headless.js';

export interface RunScriptOptions extends HeadlessBaseOptions {
  /** `res://…` or a file path (absolute or relative to the current directory). */
  script: string;
  /** Passed after `--` (`OS.get_cmdline_user_args()`). */
  scriptArgs?: string[];
  /** Each output line as it arrives. */
  onLine?: (line: string) => void;
}

export interface RunScriptReport {
  projectPath: string;
  script: string;
  /** The code the script passed to `quit()`. */
  scriptExitCode: number | null;
  output: string;
  steps: HeadlessStep[];
  diagnostics: Diagnostic[];
}

export type RunScriptResult =
  | ({ kind: 'success'; success: true } & RunScriptReport)
  | ({ kind: 'failure'; success: false; reason: HeadlessFailure['reason']; message: string } & Partial<RunScriptReport>);

/** `res://` path of a script inside the project, the absolute path otherwise (Godot loads both). */
export function scriptPathForGodot(projectPath: string, script: string, cwd = process.cwd()): string {
  if (script.startsWith('res://')) return script;
  const abs = path.resolve(cwd, script);
  const rel = path.relative(projectPath, abs);
  if (!rel.startsWith('..') && !path.isAbsolute(rel)) return `res://${rel.split(path.sep).join('/')}`;
  return abs.split(path.sep).join('/');
}

export async function runScript(options: RunScriptOptions): Promise<RunScriptResult> {
  const ctx = prepareHeadless(options);
  if (ctx.kind === 'failure') return ctx;
  const script = scriptPathForGodot(ctx.projectPath, options.script);
  const onDisk = script.startsWith('res://') ? path.join(ctx.projectPath, script.slice('res://'.length)) : script;
  if (!fs.existsSync(onDisk)) return headlessFailure('not-found', `Script not found: ${onDisk}`);

  const args = ['-s', script, ...(options.scriptArgs && options.scriptArgs.length > 0 ? ['--', ...options.scriptArgs] : [])];
  const run = await runGodotStep(ctx, 'run-script', args, options.onLine ? { onLine: options.onLine } : {});
  const diagnostics = parseGodotLog(run.output).filter((d) => d.source !== 'engine');
  const report: RunScriptReport = { projectPath: ctx.projectPath, script, scriptExitCode: run.exitCode, output: run.output, steps: ctx.steps, diagnostics };
  const fail = (failure: HeadlessFailure): RunScriptResult => ({ ...failure, ...report });

  if (run.timedOut) return fail(headlessFailure('timeout', 'The script timed out (the Godot process tree was killed).'));
  if (!engineRan(run)) return fail(headlessFailure('script-errors', `Godot did not start${run.spawnError ? `: ${run.spawnError}` : ''} (exit ${run.exitCode}).`));
  const loadErrors = diagnostics.filter((d) => d.severity === 'error' && d.phase === 'load');
  if (loadErrors.length > 0) return fail(headlessFailure('script-errors', `The script failed to load (${loadErrors.length} error(s)).`));
  if (run.exitCode !== 0) return fail(headlessFailure('script-errors', `The script exited with code ${run.exitCode}.`));
  return { kind: 'success', success: true, ...report };
}
