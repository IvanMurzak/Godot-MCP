// `--format text|json|ndjson` output contract shared by godot-cli, unity-mcp-cli and unreal-mcp-cli.
//
// json:   stdout carries exactly one pretty-printed envelope; every human-readable line goes to stderr.
// ndjson: progress lines `{"type":"progress",...}` on stderr, the envelope as one line on stdout.
// text:   the usual human output.

import type { Command } from 'commander';
import * as ui from './ui.js';
import { ExitCode, type ExitCodeValue } from './exit-codes.js';

export type OutputFormat = 'text' | 'json' | 'ndjson';

export const OUTPUT_FORMATS: readonly OutputFormat[] = ['text', 'json', 'ndjson'];

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
  | 'CLOUD_PROJECT'
  | 'ERROR';

export interface EnvelopeError {
  code: ErrorCode;
  message: string;
}

export interface Envelope {
  success: boolean;
  command: string;
  data: unknown;
  errors: EnvelopeError[];
  warnings: string[];
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
export class CommandOutput<F extends string = OutputFormat> {
  readonly warnings: string[] = [];

  constructor(readonly command: string, readonly format: F) {
    // ndjson: every human line on stderr is an event too, so stderr stays parseable line by line.
    ui.setMachineOutput(this.machine, format === 'ndjson'
      ? (level, message) => JSON.stringify({ type: 'log', command, level, message })
      : undefined);
    active = this as CommandOutput<string>;
  }

  /** json / ndjson: no human output on stdout. */
  get machine(): boolean {
    return this.format === 'json' || this.format === 'ndjson';
  }

  /** A progress line: ndjson event on stderr, plain stderr line in json, `ui.info` in text. */
  progress(message: string): void {
    if (this.format === 'ndjson') {
      process.stderr.write(JSON.stringify({ type: 'progress', command: this.command, message }) + '\n');
    } else if (this.format === 'json') {
      process.stderr.write(message + '\n');
    } else {
      ui.info(message);
    }
  }

  /** A warning: in the envelope (machine formats), on stderr in text (stdout may be a result being piped). */
  warn(message: string): void {
    this.warnings.push(message);
    if (!this.machine) process.stderr.write(`WARN: ${message}
`);
  }

  /**
   * Emit the envelope (machine formats) and end the command. Text output is the caller's job before calling this.
   * The process exits once stdout is flushed (a write to a TTY or a macOS pipe is asynchronous: an immediate
   * `process.exit` can cut a large result short); until then a {@link CommandExit} unwinds the action.
   */
  finish(exitCode: ExitCodeValue, data: unknown, errors: EnvelopeError[] = []): never {
    if (this.machine) writeEnvelope(this.format, { success: exitCode === ExitCode.Success, command: this.command, data: data ?? null, errors, warnings: this.warnings });
    exitAfterFlush(exitCode);
  }

  /** Fail with one error: printed with `ui.error` in text, in the envelope otherwise. */
  fail(exitCode: ExitCodeValue, code: ErrorCode, message: string, data: unknown = null): never {
    if (!this.machine) ui.error(message);
    return this.finish(exitCode, data, [{ code, message }]);
  }
}

let active: CommandOutput<string> | undefined;

/** Thrown by {@link exitAfterFlush} to stop the action; the entry point ignores it (the exit is already scheduled). */
export class CommandExit extends Error {
  constructor(readonly exitCode: number) {
    super(`exit ${exitCode}`);
  }
}

/** Exit with `exitCode` once stdout and stderr have flushed; throws {@link CommandExit} to unwind the caller. */
export function exitAfterFlush(exitCode: number): never {
  process.exitCode = exitCode;
  let pending = 2;
  const done = (): void => {
    if (--pending === 0) process.exit(exitCode);
  };
  process.stdout.write('', done);
  process.stderr.write('', done);
  throw new CommandExit(exitCode);
}

/**
 * An unexpected error escaped a command: report it through the active command's envelope (exit 1) when one runs
 * in a machine format. Returns false when there is none (the caller prints it).
 */
export function failActive(err: unknown): boolean {
  if (!active?.machine) return false;
  return active.fail(ExitCode.Error, 'ERROR', err instanceof Error ? err.message : String(err));
}

/** Write an envelope to stdout in `format` (json: pretty-printed, anything else: one line). */
export function writeEnvelope(format: string, envelope: Envelope): void {
  process.stdout.write(format === 'json' ? JSON.stringify(envelope, null, 2) + '\n' : JSON.stringify(envelope) + '\n');
}

/**
 * Exit from a shared helper (path / input validation): through the active command's envelope when one runs in a
 * machine format, else `ui.error`.
 */
export function exitWithError(exitCode: ExitCodeValue, code: ErrorCode, message: string): never {
  if (active) active.fail(exitCode, code, message);
  ui.error(message);
  process.exit(exitCode);
}

/**
 * Parse a `--format` value. An unknown value is a usage error (exit 2), reported in text since the format itself
 * is what is wrong.
 */
export function parseFormat<F extends string = OutputFormat>(raw: string | undefined, extra: readonly string[] = []): F {
  const value = (raw ?? 'text').toLowerCase();
  const allowed = [...OUTPUT_FORMATS, ...extra];
  if (!allowed.includes(value)) {
    ui.error(`Invalid --format value: "${raw}". Expected one of: ${allowed.join(', ')}.`);
    process.exit(ExitCode.Usage);
  }
  return value as F;
}

/**
 * The `--format` value in raw argv (for errors commander reports before any action runs), or undefined.
 * Accepts `--format json` and `--format=json`.
 */
export function formatFromArgv(argv: readonly string[]): string | undefined {
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--format') return argv[i + 1];
    if (arg.startsWith('--format=')) return arg.slice('--format='.length);
  }
  return undefined;
}

/** Parse a positive (or, with `allowZero`, non-negative) whole-number flag, or exit 2. */
export function parseIntFlag(raw: string | undefined, flagName: string, allowZero = false): number {
  const value = Number(raw);
  if (raw === undefined || raw.trim() === '' || !Number.isInteger(value) || value < 0 || (value === 0 && !allowZero)) {
    exitWithError(ExitCode.Usage, 'USAGE', `Invalid ${flagName} value: "${raw}". Must be a ${allowZero ? 'non-negative' : 'positive'} whole number.`);
  }
  return value;
}
