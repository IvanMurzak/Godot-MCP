import { Command } from 'commander';
import {
  DEFAULT_SESSION_READY_TIMEOUT_MS,
  classifyEditorStartFailure,
  classifyToolFailure,
  type ToolFailureClass,
} from '@baizor/gamedev-cli-core';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { isGodotProject, localProjectUrl, resolveAndValidateProjectPath } from '../utils/connection.js';
import { connectionForCommand } from '../utils/editor-connection.js';
import { parseInput } from '../utils/input.js';
import { isLocalhostUrl } from '../utils/project-marker.js';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag } from '../utils/output.js';
import { ensureEditor, type EnsureEditorResult } from '../lib/session.js';
import { toolErrorText, toolResponseText, withoutImageData } from '../lib/exec.js';
import type { RunToolFailure, RunToolOptions, RunToolResult } from '../lib/types.js';
import { rqlErrorHint } from '../lib/rql.js';

export interface BuilderOptions {
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
  /**
   * Lower-case noun used in user-facing failure copy:
   * `Failed to call <noun>:` and `<Capitalized> call timed out…`.
   */
  errorNoun: string;
  /** Library function — `runTool` or `runSystemTool`. */
  invoke: (opts: RunToolOptions) => Promise<RunToolResult>;
}

/** What `runToolAction` needs of a command: everything but its help texts. */
export type RunToolCall = Omit<BuilderOptions, 'description' | 'argDescription'>;

export interface RunToolCliOptions {
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

/**
 * Exit code and envelope error code of a failed tool call: the shared table of all three engine CLIs
 * (`classifyToolFailure` in @baizor/gamedev-cli-core, keyed on the server's HTTP status).
 */
export function toolFailureExit(failure: RunToolFailure): ToolFailureClass {
  return classifyToolFailure(failure);
}

/** Exit code and envelope error code when no editor could be obtained (the shared table). */
export function ensureFailureExit(reason: Extract<EnsureEditorResult, { kind: 'failure' }>['reason']): ToolFailureClass {
  return classifyEditorStartFailure({ buildFailed: reason === 'build-failed', invalidInput: reason === 'invalid-options' });
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
  ).action((toolName: string, positionalPath: string | undefined, options: RunToolCliOptions) =>
    runToolAction(cfg, toolName, positionalPath, options));
}

