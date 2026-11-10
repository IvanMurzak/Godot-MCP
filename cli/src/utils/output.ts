// `--format text|json|ndjson` output contract shared by godot-cli, unity-mcp-cli and unreal-mcp-cli (it lives in
// @baizor/gamedev-cli-core).
//
// json:   stdout carries exactly one pretty-printed envelope; every human-readable line goes to stderr.
// ndjson: progress lines `{"type":"progress",...}` on stderr, the envelope as one line on stdout.
// text:   the usual human output.

import type { Command } from 'commander';
import {
  CommandOutput as ContractOutput,
  OUTPUT_FORMATS,
  UsageError,
  activeCommandOutput,
  exitAfterFlush,
  parseFormat as parseContractFormat,
  type OutputFormat,
} from '@baizor/gamedev-cli-core';
import * as ui from './ui.js';
import { ExitCode, type ExitCodeValue } from './exit-codes.js';

export { CommandExit, OUTPUT_FORMATS, exitAfterFlush, formatFromArgv } from '@baizor/gamedev-cli-core';
export type { Envelope, OutputFormat } from '@baizor/gamedev-cli-core';

/** Upper-snake error codes of the envelope. */
export type ErrorCode =
  | 'USAGE'
  | 'TOOL_FAILED'
  | 'UNREACHABLE'
  | 'TIMEOUT'
  | 'BUSY'
  | 'AWAITING_INPUT'
  | 'TESTS_FAILED'
  | 'TEST_RUN_ERROR'
  | 'COMPILATION_FAILED'
  | 'SCRIPT_ERRORS'
  | 'IMPORT_FAILED'
  | 'PROJECT_OPEN'
  | 'NOT_FOUND'
  | 'EXPORT_FAILED'
  | 'NOT_AUTHENTICATED'
  | 'ERROR';

export interface EnvelopeError {
  code: ErrorCode;
  message: string;
}

/** Add `--format` to a command. `extra` lists command-specific formats (e.g. `github`). */
export function addFormatOption(cmd: Command, extra: readonly string[] = []): Command {
  const formats = [...OUTPUT_FORMATS, ...extra].join('|');
  return cmd.option('--format <format>', `Output format: ${formats}`, 'text');
}

/**
 * The output of one command run. Created at the start of an action; in json/ndjson it switches `ui` to stderr so
 * stdout carries only the envelope. Exactly one `finish` per run.
 */
export class CommandOutput<F extends string = OutputFormat> extends ContractOutput<ErrorCode> {
  declare readonly format: F;

  constructor(command: string, format: F) {
    super(command, format);
    // ndjson: every human line on stderr is an event too, so stderr stays parseable line by line.
    ui.setMachineOutput(this.machine, format === 'ndjson'
      ? (level, message) => JSON.stringify({ type: 'log', command, level, message })
      : undefined);
  }

  /** A progress line: ndjson event on stderr, plain stderr line in json, `ui.info` in text. */
  override progress(message: string): void {
    if (this.format === 'ndjson') super.progress(message);
    else if (this.format === 'json') process.stderr.write(message + '\n');
    else ui.info(message);
  }

  /**
   * Emit the envelope (machine formats) and end the command. Text output is the caller's job before calling this.
   * The process exits once stdout is flushed; until then a `CommandExit` unwinds the action.
   */
  override finish(exitCode: ExitCodeValue, data: unknown = null, errors: EnvelopeError[] = []): never {
    for (const err of errors) this.record(err.code, err.message);
    this.emit(exitCode, data);
    exitAfterFlush(exitCode);
  }

  /** Fail with one error: printed with `ui.error` in text, in the envelope otherwise. */
  fail(exitCode: ExitCodeValue, code: ErrorCode, message: string, data: unknown = null): never {
    if (!this.machine) ui.error(message);
    return this.finish(exitCode, data, [{ code, message }]);
  }
}

/** The godot-cli output of the command being run, if one was created. */
function active(): CommandOutput<string> | undefined {
  const output = activeCommandOutput();
  return output instanceof CommandOutput ? output : undefined;
}

/**
 * An unexpected error escaped a command: report it through the active command's envelope (exit 1) when one runs
 * in a machine format. Returns false when there is none (the caller prints it).
 */
export function failActive(err: unknown): boolean {
  const output = active();
  if (!output?.machine) return false;
  return output.fail(ExitCode.Error, 'ERROR', err instanceof Error ? err.message : String(err));
}

/**
 * Exit from a shared helper (path / input validation): through the active command's envelope when one runs in a
 * machine format, else `ui.error`.
 */
export function exitWithError(exitCode: ExitCodeValue, code: ErrorCode, message: string): never {
  active()?.fail(exitCode, code, message);
  ui.error(message);
  process.exit(exitCode);
}

/**
 * Parse a `--format` value. An unknown value is a usage error (exit 2), reported in text since the format itself
 * is what is wrong.
 */
export function parseFormat<F extends string = never>(raw: string | undefined, extra: readonly F[] = []): OutputFormat | F {
  try {
    return parseContractFormat(raw, extra);
  } catch (err) {
    if (!(err instanceof UsageError)) throw err;
    ui.error(err.message);
    process.exit(ExitCode.Usage);
  }
}

/** Parse a positive (or, with `allowZero`, non-negative) whole-number flag, or exit 2. */
export function parseIntFlag(raw: string | undefined, flagName: string, allowZero = false): number {
  const value = Number(raw);
  if (raw === undefined || raw.trim() === '' || !Number.isInteger(value) || value < 0 || (value === 0 && !allowZero)) {
    exitWithError(ExitCode.Usage, 'USAGE', `Invalid ${flagName} value: "${raw}". Must be a ${allowZero ? 'non-negative' : 'positive'} whole number.`);
  }
  return value;
}
