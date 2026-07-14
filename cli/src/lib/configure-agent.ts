import * as fs from 'fs';
import * as path from 'path';
import { spawn } from 'child_process';
import { getManagedServerExecutablePath } from './install-server.js';
import { emitProgress } from './progress.js';
import type { ConfigureAgentOptions, ConfigureAgentResult } from './types.js';

/**
 * Proxy `configure --agent <id>` to the CLI-managed `gamedev-mcp-server` binary's
 * own `configure` subcommand (design 06 / 09 workflow 1B, step 2). The downloaded
 * server binary — placed by `install-plugin --with-server` in the CLI's managed
 * dir, NOT on PATH — owns the shared C# configurator registry and writes the
 * agent's MCP-client config (with the derived `port=` + `project=` pin). The CLI
 * merely locates the managed binary and spawns:
 *
 *   <managed-server> configure --agent <id> [--url <url>]     (cwd = project root)
 *
 * forwarding the agent id + optional URL and the binary's exit code. cwd is the
 * project root so the server derives this project's pin + port. When no managed
 * binary exists (the user never ran `--with-server`), a clean failure tells them
 * to run it first — NOT a cryptic ENOENT.
 *
 * Library-safe: never throws past the boundary; captures the server's output and
 * returns it (the command prints it). `spawnImpl` is injectable for tests.
 */
export async function configureAgentViaServer(opts: ConfigureAgentOptions): Promise<ConfigureAgentResult> {
  const warnings: string[] = [];

  try {
    const projectPath = path.resolve(opts.godotProjectPath);
    if (!fs.existsSync(projectPath)) {
      throw new Error(`Project path does not exist: ${projectPath}`);
    }
    const agentId = (opts.agentId ?? '').trim();
    if (agentId.length === 0) {
      throw new Error('An agent id is required (configure --agent <id>).');
    }

    const serverBinaryPath = getManagedServerExecutablePath(projectPath);
    if (!fs.existsSync(serverBinaryPath)) {
      throw new Error(
        `No managed server binary found at ${serverBinaryPath}. ` +
          `Run \`godot-cli install-plugin --with-server\` first to download it.`,
      );
    }

    const args = ['configure', '--agent', agentId];
    if (opts.url && opts.url.trim().length > 0) {
      args.push('--url', opts.url.trim());
    }

    emitProgress(opts.onProgress, {
      phase: 'start',
      message: `Proxying to ${serverBinaryPath} ${args.join(' ')}`,
    });

    const run = await runServerConfigure(serverBinaryPath, args, projectPath, opts.spawnImpl);

    if (run.code !== 0) {
      const detail = run.output.trim();
      const err = new Error(
        `The managed server \`configure --agent ${agentId}\` exited with code ${run.code ?? 'null'}.` +
          (detail.length > 0 ? `\n${detail}` : ''),
      );
      return { kind: 'failure', success: false, exitCode: run.code ?? undefined, warnings, error: err };
    }

    emitProgress(opts.onProgress, { phase: 'done', message: `Configured ${agentId} via the managed server.` });

    return {
      kind: 'success',
      success: true,
      agentId,
      serverBinaryPath,
      args,
      exitCode: 0,
      output: run.output,
      warnings,
    };
  } catch (err: unknown) {
    return {
      kind: 'failure',
      success: false,
      warnings,
      error: err instanceof Error ? err : new Error(String(err)),
    };
  }
}

interface ServerConfigureRun {
  code: number | null;
  output: string;
}

/**
 * Spawn the managed server's `configure` subcommand, capturing stdout+stderr, and
 * resolve once it exits. A spawn `error` (e.g. the binary is not executable)
 * resolves as a non-zero run rather than throwing past the boundary. `spawnImpl`
 * is injectable for unit tests.
 */
function runServerConfigure(
  bin: string,
  args: string[],
  cwd: string,
  spawnImpl: typeof spawn = spawn,
): Promise<ServerConfigureRun> {
  return new Promise((resolve) => {
    let settled = false;
    const finish = (run: ServerConfigureRun): void => {
      if (settled) return;
      settled = true;
      resolve(run);
    };

    let child: import('child_process').ChildProcess;
    try {
      child = spawnImpl(bin, args, { cwd, stdio: ['ignore', 'pipe', 'pipe'] });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      finish({ code: 127, output: `Failed to spawn "${bin}": ${msg}` });
      return;
    }

    let output = '';
    child.stdout?.on('data', (chunk: Buffer | string) => {
      output += chunk.toString();
    });
    child.stderr?.on('data', (chunk: Buffer | string) => {
      output += chunk.toString();
    });
    child.on('error', (err: Error) => {
      finish({ code: 127, output: `${output}\nFailed to run "${bin}": ${err.message}` });
    });
    child.on('close', (code: number | null) => {
      finish({ code, output });
    });
  });
}
