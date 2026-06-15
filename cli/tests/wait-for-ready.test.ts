import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import http from 'http';
import { runCliAsync } from './helpers/cli.js';

/**
 * Bind a server on port 0 (OS-assigned), capture the port, then close it —
 * yielding a port that is guaranteed free right now so a connection there is
 * refused. Avoids the tiny collision risk of a hard-coded port.
 */
function findDeadPort(): Promise<number> {
  return new Promise((resolve) => {
    const server = http.createServer();
    server.listen(0, '127.0.0.1', () => {
      const addr = server.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      server.close(() => resolve(port));
    });
  });
}

/** Start a throwaway HTTP server that answers the ping probe with `pong`. */
function startPingServer(): Promise<{ url: string; close: () => Promise<void> }> {
  return new Promise((resolve) => {
    const server = http.createServer((req, res) => {
      let body = '';
      req.on('data', (d) => (body += d));
      req.on('end', () => {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'success', structured: { result: 'pong' } }));
      });
    });
    server.listen(0, '127.0.0.1', () => {
      const addr = server.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      resolve({
        url: `http://127.0.0.1:${port}`,
        close: () => new Promise<void>((r) => server.close(() => r())),
      });
    });
  });
}

describe('wait-for-ready — CLI smoke', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-wfr-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['wait-for-ready', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--url');
    expect(stdout).toContain('--timeout');
    expect(stdout).toContain('--interval');
  });

  it('exits 1 for an invalid --timeout value', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'wait-for-ready',
      '--url',
      'http://localhost:1',
      '--timeout',
      '0',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Invalid --timeout value');
  });

  it('exits 1 for an invalid --interval value', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'wait-for-ready',
      '--url',
      'http://localhost:1',
      '--interval',
      '-1',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Invalid --interval value');
  });

  it('exits 1 when the path is not a Godot project and no --url is given', async () => {
    const { stdout, exitCode } = await runCliAsync(['wait-for-ready', tmpDir]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Not a Godot project');
  });

  it('times out and exits 1 when the server never comes up', async () => {
    const deadPort = await findDeadPort();
    const { stdout, exitCode } = await runCliAsync([
      'wait-for-ready',
      '--url',
      `http://127.0.0.1:${deadPort}`,
      '--timeout',
      '1200',
      '--interval',
      '400',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Timed out');
  });

  it('exits 0 as soon as the ping probe succeeds', async () => {
    const srv = await startPingServer();
    try {
      const { stdout, exitCode } = await runCliAsync([
        'wait-for-ready',
        '--url',
        srv.url,
        '--timeout',
        '5000',
        '--interval',
        '500',
      ]);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('MCP server is ready');
    } finally {
      await srv.close();
    }
  });
});
