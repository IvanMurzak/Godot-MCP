import { Command } from 'commander';
import { DEFAULT_SESSION_READY_TIMEOUT_MS } from '@baizor/gamedev-cli-core';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { isGodotProject, localProjectUrl, resolveAndValidateProjectPath } from '../utils/connection.js';
import { localFallbackNote, resolveEditorConnection } from '../utils/editor-connection.js';
import { parseInput } from '../utils/input.js';
import { isLocalhostUrl } from '../utils/project-marker.js';
import { ExitCode, type ExitCodeValue } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag, type ErrorCode } from '../utils/output.js';
import { ensureEditor, type EnsureEditorResult } from '../lib/session.js';
import { toolErrorText } from '../lib/exec.js';
import type { RunToolFailure, RunToolOptions, RunToolResult } from '../lib/types.js';

interface BuilderOptions {
  /** CLI subcommand name, e.g. `'run-tool'`. */
  name: string;
  /** `description()` text for the commander command. */
  description: string;
  /** Description shown next to the `<tool-name>` argument. */
  argDescription: string;
  /** URL path prefix the lib function targets, e.g. `'/api/tools'`. */
  routePrefix: string;
  /** Verbose-log label, e.g. `'Tool'` or `'System tool'`. */
  verboseLabel: string;
  /** Heading printed by `ui.heading`, e.g. `'Run Tool'`. */
  headingLabel: string;
  /**
   * Lower-case noun used in user-facing failure copy:
   * `Failed to call <noun>:` and `<Capitalized> call timed out…`.
   */
  errorNoun: string;
  /** Library function — `runTool` or `runSystemTool`. */
  invoke: (opts: RunToolOptions) => Promise<RunToolResult>;
}

interface CliOptions {
  path?: string;
  url?: string;
  token?: string;
  input?: string;
  inputFile?: string;
  raw?: boolean;
  timeout?: string;
  format?: string;
  start?: boolean;
  once?: boolean;
  build?: boolean;
  editorPath?: string;
  readyTimeout: string;
}

export interface FailureContext {
  toolName: string;
  endpoint: string;
  raw: boolean | undefined;
  timeoutMs: number;
  errorNoun: string;
}

/** Exit code and envelope error code of a failed tool call. */
export function toolFailureExit(failure: RunToolFailure): { exitCode: ExitCodeValue; code: ErrorCode } {
  switch (failure.reason) {
    case 'timeout':
      return { exitCode: ExitCode.Timeout, code: 'TIMEOUT' };
    case 'connection-refused':
    case 'connection-reset':
    case 'network-error':
      return { exitCode: ExitCode.Unreachable, code: 'UNREACHABLE' };
    case 'http-error':
      if (failure.httpStatus === 503) return { exitCode: ExitCode.Busy, code: 'BUSY' };
      // A gateway that gave up on the editor (504) or could not reach it (502).
      if (failure.httpStatus === 504) return { exitCode: ExitCode.Timeout, code: 'TIMEOUT' };
      if (failure.httpStatus === 502) return { exitCode: ExitCode.Unreachable, code: 'UNREACHABLE' };
      return { exitCode: ExitCode.CommandFailed, code: 'TOOL_FAILED' };
    case 'invalid-input':
      return { exitCode: ExitCode.Usage, code: 'USAGE' };
    default:
      return { exitCode: ExitCode.Error, code: 'ERROR' };
  }
}

/** Exit code and envelope error code when no editor could be obtained. */
export function ensureFailureExit(reason: Extract<EnsureEditorResult, { kind: 'failure' }>['reason']): { exitCode: ExitCodeValue; code: ErrorCode } {
  if (reason === 'build-failed') return { exitCode: ExitCode.CommandFailed, code: 'COMPILATION_FAILED' };
  if (reason === 'invalid-options') return { exitCode: ExitCode.Usage, code: 'USAGE' };
  return { exitCode: ExitCode.Unreachable, code: 'UNREACHABLE' };
}

/** A tool response that carries `status: "error"`: the call reached the tool and the tool failed. */
export function isToolErrorResponse(data: unknown): boolean {
  return typeof data === 'object' && data !== null && (data as { status?: unknown }).status === 'error';
}

