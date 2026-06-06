import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { resolveAndValidateProjectPath, resolveConnection } from '../utils/connection.js';
import { findGodotProcess } from '../utils/godot-process.js';
import { probe } from '../utils/probe.js';

interface StatusOptions {
  path?: string;
  url?: string;
  token?: string;
  timeout?: string;
}

export const statusCommand = new Command('status')
  .description('Check Godot editor and MCP server connection status')
  .argument('[path]', 'Godot project path (used to detect a running editor and validate project.godot)')
  .option('--path <path>', 'Godot project path')
  .option('--url <url>', 'MCP server base URL (defaults to GODOT_MCP_HOST/cloud)')
  .option('--token <token>', 'Bearer token override (defaults to GODOT_MCP_TOKEN)')
  .option('--timeout <ms>', 'Probe timeout in milliseconds (default: 5000)', '5000')
  .action(async (positionalPath: string | undefined, options: StatusOptions) => {
    const projectPath = resolveAndValidateProjectPath(positionalPath, options);
    const { url: serverUrl, token } = resolveConnection(projectPath, options);

    const timeoutMs = parseInt(options.timeout ?? '5000', 10);
    if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
      ui.error(`Invalid --timeout value: "${options.timeout}". Must be a positive integer (milliseconds).`);
      process.exit(1);
    }

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

    if (result.ok) {
      ui.success('MCP server is reachable — ready for tool calls');
      process.exit(0);
    } else if (proc) {
      ui.warn('Godot is running but MCP server is not responding yet');
      process.exit(1);
    } else {
      ui.error('Godot is not running and MCP server is not reachable');
      process.exit(1);
    }
  });
