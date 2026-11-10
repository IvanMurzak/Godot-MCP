// Shared CLI plumbing of the headless commands (check, import, test, export, run-script).

import type { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { ExitCode, type ExitCodeValue } from '../utils/exit-codes.js';
import { CommandOutput, parseIntFlag, type EnvelopeError, type ErrorCode } from '../utils/output.js';
import { formatDiagnostic, type Diagnostic } from '../lib/godot-log.js';
import type { HeadlessFailureReason, HeadlessStep } from '../lib/headless.js';

const REASON_EXIT: Record<HeadlessFailureReason, { exitCode: ExitCodeValue; code: ErrorCode }> = {
  'invalid-options': { exitCode: ExitCode.Usage, code: 'USAGE' },
  'not-found': { exitCode: ExitCode.CommandFailed, code: 'NOT_FOUND' },
  'project-open': { exitCode: ExitCode.CommandFailed, code: 'PROJECT_OPEN' },
  timeout: { exitCode: ExitCode.Timeout, code: 'TIMEOUT' },
  'script-errors': { exitCode: ExitCode.CommandFailed, code: 'SCRIPT_ERRORS' },
  'compilation-failed': { exitCode: ExitCode.CommandFailed, code: 'COMPILATION_FAILED' },
  'import-failed': { exitCode: ExitCode.CommandFailed, code: 'IMPORT_FAILED' },
  'export-failed': { exitCode: ExitCode.CommandFailed, code: 'EXPORT_FAILED' },
  'tests-failed': { exitCode: ExitCode.TestsFailed, code: 'TESTS_FAILED' },
  'test-run-error': { exitCode: ExitCode.CommandFailed, code: 'TEST_RUN_ERROR' },
  // Ctrl+C / SIGTERM: not a verdict on the project.
  interrupted: { exitCode: ExitCode.Error, code: 'ERROR' },
};

export interface HeadlessCliOptions {
  path?: string;
  editorPath?: string;
  timeout: string;
  allowOpen?: boolean;
  format?: string;
}

/** `[path]`, `--path`, `--editor-path`, `--timeout`, `--allow-open`. */
export function addHeadlessOptions(cmd: Command, defaultTimeoutMs: number, allowOpen = true): Command {
  cmd
    .option('--path <path>', 'Godot project path (default: current directory)')
    .option('--editor-path <path>', 'Godot editor executable (default: GODOT_BIN, PATH, common install locations)')
    .option('--timeout <ms>', 'Hard timeout for the whole command; the Godot process tree is killed when it expires', String(defaultTimeoutMs));
  if (allowOpen) cmd.option('--allow-open', 'Run even when an editor has the project open (it may fight that editor over .godot/)');
  return cmd;
}

/** The project path from `[path]` / `--path` / cwd (validated by the library). */
export function headlessProjectPath(positional: string | undefined, options: HeadlessCliOptions): string {
  return positional ?? options.path ?? process.cwd();
}

export function headlessTimeout(options: HeadlessCliOptions): number {
  return parseIntFlag(options.timeout, '--timeout');
}

/** Diagnostics as the envelope's error entries, one per diagnostic; `code` picks each one's error code. */
export function diagnosticErrors(code: ErrorCode | ((d: Diagnostic) => ErrorCode), diagnostics: Diagnostic[]): EnvelopeError[] {
  return diagnostics.map((d) => ({ code: typeof code === 'function' ? code(d) : code, message: formatDiagnostic(d) }));
}

export function printDiagnostics(diagnostics: Diagnostic[]): void {
  for (const d of diagnostics) {
    if (d.severity === 'error') ui.error(formatDiagnostic(d));
    else ui.warn(formatDiagnostic(d));
  }
}

export function printSteps(steps: HeadlessStep[]): void {
  for (const s of steps) {
    ui.label(s.name, `${s.timedOut ? 'timed out' : `exit ${s.exitCode}`} in ${(s.durationMs / 1000).toFixed(1)}s`);
  }
}

/**
 * Finish a headless command from its library result: `failure` → the reason's exit code with `errors` (the summary
 * first), success → 0. Text output (beyond the final line) is printed by the caller first.
 */
export function finishHeadless(
  output: CommandOutput<string>,
  result: { kind: 'success' | 'failure'; reason?: HeadlessFailureReason; message?: string },
  data: unknown,
  extraErrors: EnvelopeError[] = [],
  successLine?: string,
): never {
  if (result.kind === 'success') {
    if (!output.machine && successLine) ui.success(successLine);
    return output.finish(ExitCode.Success, data);
  }
  const mapped = REASON_EXIT[result.reason!];
  if (!output.machine) ui.error(result.message ?? 'failed');
  return output.finish(mapped.exitCode, data, [{ code: mapped.code, message: result.message ?? 'failed' }, ...extraErrors]);
}

