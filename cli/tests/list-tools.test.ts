import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import http from 'http';
import {
  exampleCall,
  listToolsLive,
  mcpEndpoint,
  offlineTools,
  parseMcpBody,
  readToolsCache,
  writeToolsCache,
} from '../src/lib/list-tools.js';
import { httpFailureHint } from '../src/commands/run-tool-builder.js';
import { buildOpenEnv } from '../src/lib/open.js';
import { catalogToolIds } from '../src/utils/skills.js';
import { toolErrorText } from '../src/lib/exec.js';
import { spawn } from 'child_process';
import { CLI_PATH } from './helpers/cli.js';
import { CLI_SPAWN_TIMEOUT_MS } from './helpers/timeouts.js';

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

describe('tools command', () => {
  let tmp: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-toolscmd-'));
    fs.writeFileSync(path.join(tmp, 'project.godot'), 'config_version=5\n');
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  it('lists the live tools in one json envelope and does not cache an explicit --url', async () => {
    const server = await startMcpServer();
    try {
      const r = await runCli(['tools', '--path', tmp, '--url', server.url, '--format', 'json']);
      expect(r.exitCode).toBe(0);
      const envelope = JSON.parse(r.stdout);
      expect(envelope).toMatchObject({ success: true, command: 'tools', data: { source: 'editor' } });
      expect(envelope.data.tools.map((t: { name: string }) => t.name)).toEqual(['a-tool', 'b-tool']);
      expect(fs.existsSync(path.join(tmp, '.ai-game-dev', 'tools.json'))).toBe(false);
    } finally {
      await server.close();
    }
  });

  it('shows a tool with its example call, and NOT_FOUND (exit 2) for an unknown one', async () => {
    const server = await startMcpServer();
    try {
      const one = await runCli(['tools', 'a-tool', '--path', tmp, '--url', server.url, '--format', 'json']);
      expect(JSON.parse(one.stdout).data.example).toBe('godot-cli run-tool a-tool --input \'{"q":"<q>"}\'');
      const none = await runCli(['tools', 'zzz', '--path', tmp, '--url', server.url, '--format', 'json']);
      expect(none.exitCode).toBe(2);
      expect(JSON.parse(none.stdout).errors[0].code).toBe('NOT_FOUND');
    } finally {
      await server.close();
    }
  });
});

describe('toolErrorText', () => {
  const failure = (data: unknown) => ({ kind: 'failure' as const, success: false as const, endpoint: '', reason: 'http-error' as const, message: 'Unprocessable Entity', data });
  it('reads the error from the shapes servers answer with', () => {
    expect(toolErrorText(failure({ error: 'boom' }))).toBe('boom');
    expect(toolErrorText(failure({ status: 'error', content: [{ type: 'text', text: 'file not found' }] }))).toBe('file not found');
    expect(toolErrorText(failure({ title: 'One or more validation errors occurred.', errors: { query: ['required'] } })))
      .toBe('One or more validation errors occurred. query: required');
    expect(toolErrorText(failure({ unknown: 1 }))).toBe('Unprocessable Entity');
  });
});

/** A Streamable-HTTP MCP server answering initialize and two pages of tools/list over SSE. */
function startMcpServer(): Promise<{ url: string; requests: string[]; close: () => Promise<void> }> {
  const requests: string[] = [];
  return new Promise((resolve) => {
    const server = http.createServer((req, res) => {
      let body = '';
      req.on('data', (d) => (body += d));
      req.on('end', () => {
        requests.push(`${req.method} ${req.url} ${req.headers['mcp-session-id'] ?? '-'}`);
        if (req.method === 'DELETE') return res.end();
        const msg = JSON.parse(body) as { id?: number; method: string; params?: { cursor?: string } };
        if (msg.id === undefined) {
          res.writeHead(202);
          return res.end();
        }
        let result: unknown = {};
        if (msg.method === 'initialize') result = { protocolVersion: '2025-03-26' };
        if (msg.method === 'tools/list') {
          result = msg.params?.cursor
            ? { tools: [{ name: 'b-tool', description: 'B.', inputSchema: { type: 'object', properties: {} } }] }
            : { tools: [{ name: 'a-tool', description: 'A.', inputSchema: { type: 'object', properties: { q: { type: 'string' } }, required: ['q'] } }], nextCursor: 'p2' };
        }
        res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Mcp-Session-Id': 'S1' });
        res.end(`event: message\ndata: ${JSON.stringify({ jsonrpc: '2.0', id: msg.id, result })}\n\n`);
      });
    });
    server.listen(0, '127.0.0.1', () => {
      const addr = server.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      resolve({ url: `http://127.0.0.1:${port}`, requests, close: () => new Promise<void>((r) => server.close(() => r())) });
    });
  });
}