export function buildRunToolCommand(cfg: BuilderOptions): Command {
  return addFormatOption(
    new Command(cfg.name)
      .description(cfg.description)
      .argument('<tool-name>', cfg.argDescription)
      .argument('[path]', 'Godot project path (used to validate project.godot when --url is omitted)')
      .option('--path <path>', 'Godot project path')
      .option('--url <url>', 'MCP server base URL (defaults to GODOT_MCP_HOST/cloud)')
      .option('--token <token>', 'Bearer token override (defaults to GODOT_MCP_TOKEN)')
      .option('--input <json>', 'JSON string of tool arguments')
      .option('--input-file <file>', 'Read JSON arguments from file')
      .option('--raw', 'Output raw JSON (no formatting)')
      .option('--timeout <ms>', 'Request timeout in milliseconds (default: 60000)', '60000')
      .option('--no-start', 'Do not start a headless editor when no editor answers on the local server')
      .option('--once', 'Close the editor after the call when this call started it')
      .option('--no-build', 'Do not build the C# assembly before starting an editor')
      .option('--editor-path <path>', 'Godot editor executable for a started editor')
      .option('--ready-timeout <ms>', 'How long to wait for a started editor to become ready', String(DEFAULT_SESSION_READY_TIMEOUT_MS)),
  ).action(async (toolName: string, positionalPath: string | undefined, options: CliOptions) => {
    const output = new CommandOutput(cfg.name, parseFormat(options.format));
    const human = !options.raw && !output.machine;
    // Validate flags first so a bad value short-circuits before we read --input-file or hit the network.
    const timeoutMs = parseIntFlag(options.timeout, '--timeout');
    const readyTimeoutMs = parseIntFlag(options.readyTimeout, '--ready-timeout');

    // Resolve path + connection up front so the heading and verbose output
    // reflect the final endpoint before the HTTP call fires.
    const projectPath = resolveAndValidateProjectPath(positionalPath, options);
    // Cloud mode: the hub with the sign-in; never signed in: the local server (warning); unusable sign-in: refuse.
    const conn = await resolveEditorConnection(projectPath, options);
    if (conn.signInRequired) return output.fail(ExitCode.CommandFailed, 'NOT_AUTHENTICATED', conn.signInRequired);
    if (conn.localFallback) output.warn(localFallbackNote(conn.url));
    const { url: baseUrl, token } = conn;
    const body = parseInput(options);
    let endpoint = `${baseUrl}${cfg.routePrefix}/${encodeURIComponent(toolName)}`;
    const authSource = options.token ? '--token flag' : 'config/env';

    verbose(`${cfg.verboseLabel}: ${toolName}`);
    verbose(`Endpoint: ${endpoint}`);
    verbose(`Body: ${body}`);
    if (token) verbose(`Authorization header set (source: ${authSource})`);

    if (human) {
      ui.heading(cfg.headingLabel);
      ui.label('Tool', toolName);
      ui.label('URL', endpoint);
      if (token) ui.label('Auth', `from ${authSource}`);
      ui.divider();
    }

    const spinner = human ? ui.startSpinner(`Calling ${toolName}...`) : null;
    const call = (url: string) => cfg.invoke({ toolName, url, ...(token ? { token } : {}), input: body, timeoutMs });
    let result = await call(baseUrl);
    // Status lines: the spinner (text), the progress stream (json/ndjson), plain stderr with --raw (stdout is the payload).
    const note = (message: string): void => {
      if (spinner) spinner.text = message;
      else if (options.raw && !output.machine) process.stderr.write(message + '\n');
      else output.progress(message);
    };

    // No editor answers on the project's local server: start a headless one (exec's policy: a running editor is
    // reused and never closed), then retry.
    // With an explicit --url, start only the editor of a project that was named, or whose own local server that is:
    // never one for whatever Godot project the current directory happens to be.
    const explicitProject = positionalPath !== undefined || options.path !== undefined;
    const ownsUrl = !options.url || explicitProject || normalizeUrl(options.url) === normalizeUrl(localProjectUrl(projectPath));
    const canStart = options.start !== false && isLocalhostUrl(baseUrl) && isGodotProject(projectPath) && ownsUrl;
    if (result.kind === 'failure' && result.reason === 'connection-refused' && canStart) {
      note('No editor answered; starting a headless editor...');
      const editor = await ensureEditor({
        projectPath,
        url: baseUrl,
        ...(token ? { token } : {}),
        ...(options.editorPath ? { editorPath: options.editorPath } : {}),
        readyTimeoutMs,
        build: options.build !== false,
        onStatus: (message) => {
          verbose(message);
          note(message);
        },
      });
      if (editor.kind === 'failure') {
        spinner?.stop();
        const mapped = ensureFailureExit(editor.reason);
        return output.fail(mapped.exitCode, mapped.code, `Could not start an editor: ${editor.message}`);
      }
      const { ensured, godot } = editor;
      verbose(`Editor ${ensured.source} on ${ensured.ready}`);
      endpoint = `${ensured.ready}${cfg.routePrefix}/${encodeURIComponent(toolName)}`;
      if (spinner) spinner.text = `Calling ${toolName}...`;
      result = await call(ensured.ready);
      if (options.once && ensured.source === 'started') {
        note('Closing the started editor...');
        const closed = await godot.session.closeOwned();
        if (closed.kind === 'closed') {
          if (!spinner) note(`Closed the editor this call started (PID ${closed.pid}).`);
        } else {
          output.warnings.push(`The editor this call started was not closed (${closed.kind}).`);
          note(`WARN: The editor this call started was not closed (${closed.kind}).`);
        }
      }
    }

    if (result.kind === 'success') {
      const toolError = isToolErrorResponse(result.data);
      if (toolError) spinner?.error(`${toolName} returned an error`);
      else spinner?.success(`${toolName} completed`);
      if (options.raw && !output.machine) {
        process.stdout.write(stringifyForRaw(result.data));
      } else if (human) {
        if (!toolError) ui.success('Response:');
        console.log(typeof result.data === 'string' ? result.data : JSON.stringify(result.data, null, 2));
      }
      if (toolError) {
        return output.finish(ExitCode.CommandFailed, result.data ?? null, [{ code: 'TOOL_FAILED', message: toolErrorMessage(result.data, toolName) }]);
      }
      return output.finish(ExitCode.Success, result.data ?? null);
    }

    spinner?.stop();
    handleFailure(result, output, {
      toolName,
      endpoint,
      raw: options.raw,
      timeoutMs,
      errorNoun: cfg.errorNoun,
    });
  });
}

