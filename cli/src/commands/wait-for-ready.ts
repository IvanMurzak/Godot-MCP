import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { resolveAndValidateProjectPath } from '../utils/connection.js';
import { connectionForCommand } from '../utils/editor-connection.js';
import { probe } from '../utils/probe.js';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag } from '../utils/output.js';

const MAX_PROBE_TIMEOUT_MS = 10_000;

interface WaitForReadyOptions {
  path?: string;
  url?: string;
  token?: string;
  timeout?: string;
  interval?: string;
  format?: string;
}

export const waitForReadyCommand = addFormatOption(
  new Command('wait-for-ready')
    .description('Wait until the Godot editor and MCP server are ready to accept tool calls')
    .argument('[path]', 'Godot project path (validated when --url is omitted)')
    .option('--path <path>', 'Godot project path')
    .option('--url <url>', 'MCP server base URL (defaults to GODOT_MCP_HOST/cloud)')
    .option('--token <token>', 'Bearer token override (defaults to GODOT_MCP_TOKEN)')
    .option('--timeout <ms>', 'Maximum time to wait in milliseconds (default: 120000)', '120000')
    .option('--interval <ms>', 'Polling interval in milliseconds (default: 3000)', '3000'),
).action(async (positionalPath: string | undefined, options: WaitForReadyOptions) => {
  const output = new CommandOutput('wait-for-ready', parseFormat(options.format));
  const timeoutMs = parseIntFlag(options.timeout, '--timeout');
  const intervalMs = parseIntFlag(options.interval, '--interval');
  const projectPath = resolveAndValidateProjectPath(positionalPath, options);
  // The server the project's editor serves: its mode decides (Cloud with the sign-in; never signed in: the local one).
  const conn = await connectionForCommand(output, projectPath, options);
  const { url: serverUrl, token } = conn;

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  verbose(`Probe target: ${serverUrl}`);
  verbose(`Timeout: ${timeoutMs}ms, Interval: ${intervalMs}ms`);

  const spinner = ui.startSpinner('Waiting for Godot editor and MCP server...');
  const startTime = Date.now();

  while (true) {
    const elapsed = Date.now() - startTime;
    if (elapsed >= timeoutMs) {
      const message = `Timed out after ${(timeoutMs / 1000).toFixed(1)}s waiting for MCP server`;
      spinner.error(message);
      output.finish(ExitCode.Timeout, { url: serverUrl, ready: false, elapsedMs: elapsed }, [{ code: 'TIMEOUT', message }]);
    }

    const remaining = Math.ceil((timeoutMs - elapsed) / 1000);
    spinner.text = `Waiting for Godot editor and MCP server... (${remaining}s remaining)`;

    const probeTimeout = Math.min(intervalMs, MAX_PROBE_TIMEOUT_MS);
    const result = await probe(serverUrl, headers, probeTimeout);

    if (result.ok) {
      const elapsedMs = Date.now() - startTime;
      spinner.success(`MCP server is ready at ${result.baseUrl} (connected in ${(elapsedMs / 1000).toFixed(1)}s)`);
      output.finish(ExitCode.Success, { url: result.baseUrl, ready: true, elapsedMs });
    }

    const remainingMs = timeoutMs - (Date.now() - startTime);
    if (remainingMs <= 0) continue;
    await new Promise((resolve) => setTimeout(resolve, Math.min(intervalMs, remainingMs)));
  }
});
