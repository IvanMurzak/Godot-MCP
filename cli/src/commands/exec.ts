import * as fs from 'fs';
import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { resolveAndValidateProjectPath, resolveSessionConnection } from '../utils/connection.js';
import { execCode, readToolResponse } from '../lib/exec.js';

interface ExecCliOptions {
  path?: string;
  file?: string;
  using: string[];
  include: string[];
  window?: boolean;
  idle?: string;
  readyTimeout?: string;
  timeout?: string;
  build?: boolean;
  editorPath?: string;
  raw?: boolean;
  url?: string;
  token?: string;
}

/** A positive whole number from a flag, or exit with an error. */
export function parsePositiveIntFlag(raw: string | undefined, flagName: string, fallback: number, allowZero = false): number {
  if (raw === undefined) return fallback;
  const value = Number(raw);
  if (!Number.isInteger(value) || value < 0 || (value === 0 && !allowZero)) {
    ui.error(`${flagName} must be a ${allowZero ? 'non-negative' : 'positive'} whole number, got "${raw}"`);
    process.exit(1);
  }
  return value;
}

function collect(value: string, previous: string[]): string[] {
  return [...previous, value];
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

export const execCommand = new Command('exec')
  .description('Run C# in the Godot editor; starts a headless editor when none is open and keeps it warm')
  .argument('[code]', 'C# snippet: statements, the last expression is the result')
  .option('--path <path>', 'Godot project path (default: current directory)')
  .option('-f, --file <file>', 'Read the code from a file ("-" for stdin)')
  .option('--using <namespace>', 'Extra namespace (repeatable)', collect, [])
  .option('--include <path>', 'Helper .cs file or folder, relative to <project>/.ai-game-dev/snippets (repeatable)', collect, [])
  .option('--window', 'Start the editor with a window instead of headless (only when none is running yet)')
  .option('--idle <seconds>', 'An editor started by exec quits after this many idle seconds (0 = never)', '300')
  .option('--ready-timeout <ms>', 'How long to wait for a started editor to become ready', '300000')
  .option('--timeout <ms>', 'Tool call timeout in milliseconds', '180000')
  .option('--no-build', 'Do not build the C# assembly before starting an editor')
  .option('--editor-path <path>', 'Explicit path to the Godot editor executable')
  .option('--raw', 'Print the raw tool response JSON')
  .option('--url <url>', 'Local MCP server URL (default: the project\'s local port)')
  .option('--token <token>', 'Bearer token (default: GODOT_MCP_TOKEN)')
  .action(async (codeArg: string | undefined, options: ExecCliOptions) => {
    const idleSeconds = parsePositiveIntFlag(options.idle, '--idle', 300, true);
    const readyTimeoutMs = parsePositiveIntFlag(options.readyTimeout, '--ready-timeout', 300_000);
    const timeoutMs = parsePositiveIntFlag(options.timeout, '--timeout', 180_000);
    let code: string;
    try {
      code = readCode(codeArg, options.file);
    } catch (err) {
      ui.error(err instanceof Error ? err.message : String(err));
      process.exit(1);
    }

    const projectPath = resolveAndValidateProjectPath(undefined, { path: options.path });
    const { url, token } = resolveSessionConnection(options);

    const spinner = options.raw ? null : ui.startSpinner('Connecting...');
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
      ...(url ? { url } : {}),
      ...(token ? { token } : {}),
      ...(options.editorPath ? { editorPath: options.editorPath } : {}),
      onStatus: (message) => {
        verbose(message);
        if (spinner) spinner.text = message;
      },
    });

    spinner?.stop();
    if (result.kind === 'failure') {
      if (options.raw && result.toolFailure?.data !== undefined) {
        process.stdout.write(JSON.stringify(result.toolFailure.data));
      } else {
        ui.error(result.message);
      }
      process.exit(1);
    }

    verbose(`Ran on ${result.baseUrl} (editor: ${result.editor})`);
    if (options.raw) {
      process.stdout.write(typeof result.data === 'string' ? result.data : JSON.stringify(result.data));
      return;
    }
    const response = readToolResponse(result.data);
    if (!response.ok) {
      ui.error(response.text);
      process.exit(1);
    }
    console.log(response.text);
  });