function toolErrorMessage(data: unknown, toolName: string): string {
  const response = data as { content?: Array<{ type?: string; text?: string }>; error?: unknown; message?: unknown };
  const text = (response.content ?? []).filter((c) => c?.type === 'text' && typeof c.text === 'string').map((c) => c.text).join('\n');
  if (text) return text;
  if (typeof response.error === 'string') return response.error;
  if (typeof response.message === 'string') return response.message;
  return `${toolName} returned status "error"`;
}

function normalizeUrl(url: string): string {
  return url.replace(/\/+$/, '').toLowerCase();
}

function stringifyForRaw(data: unknown): string {
  if (typeof data === 'string') return data;
  if (data === undefined) return '';
  return JSON.stringify(data);
}

function handleFailure(failure: RunToolFailure, output: CommandOutput<string>, ctx: FailureContext): never {
  const mapped = toolFailureExit(failure);
  if (failure.reason === 'http-error') {
    const message = `HTTP ${failure.httpStatus}: ${toolErrorText(failure)}`;
    if (output.machine) return output.finish(mapped.exitCode, failure.data ?? null, [{ code: mapped.code, message }]);
    if (ctx.raw) {
      process.stdout.write(stringifyForRaw(failure.data));
    } else {
      ui.error(`HTTP ${failure.httpStatus}: ${failure.message}`);
      if (failure.data !== undefined) {
        ui.info(
          typeof failure.data === 'string' ? failure.data : JSON.stringify(failure.data, null, 2),
        );
      }
    }
    process.exit(mapped.exitCode);
  }

  const message = buildFailureMessage(failure, ctx);
  if (output.machine) return output.finish(mapped.exitCode, null, [{ code: mapped.code, message }]);
  if (ctx.raw) process.stderr.write(message + '\n');
  else ui.error(`Failed to call ${ctx.errorNoun}: ${message}`);
  process.exit(mapped.exitCode);
}

export function buildFailureMessage(failure: RunToolFailure, ctx: FailureContext): string {
  switch (failure.reason) {
    case 'timeout':
      return `${capitalize(ctx.errorNoun)} call timed out after ${ctx.timeoutMs / 1000} seconds: ${ctx.toolName}`;
    case 'connection-refused':
      return `Connection refused at ${ctx.endpoint}. Is the MCP server running? Open Godot with the godot_mcp plugin connected first.`;
    case 'connection-reset':
      return `Connection was reset by the server at ${ctx.endpoint}. The server may have crashed or restarted.`;
    case 'network-error':
      return `Cannot reach ${ctx.endpoint}. Check your network connection and server URL.`;
    default:
      return failure.message;
  }
}

function capitalize(s: string): string {
  return s.length === 0 ? s : s.charAt(0).toUpperCase() + s.slice(1);
}