/** Call one tool and print its answer (`run-tool`, and `rql` with the RQL tool it picks); a failure exits. */
export async function runToolAction(
  cfg: RunToolCall,
  toolName: string,
  positionalPath: string | undefined,
  options: RunToolCliOptions,
  /** After a successful call printed its answer: when the call went out, and whether the editor runs on this machine. */
  onSuccess?: (call: { calledAt: number; local: boolean }) => void,
): Promise<void> {
  const output = new CommandOutput(cfg.name, parseFormat(options.format));
  const human = !options.raw && !output.machine;
  // Validate flags first so a bad value short-circuits before we read --input-file or hit the network.
  const timeoutMs = parseIntFlag(options.timeout, '--timeout');
  const readyTimeoutMs = parseIntFlag(options.readyTimeout, '--ready-timeout');

  // Resolve path + connection up front so the heading and verbose output
  // reflect the final endpoint before the HTTP call fires.
  const projectPath = resolveAndValidateProjectPath(positionalPath, options);
  // Cloud mode: the hub with the sign-in; never signed in: the local server (warning); unusable sign-in: refuse.
  const conn = await connectionForCommand(output, projectPath, options);
  const { url: baseUrl, token } = conn;
  const body = parseInput(options);
  let endpoint = `${baseUrl}${cfg.routePrefix}/${encodeURIComponent(toolName)}`;
  const authSource = options.token ? '--token flag' : 'config/env';

  verbose(`${cfg.verboseLabel}: ${toolName}`);
  verbose(`Endpoint: ${endpoint}`);
  verbose(`Body: ${body}`);
  if (token) verbose(`Authorization header set (source: ${authSource})`);

  // Text mode prints the answer alone (agents re-read every line on each later turn); --verbose adds the endpoint.
  const spinner = human && process.stdout.isTTY ? ui.startSpinner(`Calling ${toolName}...`) : null;
  let calledAt = Date.now();
  let calledUrl = baseUrl;
  const call = (url: string) => {
    calledAt = Date.now();
    calledUrl = url;
    return cfg.invoke({ toolName, url, ...(token ? { token } : {}), input: body, timeoutMs });
  };
  let result = await call(baseUrl);
  // Status lines: the spinner (a terminal), the progress stream (json/ndjson), else stderr: stdout is the answer alone.
  const note = (message: string): void => {
    if (spinner) spinner.text = message;
    else if (!output.machine) process.stderr.write(message + '\n');
    else output.progress(message);
  };

  // No editor answers on the project's local server: start a headless one (exec's policy: a running editor is
  // reused and never closed), then retry.
  // With an explicit --url, start only the editor of a project that was named, or whose own local server that is:
  // never one for whatever Godot project the current directory happens to be.
  const explicitProject = positionalPath !== undefined || options.path !== undefined;
  const ownsUrl = !options.url || explicitProject || normalizeUrl(options.url) === normalizeUrl(localProjectUrl(projectPath));
  // Never for a Cloud connection (even a dev hub on localhost): a signed-in machine is never switched to a local server.
  const canStart = options.start !== false && !conn.cloud && isLocalhostUrl(baseUrl) && isGodotProject(projectPath) && ownsUrl;
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
    spinner?.stop();
    if (options.raw && !output.machine) {
      process.stdout.write(stringifyForRaw(result.data));
    } else if (human) {
      if (toolError) ui.error(withRqlHint(toolName, `${toolName} returned an error: ${toolErrorMessage(result.data, toolName)}`));
      else console.log(toolResponseText(result.data)
        ?? (typeof result.data === 'string' ? result.data : JSON.stringify(withoutImageData(result.data), null, 2)));
    }
    if (toolError) {
      return output.finish(ExitCode.CommandFailed, result.data ?? null, [{ code: 'TOOL_FAILED', message: toolErrorMessage(result.data, toolName) }]);
    }
    onSuccess?.({ calledAt, local: !conn.cloud && isLocalhostUrl(calledUrl) });
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
    const text = toolErrorText(failure);
    const rqlHint = rqlErrorHint(ctx.toolName, text);
    const hint = rqlHint ? `hint: ${rqlHint}` : httpFailureHint(failure.httpStatus, ctx.toolName);
    const message = `${text}${/^HTTP \d/.test(text) ? '' : ` (HTTP ${failure.httpStatus})`}${hint ? ` ${hint}` : ''}`;
    if (output.machine) return output.finish(mapped.exitCode, failure.data ?? null, [{ code: mapped.code, message, httpStatus: mapped.httpStatus }]);
    if (ctx.raw) {
      process.stdout.write(stringifyForRaw(failure.data));
    } else {
      // The server's own error text, and what to do next; the raw response only with --verbose.
      ui.error(message);
      verbose(`Response: ${typeof failure.data === 'string' ? failure.data : JSON.stringify(failure.data)}`);
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

/**
 * The next step after an HTTP failure: list the tools on a 404; on 400 read the tool's arguments (422 is the tool's
 * own failure, e.g. a missing file: its text says what is wrong).
 */
export function httpFailureHint(httpStatus: number | undefined, toolName: string): string | undefined {
  if (httpStatus === 404) return 'Run `godot-cli tools` to list the tools this editor has.';
  if (httpStatus === 400) return `Run \`godot-cli tools ${toolName}\` for its arguments.`;
  return undefined;
}

function capitalize(s: string): string {
  return s.length === 0 ? s : s.charAt(0).toUpperCase() + s.slice(1);
}

/** `message`, with a hint for a familiar slip when an RQL tool failed. */
function withRqlHint(toolName: string, message: string): string {
  const hint = rqlErrorHint(toolName, message);
  return hint ? `${message}\nhint: ${hint}` : message;
}
