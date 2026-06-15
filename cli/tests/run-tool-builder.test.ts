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

/** Echo server that returns `{ ok: true, tool, body }` for any POST. */
function startEchoServer(): Promise<{ url: string; close: () => Promise<void> }> {
  return new Promise((resolve) => {
    const server = http.createServer((req, res) => {
      let body = '';
      req.on('data', (d) => (body += d));
      req.on('end', () => {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ ok: true, path: req.url, body: body || '{}' }));
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

describe('run-tool (builder) — CLI smoke', () => {
  it('shows help with --input / --input-file / --raw / --timeout', async () => {
    const { stdout, exitCode } = await runCliAsync(['run-tool', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--input');
    expect(stdout).toContain('--input-file');
    expect(stdout).toContain('--raw');
    expect(stdout).toContain('--timeout');
  });

  it('exits 1 for an invalid --timeout value', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'run-tool', 'ping', '--url', 'http://127.0.0.1:1', '--timeout', 'nope',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Invalid --timeout value');
  });

  it('exits 1 with the connection-refused failure copy when the server is down', async () => {
    const deadPort = await findDeadPort();
    const { stdout, exitCode } = await runCliAsync([
      'run-tool', 'ping', '--url', `http://127.0.0.1:${deadPort}`, '--timeout', '1500',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Connection refused');
    expect(stdout).toContain('Failed to call tool');
  });

  it('exits 1 and rejects malformed --input JSON', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'run-tool', 'ping', '--url', 'http://127.0.0.1:1', '--input', 'not-json',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('--input must be valid JSON');
  });

  it('POSTs to /api/tools/<name> and prints the formatted response on success', async () => {
    const srv = await startEchoServer();
    try {
      const { stdout, exitCode } = await runCliAsync([
        'run-tool', 'ping', '--url', srv.url, '--timeout', '3000',
      ]);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('ping completed');
      expect(stdout).toContain('/api/tools/ping');
    } finally {
      await srv.close();
    }
  });

  it('--raw prints only the JSON payload (no decorative headings)', async () => {
    const srv = await startEchoServer();
    try {
      const { stdout, exitCode } = await runCliAsync([
        'run-tool', 'ping', '--url', srv.url, '--timeout', '3000', '--raw',
      ]);
      expect(exitCode).toBe(0);
      expect(stdout).not.toContain('Run Tool');
      const parsed = JSON.parse(stdout.trim());
      expect(parsed.ok).toBe(true);
      expect(parsed.path).toBe('/api/tools/ping');
    } finally {
      await srv.close();
    }
  });

  it('forwards --input as the POST body', async () => {
    const srv = await startEchoServer();
    try {
      const { stdout, exitCode } = await runCliAsync([
        'run-tool', 'echo', '--url', srv.url, '--timeout', '3000', '--raw',
        '--input', '{"message":"hello"}',
      ]);
      expect(exitCode).toBe(0);
      const parsed = JSON.parse(stdout.trim());
      expect(JSON.parse(parsed.body)).toEqual({ message: 'hello' });
    } finally {
      await srv.close();
    }
  });

  it('reads JSON arguments from --input-file', async () => {
    const srv = await startEchoServer();
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-input-'));
    const file = path.join(tmp, 'args.json');
    fs.writeFileSync(file, '{"from":"file"}');
    try {
      const { stdout, exitCode } = await runCliAsync([
        'run-tool', 'echo', '--url', srv.url, '--timeout', '3000', '--raw',
        '--input-file', file,
      ]);
      expect(exitCode).toBe(0);
      const parsed = JSON.parse(stdout.trim());
      expect(JSON.parse(parsed.body)).toEqual({ from: 'file' });
    } finally {
      await srv.close();
      fs.rmSync(tmp, { recursive: true, force: true });
    }
  });

  it('exits 1 when --input-file does not exist', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'run-tool', 'ping', '--url', 'http://127.0.0.1:1', '--input-file', '/no/such/file.json',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Input file does not exist');
  });
});

describe('run-system-tool (builder) — CLI smoke', () => {
  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['run-system-tool', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--input');
    expect(stdout).toContain('--timeout');
  });

  it('targets the /api/system-tools/<name> route (not /api/tools)', async () => {
    const srv = await startEchoServer();
    try {
      const { stdout, exitCode } = await runCliAsync([
        'run-system-tool', 'health', '--url', srv.url, '--timeout', '3000', '--raw',
      ]);
      expect(exitCode).toBe(0);
      const parsed = JSON.parse(stdout.trim());
      expect(parsed.path).toBe('/api/system-tools/health');
    } finally {
      await srv.close();
    }
  });

  it('uses the "system tool" noun in failure copy on connection refused', async () => {
    const deadPort = await findDeadPort();
    const { stdout, exitCode } = await runCliAsync([
      'run-system-tool', 'health', '--url', `http://127.0.0.1:${deadPort}`, '--timeout', '1500',
    ]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Failed to call system tool');
  });
});
