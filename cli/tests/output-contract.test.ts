import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import http from 'http';
import { spawn } from 'child_process';
import { CLI_PATH } from './helpers/cli.js';
import { CLI_SPAWN_TIMEOUT_MS } from './helpers/timeouts.js';
import { ExitCode, EXIT_CODE_TABLE } from '../src/utils/exit-codes.js';
import { formatFromArgv } from '../src/utils/output.js';
import { isToolErrorResponse, toolFailureExit } from '../src/commands/run-tool-builder.js';

/** Run the CLI keeping stdout and stderr apart (the machine formats own stdout). */
function runCli(args: string[], cwd?: string): Promise<{ stdout: string; stderr: string; exitCode: number }> {
  return new Promise((resolve) => {
    const child = spawn('node', [CLI_PATH, ...args], { stdio: 'pipe', cwd });
    let stdout = '';
    let stderr = '';
    const timer = setTimeout(() => child.kill(), CLI_SPAWN_TIMEOUT_MS);
    child.stdout.on('data', (d: Buffer) => (stdout += d.toString()));
    child.stderr.on('data', (d: Buffer) => (stderr += d.toString()));
    child.on('close', (code) => {
      clearTimeout(timer);
      resolve({ stdout, stderr, exitCode: code ?? 1 });
    });
  });
}

function startServer(handler: (res: http.ServerResponse) => void): Promise<{ url: string; close: () => Promise<void> }> {
  return new Promise((resolve) => {
    const server = http.createServer((req, res) => {
      req.on('data', () => undefined);
      req.on('end', () => handler(res));
    });
    server.listen(0, '127.0.0.1', () => {
      const addr = server.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      resolve({ url: `http://127.0.0.1:${port}`, close: () => new Promise<void>((r) => server.close(() => r())) });
    });
  });
}

function deadPort(): Promise<number> {
  return new Promise((resolve) => {
    const server = http.createServer();
    server.listen(0, '127.0.0.1', () => {
      const addr = server.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      server.close(() => resolve(port));
    });
  });
}

describe('exit codes', () => {
  it('match the shared contract', () => {
    expect(ExitCode).toEqual({ Success: 0, Error: 1, Usage: 2, CommandFailed: 6, Unreachable: 7, TestsFailed: 8, Timeout: 14, Busy: 17, AwaitingInput: 18 });
    expect(EXIT_CODE_TABLE.map((e) => e.code)).toEqual([0, 1, 2, 6, 7, 8, 14, 17, 18]);
  });

  it('map tool-call failures', () => {
    const f = (reason: string, httpStatus?: number) => toolFailureExit({ kind: 'failure', success: false, endpoint: '', reason: reason as never, message: '', ...(httpStatus ? { httpStatus } : {}) });
    expect(f('timeout')).toEqual({ exitCode: 14, code: 'TIMEOUT' });
    expect(f('connection-refused')).toEqual({ exitCode: 7, code: 'UNREACHABLE' });
    expect(f('http-error', 503)).toEqual({ exitCode: 17, code: 'BUSY' });
    expect(f('http-error', 422)).toEqual({ exitCode: 6, code: 'TOOL_FAILED' });
    expect(f('invalid-input')).toEqual({ exitCode: 2, code: 'USAGE' });
    expect(f('unknown')).toEqual({ exitCode: 1, code: 'ERROR' });
    expect(isToolErrorResponse({ status: 'error' })).toBe(true);
    expect(isToolErrorResponse({ status: 'success' })).toBe(false);
    expect(isToolErrorResponse('text')).toBe(false);
  });

  it('formatFromArgv reads both spellings', () => {
    expect(formatFromArgv(['status', '--format', 'json'])).toBe('json');
    expect(formatFromArgv(['status', '--format=ndjson'])).toBe('ndjson');
    expect(formatFromArgv(['status'])).toBeUndefined();
  });
});

