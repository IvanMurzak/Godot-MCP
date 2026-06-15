import { describe, it, expect } from 'vitest';
import { spawn } from 'child_process';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { runCliAsync } from './helpers/cli.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CLI_PATH = path.resolve(__dirname, '..', 'bin', 'godot-cli.js');

/**
 * Spawn the CLI with a fully-explicit environment. We do NOT spread the parent
 * env: vitest's worker pool can hand us a sanitized/proxied `process.env` whose
 * `npm_*` keys do not propagate predictably to a child, which would mask the
 * npx-detection branch under test. Building the child env from scratch (plus a
 * minimal PATH so `node` resolves) keeps these assertions hermetic.
 */
function runCliExplicitEnv(
  args: string[],
  env: Record<string, string>,
): Promise<{ stdout: string; exitCode: number }> {
  const childEnv: Record<string, string> = {
    PATH: process.env['PATH'] ?? process.env['Path'] ?? '',
    ...env,
  };
  // Windows resolves executables via Path (capitalized); mirror it.
  if (process.platform === 'win32') childEnv['Path'] = childEnv['PATH'];

  return new Promise((resolve) => {
    const child = spawn(process.execPath, [CLI_PATH, ...args], {
      stdio: 'pipe',
      env: childEnv,
    });
    let stdout = '';
    const timeout = setTimeout(() => {
      try { child.kill(); } catch { /* noop */ }
      resolve({ stdout: stdout + '\n[timeout]\n', exitCode: 1 });
    }, 20000);
    child.stdout?.on('data', (d: Buffer) => (stdout += d.toString()));
    child.stderr?.on('data', (d: Buffer) => (stdout += d.toString()));
    child.on('close', (code) => {
      clearTimeout(timeout);
      resolve({ stdout, exitCode: code ?? 0 });
    });
  });
}

describe('update — CLI smoke', () => {
  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['update', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--check');
    expect(stdout).toContain('latest version');
  });

  it('short-circuits with a no-op when running via npx (npm_command=exec)', async () => {
    const { stdout, exitCode } = await runCliExplicitEnv(['update'], { npm_command: 'exec' });
    expect(exitCode).toBe(0);
    expect(stdout).toContain('npx');
    expect(stdout).toContain('No update action needed');
  });

  it('short-circuits with a no-op when npm_execpath references npx', async () => {
    const { stdout, exitCode } = await runCliExplicitEnv(['update', '--check'], {
      npm_execpath: '/usr/lib/node_modules/npm/bin/npx-cli.js',
    });
    expect(exitCode).toBe(0);
    expect(stdout).toContain('npx');
  });
});
