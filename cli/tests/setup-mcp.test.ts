import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { setupMcp, listAgentIds } from '../src/lib/setup-mcp.js';
import { ENV_HOST, ENV_TOKEN } from '../src/utils/connection.js';

describe('setupMcp', () => {
  let tmpDir: string;
  const saved: Record<string, string | undefined> = {};

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-setup-mcp-'));
    for (const k of [ENV_HOST, ENV_TOKEN]) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
  });
  afterEach(() => {
    for (const k of [ENV_HOST, ENV_TOKEN]) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('lists the known agent ids', () => {
    const ids = listAgentIds();
    expect(ids).toContain('claude-code');
    expect(ids).toContain('cursor');
    expect(ids).toContain('vscode');
    expect(ids).toContain('custom');
  });

  it('fails for an unknown agent', async () => {
    const result = await setupMcp({ agentId: 'nope', godotProjectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.error.message).toContain('Unknown agent');
    }
  });

  it('writes a claude-code .mcp.json under mcpServers with the cloud /mcp URL by default', async () => {
    const result = await setupMcp({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    expect(result.serverUrl).toBe('https://ai-game.dev/mcp');
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.mcp.json'), 'utf-8'));
    expect(json.mcpServers['ai-game-developer']).toMatchObject({
      type: 'http',
      url: 'https://ai-game.dev/mcp',
    });
  });

  it('derives the <host>/mcp client URL from --url', async () => {
    const result = await setupMcp({
      agentId: 'cursor',
      godotProjectPath: tmpDir,
      url: 'http://localhost:8080',
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.serverUrl).toBe('http://localhost:8080/mcp');
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.cursor', 'mcp.json'), 'utf-8'));
    expect(json.mcpServers['ai-game-developer'].url).toBe('http://localhost:8080/mcp');
  });

  it('writes a VS Code config under the "servers" body path', async () => {
    const result = await setupMcp({ agentId: 'vscode', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.vscode', 'mcp.json'), 'utf-8'));
    expect(json.servers['ai-game-developer'].url).toBe('http://localhost:8080/mcp');
  });

  it('adds an Authorization header when a token is provided', async () => {
    const result = await setupMcp({
      agentId: 'claude-code',
      godotProjectPath: tmpDir,
      url: 'http://localhost:8080',
      token: 'secret',
    });
    expect(result.kind).toBe('success');
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.mcp.json'), 'utf-8'));
    expect(json.mcpServers['ai-game-developer'].headers).toEqual({ Authorization: 'Bearer secret' });
  });
});
