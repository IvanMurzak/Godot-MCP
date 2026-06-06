import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { resolveAndValidateProjectPath, resolveConnection } from '../utils/connection.js';
import { probe } from '../utils/probe.js';

const MAX_PROBE_TIMEOUT_MS = 10_000;

interface WaitForReadyOptions {
  path?: string;
  url?: string;
  token?: string;
  timeout?: string;
  interval?: string;
}

export const waitForReadyCommand = new Command('wait-for-ready')
  .description('Wait until the Godot editor and MCP server are ready to accept tool calls')
  .argument('[path]', 'Godot project path (validated when --url is omitted)')
  .option('--path <path>', 'Godot project path')
  .option('--url <url>', 'MCP server base URL (defaults to GODOT_MCP_HOST/cloud)')
  .option('--token <token>', 'Bearer token override (defaults to GODOT_MCP_TOKEN)')
  .option('--timeout <ms>', 'Maximum time to wait in milliseconds (default: 120000)', '120000')
  .option('--interval <ms>', 'Polling interval in milliseconds (default: 3000)', '3000')
  .action(async (positionalPath: string | undefined, options: WaitForReadyOptions) => {
    const projectPath = resolveAndValidateProjectPath(positionalPath, options);
    const { url: serverUrl, token } = resolveConnection(projectPath, options);

    const timeoutMs = parseInt(options.timeout ?? '120000', 10);
    const intervalMs = parseInt(options.interval ?? '3000', 10);

    if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
      ui.error(`Invalid --timeout value: "${options.timeout}". Must be a positive integer (milliseconds).`);
      process.exit(1);
    }
    if (!Number.isFinite(intervalMs) || intervalMs <= 0) {
      ui.error(`Invalid --interval value: "${options.interval}". Must be a positive integer (milliseconds).`);
      process.exit(1);
    }

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
        spinner.error(`Timed out after ${(timeoutMs / 1000).toFixed(1)}s waiting for MCP server`);
        process.exit(1);
      }

      const remaining = Math.ceil((timeoutMs - elapsed) / 1000);
      spinner.text = `Waiting for Godot editor and MCP server... (${remaining}s remaining)`;

      const probeTimeout = Math.min(intervalMs, MAX_PROBE_TIMEOUT_MS);
      const result = await probe(serverUrl, headers, probeTimeout);

      if (result.ok) {
        const totalSeconds = ((Date.now() - startTime) / 1000).toFixed(1);
        spinner.success(`MCP server is ready at ${result.baseUrl} (connected in ${totalSeconds}s)`);
        process.exit(0);
      }

      const remainingMs = timeoutMs - (Date.now() - startTime);
      if (remainingMs <= 0) continue;
      await new Promise((resolve) => setTimeout(resolve, Math.min(intervalMs, remainingMs)));
    }
  });