describe('CLI output contract', () => {
  let tmp: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-contract-'));
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  it('commands --format json prints one envelope with every command and the exit-code table', async () => {
    const { stdout, exitCode } = await runCli(['commands', '--format', 'json']);
    expect(exitCode).toBe(0);
    const envelope = JSON.parse(stdout);
    expect(envelope).toMatchObject({ success: true, command: 'commands', errors: [], warnings: [] });
    const names = envelope.data.commands.map((c: { name: string }) => c.name);
    for (const name of ['check', 'commands', 'exec', 'export', 'import', 'run-script', 'run-tool', 'status', 'test', 'wait-for-ready']) {
      expect(names).toContain(name);
    }
    const exportCmd = envelope.data.commands.find((c: { name: string }) => c.name === 'export');
    expect(exportCmd.arguments).toEqual([
      { name: 'preset', required: true, variadic: false, description: expect.any(String) },
      { name: 'output', required: true, variadic: false, description: expect.any(String) },
    ]);
    expect(exportCmd.options.map((o: { flags: string }) => o.flags)).toContain('--format <format>');
    expect(envelope.data.exitCodes.map((e: { code: number }) => e.code)).toEqual([0, 1, 2, 6, 7, 8, 14, 17, 18]);
  });

  it('--help and --version exit 0; unknown options and bad --format exit 2', async () => {
    expect((await runCli(['--version'])).exitCode).toBe(0);
    expect((await runCli(['check', '--help'])).exitCode).toBe(0);
    expect((await runCli(['status', '--bogus'])).exitCode).toBe(2);
    expect((await runCli(['status', '--url', 'http://127.0.0.1:1', '--format', 'xml'])).exitCode).toBe(2);
  });

  it('a commander parse error in json mode still prints a USAGE envelope on stdout', async () => {
    const { stdout, exitCode } = await runCli(['status', '--bogus', '--format', 'json']);
    expect(exitCode).toBe(2);
    expect(JSON.parse(stdout)).toMatchObject({ success: false, command: 'status', errors: [{ code: 'USAGE' }] });
  });

  it('validation errors exit 2 with a USAGE envelope', async () => {
    const { stdout, exitCode } = await runCli(['status', tmp, '--format', 'json']);
    expect(exitCode).toBe(2);
    expect(JSON.parse(stdout).errors[0].code).toBe('USAGE');
    expect((await runCli(['check', tmp])).exitCode).toBe(2);
  });

  it('status: unreachable server exits 7 with data in the envelope', async () => {
    const port = await deadPort();
    const { stdout, exitCode } = await runCli(['status', '--url', `http://127.0.0.1:${port}`, '--timeout', '2000', '--format', 'json']);
    expect(exitCode).toBe(7);
    const envelope = JSON.parse(stdout);
    expect(envelope).toMatchObject({ success: false, command: 'status', errors: [{ code: 'UNREACHABLE' }] });
    expect(envelope.data.server).toMatchObject({ reachable: false });
  });

  it('run-tool: a tool answering status "error" exits 6; HTTP 503 exits 17; ndjson puts one envelope line on stdout', async () => {
    const failing = await startServer((res) => {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ status: 'error', content: [{ type: 'text', text: 'no such node' }] }));
    });
    try {
      const r = await runCli(['run-tool', 'x', '--url', failing.url, '--format', 'json']);
      expect(r.exitCode).toBe(6);
      expect(JSON.parse(r.stdout)).toMatchObject({ success: false, command: 'run-tool', errors: [{ code: 'TOOL_FAILED', message: 'no such node' }] });
      const nd = await runCli(['run-tool', 'x', '--url', failing.url, '--format', 'ndjson']);
      expect(nd.stdout.trim().split('\n')).toHaveLength(1);
      expect(JSON.parse(nd.stdout).errors[0].code).toBe('TOOL_FAILED');
    } finally {
      await failing.close();
    }

    const busy = await startServer((res) => {
      res.writeHead(503, { 'Content-Type': 'application/json' });
      res.end('{"error":"busy"}');
    });
    try {
      const r = await runCli(['run-system-tool', 'ping', '--url', busy.url, '--format', 'json']);
      expect(r.exitCode).toBe(17);
      expect(JSON.parse(r.stdout).errors[0].code).toBe('BUSY');
    } finally {
      await busy.close();
    }
  });

  it('run-tool: success in json mode carries the tool response as data and nothing else on stdout', async () => {
    const ok = await startServer((res) => {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ status: 'success', structured: { result: 'pong' } }));
    });
    try {
      const r = await runCli(['run-system-tool', 'ping', '--url', ok.url, '--format', 'json', '--verbose']);
      expect(r.exitCode).toBe(0);
      expect(JSON.parse(r.stdout)).toEqual({ success: true, command: 'run-system-tool', data: { status: 'success', structured: { result: 'pong' } }, errors: [], warnings: [] });
      expect(r.stderr).toContain('[verbose]');
    } finally {
      await ok.close();
    }
  });

  it('run-tool --no-start on a Godot project with no server exits 7 without starting an editor', async () => {
    fs.writeFileSync(path.join(tmp, 'project.godot'), 'config_version=5\n');
    const port = await deadPort();
    const { stdout, exitCode } = await runCli(['run-tool', 'x', tmp, '--url', `http://127.0.0.1:${port}`, '--no-start', '--format', 'json']);
    expect(exitCode).toBe(7);
    expect(JSON.parse(stdout).errors[0].code).toBe('UNREACHABLE');
  });

  it('test: no runner installed exits 6 NOT_FOUND', async () => {
    fs.writeFileSync(path.join(tmp, 'project.godot'), 'config_version=5\n');
    const { stdout, exitCode } = await runCli(['test', tmp, '--format', 'json']);
    expect(exitCode).toBe(6);
    expect(JSON.parse(stdout).errors[0].code).toBe('NOT_FOUND');
  });
});
