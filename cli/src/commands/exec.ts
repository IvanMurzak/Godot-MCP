import * as fs from 'fs';
import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { DEFAULT_SESSION_IDLE_SECONDS, DEFAULT_SESSION_READY_TIMEOUT_MS } from '@baizor/gamedev-cli-core';
import { resolveAndValidateProjectPath, resolveSessionConnection } from '../utils/connection.js';
import { collect } from '../utils/input.js';
import { ExitCode } from '../utils/exit-codes.js';
import { connectionForCommand } from '../utils/editor-connection.js';
import { isLocalhostUrl } from '../utils/project-marker.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag } from '../utils/output.js';
import { DEFAULT_EXEC_TIMEOUT_MS, execCode, readToolResponse } from '../lib/exec.js';
import { classifyToolFailure } from '@baizor/gamedev-cli-core';
import { buildFailureMessage, ensureFailureExit } from './run-tool-builder.js';

interface ExecCliOptions {
  path?: string;
  file?: string;
  using: string[];
  include: string[];
  window?: boolean;
  idle: string;
  readyTimeout: string;
  timeout: string;
  build?: boolean;
  editorPath?: string;
  raw?: boolean;
  url?: string;
  token?: string;
  format?: string;
}

function readCode(code: string | undefined, file: string | undefined): string {
  if (code !== undefined && file !== undefined) throw new Error('Pass the code either as an argument or with --file, not both.');
  if (code !== undefined) return code;
  const fromStdin = file === '-' || (file === undefined && !process.stdin.isTTY);
  if (!fromStdin && file === undefined) throw new Error('No code given: pass it as an argument, with --file <file>, or on stdin.');
  const text = fs.readFileSync(fromStdin ? 0 : file!, 'utf-8');
  if (text.trim() === '') throw new Error(fromStdin ? 'No code received from stdin.' : `The file is empty: ${file}`);
  return text;
}

export const execCommand = addFormatOption(new Command('exec')
  .description('Run C# in the Godot editor; starts a headless editor when none is open and keeps it warm (Cloud mode with a sign-in: runs in the editor connected to ai-game.dev, starting none)')
  .argument('[code]', 'C# snippet: statements, the last expression is the result')
  .option('--path <path>', 'Godot project path (default: current directory)')
  .option('-f, --file <file>', 'Read the code from a file ("-" for stdin)')
  .option('--using <namespace>', 'Extra namespace (repeatable)', collect, [])
  .option('--include <path>', 'Helper .cs file or folder, relative to <project>/.ai-game-dev/snippets (repeatable)', collect, [])
  .option('--window', 'Start the editor with a window instead of headless (only when none is running yet)')
  .option('--idle <seconds>', 'An editor started by exec quits after this many idle seconds (0 = never)', String(DEFAULT_SESSION_IDLE_SECONDS))
  .option('--ready-timeout <ms>', 'How long to wait for a started editor to become ready', String(DEFAULT_SESSION_READY_TIMEOUT_MS))
  .option('--timeout <ms>', 'Tool call timeout in milliseconds', String(DEFAULT_EXEC_TIMEOUT_MS))
  .option('--no-build', 'Do not build the C# assembly before starting an editor')
  .option('--editor-path <path>', 'Explicit path to the Godot editor executable')
  .option('--raw', 'Print the raw tool response JSON')
  .option('--url <url>', 'Local MCP server URL (default: the project\'s local port)')
  .option('--token <token>', 'Bearer token (default: GODOT_MCP_TOKEN)'))
  .action(async (codeArg: string | undefined, options: ExecCliOptions) => {
    const output = new CommandOutput('exec', parseFormat(options.format));
    const idleSeconds = parseIntFlag(options.idle, '--idle', true);
    const readyTimeoutMs = parseIntFlag(options.readyTimeout, '--ready-timeout');
    const timeoutMs = parseIntFlag(options.timeout, '--timeout');
    let code: string;
    try {
      code = readCode(codeArg, options.file);
    } catch (err) {
      return output.fail(ExitCode.Usage, 'USAGE', err instanceof Error ? err.message : String(err));
    }

    const projectPath = resolveAndValidateProjectPath(undefined, { path: options.path });
    // Cloud mode: through ai-game.dev with the sign-in (no editor is started); never signed in: the local server.
    const conn = await connectionForCommand(output, projectPath, { ...(options.url ? { url: options.url } : {}), ...(options.token ? { token: options.token } : {}) });
    const spinner = options.raw || output.machine ? null : ui.startSpinner('Connecting...');
    const result = await execCode({
      projectPath,
      code,
      usings: options.using,
      include: options.include,
      window: options.window === true,
      build: options.build !== false,
      idleSeconds,
      readyTimeoutMs,
      timeoutMs,
      ...resolveSessionConnection(options),
      // The cloud, or a non-local --url / GODOT_MCP_HOST: talk to that server and start nothing (a session is local only).
      ...(conn.cloud || !isLocalhostUrl(conn.url) ? { remoteUrl: conn.url, token: conn.token } : {}),
      ...(conn.localFallback ? { url: conn.url } : {}),
      ...(options.editorPath ? { editorPath: options.editorPath } : {}),
      onStatus: (message) => {
        verbose(message);
        if (spinner) spinner.text = message;
        else if (output.machine) output.progress(message);
      },
    });

    spinner?.stop();
    if (result.kind === 'failure') {
      const mapped = result.toolFailure ? classifyToolFailure(result.toolFailure) : ensureFailureExit({ reason: result.reason as Exclude<typeof result.reason, 'tool-failed'>, sessionReason: result.sessionReason });
      // Timeout, refused, reset: the same hints as run-tool.
      const message = result.toolFailure && result.toolFailure.reason !== 'http-error'
        ? buildFailureMessage(result.toolFailure, { toolName: 'script-execute', endpoint: result.toolFailure.endpoint, raw: false, timeoutMs, errorNoun: 'tool' })
        : result.message;
      if (output.machine) output.finish(mapped.exitCode, result.toolFailure?.data ?? null, [{ code: mapped.code, message, httpStatus: mapped.httpStatus }]);
      if (options.raw && result.toolFailure?.data !== undefined) process.stdout.write(JSON.stringify(result.toolFailure.data));
      else ui.error(message);
      process.exit(mapped.exitCode);
    }

    verbose(`Ran on ${result.baseUrl} (editor: ${result.editor})`);
    const response = readToolResponse(result.data);
    if (output.machine) {
      const data = { editor: result.editor, url: result.baseUrl, output: response.text, response: result.data };
      if (!response.ok) output.finish(ExitCode.CommandFailed, data, [{ code: 'TOOL_FAILED', message: response.text }]);
      output.finish(ExitCode.Success, data);
    }
    if (options.raw) {
      process.stdout.write(typeof result.data === 'string' ? result.data : JSON.stringify(result.data));
      process.exit(response.ok ? ExitCode.Success : ExitCode.CommandFailed);
    }
    if (!response.ok) {
      ui.error(response.text);
      process.exit(ExitCode.CommandFailed);
    }
    console.log(response.text);
  });
