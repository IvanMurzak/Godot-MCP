import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import http from 'http';
import { runCliAsync } from './helpers/cli.js';

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

describe('status — CLI smoke', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-status-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['status', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--url');
    expect(stdout).toContain('--token');
    expect(stdout).toContain('--timeout');
  });

  it('exits 1 for an invalid --timeout value', async () => {
    const { stdout, exitCode } = await runCliAsync(['status', '--url', 'http://localhost:1', '--timeout', '0']);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Invalid --timeout value');
  });

  it('exits 1 when the path is not a Godot project and no --url is given', async () => {
    const { stdout, exitCode } = await runCliAsync(['status', tmpDir]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Not a Godot project');
  });

  it('exits 1 when the server is unreachable (connection refused)', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'status',
      '--url',
      'http://127.0.0.1:59999',
      '--timeout',
      '1500',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('connection refused');
    expect(stdout).toContain('not reachable');
  });

  it('exits 0 and reports the server reachable when the ping probe succeeds', async () => {
    const srv = await startPingServer();
    try {
      const { stdout, exitCode } = await runCliAsync([
        'status',
        '--url',
        srv.url,
        '--timeout',
        '3000',
      ]);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('Connected');
      expect(stdout).toContain('MCP server is reachable');
    } finally {
      await srv.close();
    }
  });
});
