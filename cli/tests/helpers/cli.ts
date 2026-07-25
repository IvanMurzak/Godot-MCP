// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

import * as path from 'path';
import { spawn } from 'child_process';
import { fileURLToPath } from 'url';
import { CLI_SPAWN_TIMEOUT_MS } from './timeouts.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const CLI_PATH = path.resolve(__dirname, '..', '..', 'bin', 'godot-cli.js');

/**
 * Run the CLI as a child process with timeout and error handling.
 *
 * The internal budget is {@link CLI_SPAWN_TIMEOUT_MS}, deliberately kept BELOW the
 * suite's per-test timeout so a stalled child is reported by this guard (with its
 * captured output) instead of by an opaque `Test timed out` from the runner. It used
 * to be 30 s against a 5 s test timeout, which made it unreachable — see
 * `tests/helpers/timeouts.ts`.
 */
export function runCliAsync(args: string[], cwd?: string): Promise<{ stdout: string; exitCode: number }> {
  return new Promise((resolve) => {
    const child = spawn('node', [CLI_PATH, ...args], { stdio: 'pipe', cwd });
    let stdout = '';
    let settled = false;
    const timeoutMs = CLI_SPAWN_TIMEOUT_MS;

    const timeout = setTimeout(() => {
      if (settled) return;
      settled = true;
      try { child.kill(); } catch { /* noop */ }
      stdout += '\n[runCliAsync] Process timed out.\n';
      resolve({ stdout, exitCode: 1 });
    }, timeoutMs);

    const finish = (exitCode: number) => {
      if (settled) return;
      settled = true;
      clearTimeout(timeout);
      resolve({ stdout, exitCode });
    };

    child.stdout?.on('data', (d: Buffer) => { stdout += d.toString(); });
    child.stderr?.on('data', (d: Buffer) => { stdout += d.toString(); });
    child.on('close', (code) => { finish(code ?? 0); });
    child.on('error', (err) => {
      stdout += `\n[runCliAsync] Error: ${String(err)}\n`;
      finish(1);
    });
  });
}