describe('listToolsLive', () => {
  it('initializes, pages through tools/list with the session id, and sorts', async () => {
    const server = await startMcpServer();
    try {
      const r = await listToolsLive(server.url, undefined, 5_000);
      expect(r.kind).toBe('success');
      if (r.kind === 'success') expect(r.tools.map((t) => t.name)).toEqual(['a-tool', 'b-tool']);
      expect(server.requests.slice(0, 4)).toEqual(['POST /mcp -', 'POST /mcp S1', 'POST /mcp S1', 'POST /mcp S1']);
    } finally {
      await server.close();
    }
  });

  it('reports a refused connection as unreachable', async () => {
    const port = await new Promise<number>((resolve) => {
      const s = http.createServer();
      s.listen(0, '127.0.0.1', () => {
        const addr = s.address();
        const p = typeof addr === 'object' && addr ? addr.port : 0;
        s.close(() => resolve(p));
      });
    });
    const r = await listToolsLive(`http://127.0.0.1:${port}`, undefined, 5_000);
    expect(r).toMatchObject({ kind: 'failure', reason: 'unreachable' });
  });
});

describe('tools helpers', () => {
  it('maps base URLs to the MCP endpoint', () => {
    expect(mcpEndpoint('http://localhost:5000')).toBe('http://localhost:5000/mcp');
    expect(mcpEndpoint('https://ai-game.dev/mcp')).toBe('https://ai-game.dev/mcp');
    expect(mcpEndpoint('https://ai-game.dev/mcp/p/abc/')).toBe('https://ai-game.dev/mcp/p/abc');
  });

  it('parses plain JSON and SSE bodies', () => {
    expect(parseMcpBody('{"id":1,"result":{}}')).toEqual([{ id: 1, result: {} }]);
    expect(parseMcpBody('event: message\ndata: {"id":2,"result":{"x":1}}\n\n')).toEqual([{ id: 2, result: { x: 1 } }]);
  });

  it('builds an example call from the required arguments', () => {
    expect(exampleCall({ name: 'rql-mutate', description: '', inputSchema: { properties: { query: { type: 'string' }, dry_run: { type: 'boolean' } }, required: ['query'] } }))
      .toBe('godot-cli run-tool rql-mutate --input \'{"query":"<query>"}\'');
    expect(exampleCall({ name: 'x', description: '', inputSchema: { properties: { n: { type: ['integer', 'null'] } }, required: ['n'] } }))
      .toBe('godot-cli run-tool x --input \'{"n":0}\'');
  });

  it('caches the list and keeps it out of version control', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-tools-'));
    try {
      writeToolsCache(dir, 'http://localhost:1', [{ name: 'a', description: 'A' }]);
      writeToolsCache(dir, 'http://localhost:1', [{ name: 'a', description: 'A' }]);
      expect(readToolsCache(dir)?.tools).toEqual([{ name: 'a', description: 'A' }]);
      expect(fs.readFileSync(path.join(dir, '.ai-game-dev', '.gitignore'), 'utf-8')).toBe('tools.json\n');
    } finally {
      fs.rmSync(dir, { recursive: true, force: true });
    }
  });

  it('knows every addon tool without an editor', () => {
    expect(offlineTools().map((t) => t.name)).toEqual([...catalogToolIds()].sort((a, b) => a.localeCompare(b)));
  });

  it('points to the next step after a 404 / 400', () => {
    expect(httpFailureHint(404, 'x')).toMatch(/godot-cli tools`/);
    expect(httpFailureHint(400, 'rql-query')).toMatch(/godot-cli tools rql-query/);
    expect(httpFailureHint(500, 'x')).toBeUndefined();
  });
});

describe('open: local fallback env', () => {
  it('asks the plugin to start its local server', () => {
    expect(buildOpenEnv({ url: 'http://localhost:1', mode: 'Custom', auth: 'None', startServer: true })).toEqual({
      GODOT_MCP_HOST: 'http://localhost:1',
      GODOT_MCP_CONNECTION_MODE: 'Custom',
      GODOT_MCP_AUTH_OPTION: 'None',
      GODOT_MCP_START_SERVER: 'true',
    });
  });
});
