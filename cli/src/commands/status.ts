import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { resolveAndValidateProjectPath, resolveConnection } from '../utils/connection.js';
import { findGodotProcess } from '../utils/godot-process.js';
import { probe } from '../utils/probe.js';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag } from '../utils/output.js';

interface StatusOptions {
  path?: string;
  url?: string;
  token?: string;
  timeout?: string;
  format?: string;
}

export const statusCommand = addFormatOption(
  new Command('status')
    .description('Check Godot editor and MCP server connection status')
    .argument('[path]', 'Godot project path (used to detect a running editor and validate project.godot)')
    .option('--path <path>', 'Godot project path')
    .option('--url <url>', 'MCP server base URL (defaults to GODOT_MCP_HOST/cloud)')
    .option('--token <token>', 'Bearer token override (defaults to GODOT_MCP_TOKEN)')
    .option('--timeout <ms>', 'Probe timeout in milliseconds (default: 5000)', '5000'),
).action(async (positionalPath: string | undefined, options: StatusOptions) => {
  const output = new CommandOutput('status', parseFormat(options.format));
  const timeoutMs = parseIntFlag(options.timeout, '--timeout');
  const projectPath = resolveAndValidateProjectPath(positionalPath, options);
  const { url: serverUrl, token } = await resolveConnection(projectPath, options);

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  ui.heading('Godot-MCP Status');
  ui.label('Project', projectPath);
  ui.divider();

  // 1. Godot process detection
  ui.heading('Godot Editor Process');
  const proc = findGodotProcess(projectPath);
  if (proc) {
    ui.success(`Godot is running (PID: ${proc.pid})`);
  } else {
    ui.warn('Godot is not running with this project');
  }

  // 2. MCP server check
  ui.heading('MCP Server');
  ui.label('URL', serverUrl);

  const spinner = ui.startSpinner(`Probing ${serverUrl}...`);
  const result = await probe(serverUrl, headers, timeoutMs);
  if (result.ok) {
    spinner.success('Connected');
    verbose(`Server response: ${JSON.stringify(result.data)}`);
  } else {
    spinner.error(`Not available (${result.reason})`);
  }

  ui.divider();

  const data = {
    projectPath,
    editor: { running: proc !== null, pid: proc?.pid ?? null },
    server: { url: serverUrl, reachable: result.ok, ...(result.ok ? {} : { reason: result.reason }) },
  };
  if (result.ok) {
    ui.success('MCP server is reachable — ready for tool calls');
    output.finish(ExitCode.Success, data);
  }
  const message = proc
    ? 'Godot is running but MCP server is not responding yet'
    : 'Godot is not running and MCP server is not reachable';
  if (proc) ui.warn(message);
  else ui.error(message);
  output.finish(ExitCode.Unreachable, data, [{ code: 'UNREACHABLE', message }]);
});
