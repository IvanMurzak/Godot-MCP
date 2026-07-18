import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

// Redirectable home dir: agents.ts resolves home-dir / global agent config paths
// via os.homedir(); ESM forbids spying on the live export, so we mock the module
// and back homedir() with a mutable holder the tests can repoint under tmpDir.
const homeHolder = vi.hoisted(() => ({ dir: '' }));
vi.mock('os', async (importOriginal) => {
  const actual = await importOriginal<typeof import('os')>();
  return { ...actual, homedir: () => homeHolder.dir || actual.homedir() };
});
import { setupMcp, listAgentIds, shouldWriteAuthHeader } from '../src/lib/setup-mcp.js';
import {
  agentRegistry,
  getAgentById,
  getAgentIds,
  writeJsonAgentConfig,
  writeTomlAgentConfig,
  MCP_SERVER_NAME,
} from '../src/utils/agents.js';
import { derivePinV2 } from '../src/utils/project-identity.js';
import { ENV_HOST, ENV_TOKEN } from '../src/utils/connection.js';

const SERVER_NAME = 'ai-game-developer';

/**
 * The pinned MCP-client URL setup-mcp writes by DEFAULT (design 02 §T4 / B4): `<base>/mcp/p/<pin-v2>`
 * where the pin is the shared cli-core v2 pin of the RESOLVED project root (matching what the editor
 * Configure writes). setup-mcp resolves the project path with `path.resolve`, so tests derive the
 * same pin from `path.resolve(projectPath)`.
 */
const pinnedFor = (projectPath: string, base = 'https://ai-game.dev'): string =>
  `${base}/mcp/p/${derivePinV2(path.resolve(projectPath))}`;

describe('setupMcp', () => {
  let tmpDir: string;
  const saved: Record<string, string | undefined> = {};
  // Env vars that steer home-dir / global agent config paths (agents.ts `appData()`
  // reads APPDATA; the posix branches of `home()` read HOME/XDG_CONFIG_HOME).
  const HOME_ENV_KEYS = ['APPDATA', 'HOME', 'XDG_CONFIG_HOME'];

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-setup-mcp-'));
    for (const k of [ENV_HOST, ENV_TOKEN]) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    // Redirect home-dir / global agent writes (cline, github-copilot-cli,
    // antigravity, claude-desktop) under tmpDir so they never touch the real
    // developer config. agents.ts resolves these via os.homedir() and APPDATA.
    for (const k of HOME_ENV_KEYS) {
      saved[k] = process.env[k];
      process.env[k] = tmpDir;
    }
    homeHolder.dir = tmpDir;
  });
  afterEach(() => {
    homeHolder.dir = '';
    for (const k of [ENV_HOST, ENV_TOKEN, ...HOME_ENV_KEYS]) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('lists the known agent ids (parity roster)', () => {
    const ids = listAgentIds();
    // Original 5
    expect(ids).toContain('claude-code');
    expect(ids).toContain('claude-desktop');
    expect(ids).toContain('cursor');
    expect(ids).toContain('vscode-copilot');
    expect(ids).toContain('custom');
    // Ported from Unity for parity
    expect(ids).toContain('vs-copilot');
    expect(ids).toContain('rider-junie');
    expect(ids).toContain('github-copilot-cli');
    expect(ids).toContain('gemini');
    expect(ids).toContain('antigravity');
    expect(ids).toContain('cline');
    expect(ids).toContain('open-code');
    expect(ids).toContain('codex');
    expect(ids).toContain('kilo-code');
  });

  it('excludes the Unity-only unity-ai agent', () => {
    expect(listAgentIds()).not.toContain('unity-ai');
    expect(getAgentById('unity-ai')).toBeUndefined();
  });

  it('fails for an unknown agent', async () => {
    const result = await setupMcp({ agentId: 'nope', godotProjectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.error.message).toContain('Unknown agent');
    }
  });

  it('writes a claude-code .mcp.json under mcpServers with the PINNED cloud URL by default', async () => {
    const result = await setupMcp({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    expect(result.serverUrl).toBe(pinnedFor(tmpDir));
    expect(result.pinned).toBe(true);
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.mcp.json'), 'utf-8'));
    expect(json.mcpServers[SERVER_NAME]).toMatchObject({
      type: 'http',
      url: pinnedFor(tmpDir),
    });
  });

  it('derives the PINNED <host>/mcp/p/<pin> client URL from --url', async () => {
    const result = await setupMcp({
      agentId: 'cursor',
      godotProjectPath: tmpDir,
      url: 'http://localhost:8080',
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.serverUrl).toBe(pinnedFor(tmpDir, 'http://localhost:8080'));
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.cursor', 'mcp.json'), 'utf-8'));
    expect(json.mcpServers[SERVER_NAME].url).toBe(pinnedFor(tmpDir, 'http://localhost:8080'));
  });

  it('--no-pin writes the bare unpinned <host>/mcp URL (escape hatch)', async () => {
    const result = await setupMcp({
      agentId: 'claude-code',
      godotProjectPath: tmpDir,
      url: 'http://localhost:8080',
      noPin: true,
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.serverUrl).toBe('http://localhost:8080/mcp');
    expect(result.pinned).toBe(false);
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.mcp.json'), 'utf-8'));
    expect(json.mcpServers[SERVER_NAME].url).toBe('http://localhost:8080/mcp');
    // No `/p/<pin>` routing segment on the unpinned URL.
    expect(json.mcpServers[SERVER_NAME].url).not.toMatch(/\/p\/[0-9a-f]{8}$/);
  });

  it('--no-pin (default cloud) writes the bare https://ai-game.dev/mcp URL', async () => {
    const result = await setupMcp({ agentId: 'claude-code', godotProjectPath: tmpDir, noPin: true });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.serverUrl).toBe('https://ai-game.dev/mcp');
    expect(result.pinned).toBe(false);
  });

  it('writes a VS Code config under the "servers" body path (pinned)', async () => {
    const result = await setupMcp({ agentId: 'vscode-copilot', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.vscode', 'mcp.json'), 'utf-8'));
    expect(json.servers[SERVER_NAME].url).toBe(pinnedFor(tmpDir, 'http://localhost:8080'));
  });

  it('writes a Visual Studio (vs-copilot) config under .vs/mcp.json / servers (pinned)', async () => {
    const result = await setupMcp({ agentId: 'vs-copilot', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.configPath).toBe(path.join(tmpDir, '.vs', 'mcp.json'));
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.vs', 'mcp.json'), 'utf-8'));
    expect(json.servers[SERVER_NAME].url).toBe(pinnedFor(tmpDir, 'http://localhost:8080'));
  });

  it('writes rider-junie with enabled:true under .junie/mcp/mcp.json (pinned)', async () => {
    const result = await setupMcp({ agentId: 'rider-junie', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.configPath).toBe(path.join(tmpDir, '.junie', 'mcp', 'mcp.json'));
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcpServers[SERVER_NAME]).toMatchObject({
      enabled: true,
      type: 'http',
      url: pinnedFor(tmpDir, 'http://localhost:8080'),
    });
  });

  it('writes gemini under .gemini/settings.json / mcpServers (pinned)', async () => {
    const result = await setupMcp({ agentId: 'gemini', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.configPath).toBe(path.join(tmpDir, '.gemini', 'settings.json'));
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcpServers[SERVER_NAME].url).toBe(pinnedFor(tmpDir, 'http://localhost:8080'));
  });

  it('writes open-code with type:remote + enabled:true under opencode.json / mcp (pinned)', async () => {
    const result = await setupMcp({ agentId: 'open-code', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.configPath).toBe(path.join(tmpDir, 'opencode.json'));
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcp[SERVER_NAME]).toMatchObject({
      type: 'remote',
      enabled: true,
      url: pinnedFor(tmpDir, 'http://localhost:8080'),
    });
  });

  it('writes kilo-code with type:streamable-http + disabled:false (pinned)', async () => {
    const result = await setupMcp({ agentId: 'kilo-code', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.configPath).toBe(path.join(tmpDir, '.kilocode', 'mcp.json'));
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcpServers[SERVER_NAME]).toMatchObject({
      type: 'streamable-http',
      disabled: false,
      url: pinnedFor(tmpDir, 'http://localhost:8080'),
    });
  });

  it('writes cline with type:streamableHttp (pinned)', async () => {
    const result = await setupMcp({ agentId: 'cline', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    // cline writes to a home-dir global path; assert the entry written there.
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcpServers[SERVER_NAME]).toMatchObject({
      type: 'streamableHttp',
      url: pinnedFor(tmpDir, 'http://localhost:8080'),
    });
  });

  it('writes github-copilot-cli with the tools:["*"] passthrough (pinned)', async () => {
    const result = await setupMcp({ agentId: 'github-copilot-cli', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcpServers[SERVER_NAME]).toMatchObject({
      type: 'http',
      url: pinnedFor(tmpDir, 'http://localhost:8080'),
      tools: ['*'],
    });
  });

  it('writes antigravity with the serverUrl key (not url) and disabled:false (pinned)', async () => {
    const result = await setupMcp({ agentId: 'antigravity', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    const json = JSON.parse(fs.readFileSync(result.configPath, 'utf-8'));
    expect(json.mcpServers[SERVER_NAME]).toMatchObject({
      disabled: false,
      serverUrl: pinnedFor(tmpDir, 'http://localhost:8080'),
    });
    // antigravity never carries a `url` key.
    expect(json.mcpServers[SERVER_NAME].url).toBeUndefined();
  });

  it('writes codex as TOML under .codex/config.toml / mcp_servers (pinned)', async () => {
    const result = await setupMcp({ agentId: 'codex', godotProjectPath: tmpDir, url: 'http://localhost:8080' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.configPath).toBe(path.join(tmpDir, '.codex', 'config.toml'));
    const toml = fs.readFileSync(result.configPath, 'utf-8');
    expect(toml).toContain(`[mcp_servers.${SERVER_NAME}]`);
    expect(toml).toContain('enabled = true');
    expect(toml).toContain(`url = "${pinnedFor(tmpDir, 'http://localhost:8080')}"`);
    expect(toml).toContain('tool_timeout_sec = 300');
    expect(toml).toContain('startup_timeout_sec = 30');
  });

  // --- D11 / Flow A: OAuth-capable clients get a credential-free, URL-only config ---

  it('omits the Authorization header for OAuth-capable clients even when GODOT_MCP_TOKEN is set (flagship g1 fix)', async () => {
    // A prior `login` leaves GODOT_MCP_TOKEN in the environment. setup-mcp must NOT
    // inject it as a static header for OAuth-capable clients (claude-code, cursor,
    // copilot, …): the hosted AS rejects the token (401) AND the client then skips
    // its own OAuth handshake ("OAuth fallback is disabled when headers.Authorization
    // is set"). The config must stay URL-only so native RFC 9728 OAuth runs (Flow A).
    process.env[ENV_TOKEN] = 'agd_pat_ambient';
    const cases: Array<[string, string[], string]> = [
      ['claude-code', ['.mcp.json'], 'mcpServers'],
      ['cursor', ['.cursor', 'mcp.json'], 'mcpServers'],
      ['vscode-copilot', ['.vscode', 'mcp.json'], 'servers'],
    ];
    for (const [id, rel, body] of cases) {
      const result = await setupMcp({ agentId: id, godotProjectPath: tmpDir });
      expect(result.kind).toBe('success');
      if (result.kind !== 'success') continue;
      const json = JSON.parse(fs.readFileSync(path.join(tmpDir, ...rel), 'utf-8'));
      const entry = json[body][SERVER_NAME];
      // Default is pinned; the credential-free (no static header) policy is orthogonal to pinning.
      expect(entry).toMatchObject({ type: 'http', url: pinnedFor(tmpDir) });
      expect(entry.headers).toBeUndefined();
      // No credential landed in the file → no VCS-leak warning.
      expect(result.warnings).toEqual([]);
    }
  });

  it('codex writes URL-only TOML (no Authorization) even with an ambient GODOT_MCP_TOKEN', async () => {
    process.env[ENV_TOKEN] = 'agd_pat_ambient';
    const result = await setupMcp({ agentId: 'codex', godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    const toml = fs.readFileSync(result.configPath, 'utf-8');
    expect(toml).not.toContain('Authorization');
    expect(toml).not.toContain('headers');
  });

  it('adds an Authorization header on an explicit --token opt-in (Flow C) and warns about the project-file PAT (claude-code)', async () => {
    // Passing --token / the library `token` arg is a deliberate PAT opt-in, unlike
    // the ambient env token above — the legacy header shape is still written.
    const result = await setupMcp({
      agentId: 'claude-code',
      godotProjectPath: tmpDir,
      url: 'http://localhost:8080',
      token: 'secret',
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.mcp.json'), 'utf-8'));
    expect(json.mcpServers[SERVER_NAME].headers).toEqual({ Authorization: 'Bearer secret' });
    // Flow C: writing a PAT into a project-scoped config warns about the VCS-leak risk.
    expect(result.warnings.some((w) => /PAT/i.test(w) && /leak/i.test(w))).toBe(true);
  });

  it('does NOT add Authorization headers to codex (token-less TOML)', async () => {
    const result = await setupMcp({
      agentId: 'codex',
      godotProjectPath: tmpDir,
      url: 'http://localhost:8080',
      token: 'secret',
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    const toml = fs.readFileSync(result.configPath, 'utf-8');
    expect(toml).not.toContain('Authorization');
    expect(toml).not.toContain('headers');
    // Codex's getHttpProps ignores the token (URL-only TOML), so no static header
    // lands in the file — and therefore NO VCS-leak warning must be raised. The
    // warning is gated on the header actually written to the config, not on the
    // presence of an (explicit) token.
    expect(result.warnings.some((w) => /PAT/i.test(w) && /leak/i.test(w))).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Per-agent config-path resolution (covers home-dir / project-dir branches)
// ---------------------------------------------------------------------------

describe('agent config-path resolution', () => {
  const projectRoot = process.platform === 'win32' ? 'C:\\proj' : '/proj';

  it('every registered agent resolves a non-empty absolute config path', () => {
    for (const agent of agentRegistry) {
      const p = agent.getConfigPath(projectRoot);
      expect(typeof p).toBe('string');
      expect(p.length).toBeGreaterThan(0);
      expect(path.isAbsolute(p)).toBe(true);
    }
  });

  it('project-scoped agents resolve under the supplied project root', () => {
    const cases: Array<[string, string[]]> = [
      ['claude-code', ['.mcp.json']],
      ['cursor', ['.cursor', 'mcp.json']],
      ['vscode-copilot', ['.vscode', 'mcp.json']],
      ['vs-copilot', ['.vs', 'mcp.json']],
      ['rider-junie', ['.junie', 'mcp', 'mcp.json']],
      ['gemini', ['.gemini', 'settings.json']],
      ['open-code', ['opencode.json']],
      ['codex', ['.codex', 'config.toml']],
      ['kilo-code', ['.kilocode', 'mcp.json']],
      ['custom', ['mcp.json']],
    ];
    for (const [id, segments] of cases) {
      const agent = getAgentById(id);
      expect(agent, `agent ${id} should exist`).toBeDefined();
      expect(agent!.getConfigPath(projectRoot)).toBe(path.join(projectRoot, ...segments));
    }
  });

  it('home-dir / global agents ignore the project root', () => {
    for (const id of ['claude-desktop', 'github-copilot-cli', 'antigravity', 'cline']) {
      const agent = getAgentById(id)!;
      const fromA = agent.getConfigPath(projectRoot);
      const fromB = agent.getConfigPath(process.platform === 'win32' ? 'D:\\other' : '/other');
      expect(fromA).toBe(fromB); // project-root independent
      expect(fromA.startsWith(projectRoot)).toBe(false);
    }
  });

  it('exposes a skillsPath field for every agent (string or null)', () => {
    for (const agent of agentRegistry) {
      expect(agent.skillsPath === null || typeof agent.skillsPath === 'string').toBe(true);
    }
    // Spot-check a couple of known skillsPath values ported from Unity.
    expect(getAgentById('claude-code')!.skillsPath).toBe('.claude/skills');
    expect(getAgentById('codex')!.skillsPath).toBe('.agents/skills');
    expect(getAgentById('claude-desktop')!.skillsPath).toBeNull();
  });

  it('declares the correct configFormat per agent (only codex is toml)', () => {
    for (const agent of agentRegistry) {
      expect(agent.configFormat === 'json' || agent.configFormat === 'toml').toBe(true);
    }
    expect(getAgentById('codex')!.configFormat).toBe('toml');
    const tomlAgents = agentRegistry.filter((a) => a.configFormat === 'toml').map((a) => a.id);
    expect(tomlAgents).toEqual(['codex']);
  });

  it('every agent declares supportsOAuth (boolean); all shipped clients are OAuth-capable (D11/b6 default true)', () => {
    for (const agent of agentRegistry) {
      expect(typeof agent.supportsOAuth).toBe('boolean');
      // Every client shipped today performs native RFC 9728 OAuth; a future
      // non-OAuth client would set this false to opt back into a static header.
      expect(agent.supportsOAuth).toBe(true);
    }
  });
});

// ---------------------------------------------------------------------------
// OAuth-aware header gate (design D11 / auth Flow A & C)
// ---------------------------------------------------------------------------

describe('shouldWriteAuthHeader', () => {
  it('never writes a header without a token', () => {
    expect(shouldWriteAuthHeader({ hasToken: false, explicitToken: false, supportsOAuth: true })).toBe(false);
    expect(shouldWriteAuthHeader({ hasToken: false, explicitToken: true, supportsOAuth: false })).toBe(false);
  });

  it('OAuth-capable client: omits the header for an ambient token, writes it only on explicit opt-in', () => {
    // ambient token (env), not explicit → URL-only (Flow A, the flagship fix)
    expect(shouldWriteAuthHeader({ hasToken: true, explicitToken: false, supportsOAuth: true })).toBe(false);
    // explicit --token opt-in → header (Flow C)
    expect(shouldWriteAuthHeader({ hasToken: true, explicitToken: true, supportsOAuth: true })).toBe(true);
  });

  it('non-OAuth client (supportsOAuth:false): writes the header whenever a token is present', () => {
    expect(shouldWriteAuthHeader({ hasToken: true, explicitToken: false, supportsOAuth: false })).toBe(true);
    expect(shouldWriteAuthHeader({ hasToken: true, explicitToken: true, supportsOAuth: false })).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Low-level writer round-trips: write / merge / remove
// ---------------------------------------------------------------------------

describe('writeJsonAgentConfig — write/merge/remove round-trip', () => {
  let tmpDir: string;
  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-json-cfg-'));
  });
  afterEach(() => fs.rmSync(tmpDir, { recursive: true, force: true }));

  it('creates a fresh file with the server entry', () => {
    const cfg = path.join(tmpDir, '.mcp.json');
    writeJsonAgentConfig(cfg, 'mcpServers', MCP_SERVER_NAME, { type: 'http', url: 'u' }, ['command', 'args']);
    const json = JSON.parse(fs.readFileSync(cfg, 'utf-8'));
    expect(json.mcpServers[MCP_SERVER_NAME]).toEqual({ type: 'http', url: 'u' });
  });

  it('merges into an existing file, preserving unrelated entries', () => {
    const cfg = path.join(tmpDir, '.mcp.json');
    fs.writeFileSync(
      cfg,
      JSON.stringify({ mcpServers: { 'other-server': { type: 'http', url: 'keep' } } }, null, 2),
    );
    writeJsonAgentConfig(cfg, 'mcpServers', MCP_SERVER_NAME, { type: 'http', url: 'new' }, ['command', 'args']);
    const json = JSON.parse(fs.readFileSync(cfg, 'utf-8'));
    expect(json.mcpServers['other-server']).toEqual({ type: 'http', url: 'keep' });
    expect(json.mcpServers[MCP_SERVER_NAME]).toEqual({ type: 'http', url: 'new' });
  });

  it('removes the deprecated Godot-MCP entry on write', () => {
    const cfg = path.join(tmpDir, '.mcp.json');
    fs.writeFileSync(
      cfg,
      JSON.stringify({ mcpServers: { 'Godot-MCP': { command: 'old', args: ['x'] } } }, null, 2),
    );
    writeJsonAgentConfig(cfg, 'mcpServers', MCP_SERVER_NAME, { type: 'http', url: 'u' }, ['command', 'args']);
    const json = JSON.parse(fs.readFileSync(cfg, 'utf-8'));
    expect(json.mcpServers['Godot-MCP']).toBeUndefined();
    expect(json.mcpServers[MCP_SERVER_NAME]).toEqual({ type: 'http', url: 'u' });
  });

  it('removes stale keys from a pre-existing server entry before merging', () => {
    const cfg = path.join(tmpDir, '.mcp.json');
    fs.writeFileSync(
      cfg,
      JSON.stringify(
        { mcpServers: { [MCP_SERVER_NAME]: { command: 'stale', args: ['a'], keep: 1 } } },
        null,
        2,
      ),
    );
    writeJsonAgentConfig(cfg, 'mcpServers', MCP_SERVER_NAME, { type: 'http', url: 'u' }, ['command', 'args']);
    const json = JSON.parse(fs.readFileSync(cfg, 'utf-8'));
    const entry = json.mcpServers[MCP_SERVER_NAME];
    expect(entry.command).toBeUndefined();
    expect(entry.args).toBeUndefined();
    expect(entry.keep).toBe(1); // untouched keys survive
    expect(entry.type).toBe('http');
    expect(entry.url).toBe('u');
  });

  it('recovers from a malformed existing file by starting fresh', () => {
    const cfg = path.join(tmpDir, '.mcp.json');
    fs.writeFileSync(cfg, '{ this is not json');
    writeJsonAgentConfig(cfg, 'mcpServers', MCP_SERVER_NAME, { type: 'http', url: 'u' }, []);
    const json = JSON.parse(fs.readFileSync(cfg, 'utf-8'));
    expect(json.mcpServers[MCP_SERVER_NAME]).toEqual({ type: 'http', url: 'u' });
  });
});

describe('writeTomlAgentConfig — write/merge/remove round-trip (Codex)', () => {
  let tmpDir: string;
  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-toml-cfg-'));
  });
  afterEach(() => fs.rmSync(tmpDir, { recursive: true, force: true }));

  it('creates a fresh TOML file with the section', () => {
    const cfg = path.join(tmpDir, 'config.toml');
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { enabled: true, url: 'http://h/mcp', tool_timeout_sec: 300 }, []);
    const toml = fs.readFileSync(cfg, 'utf-8');
    expect(toml).toContain(`[mcp_servers.${MCP_SERVER_NAME}]`);
    expect(toml).toContain('enabled = true');
    expect(toml).toContain('url = "http://h/mcp"');
    expect(toml).toContain('tool_timeout_sec = 300');
  });

  it('preserves other TOML sections when merging', () => {
    const cfg = path.join(tmpDir, 'config.toml');
    fs.writeFileSync(cfg, '[profile]\nname = "me"\n\n[mcp_servers.other]\nurl = "keep"\n');
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { enabled: true, url: 'http://h/mcp' }, []);
    const toml = fs.readFileSync(cfg, 'utf-8');
    expect(toml).toContain('[profile]');
    expect(toml).toContain('name = "me"');
    expect(toml).toContain('[mcp_servers.other]');
    expect(toml).toContain('url = "keep"');
    expect(toml).toContain(`[mcp_servers.${MCP_SERVER_NAME}]`);
    expect(toml).toContain('url = "http://h/mcp"');
  });

  it('replaces the target section wholesale on re-run (idempotent, drops stale keys)', () => {
    const cfg = path.join(tmpDir, 'config.toml');
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { enabled: true, url: 'http://old/mcp', stale: 'drop' }, []);
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { enabled: true, url: 'http://new/mcp' }, []);
    const toml = fs.readFileSync(cfg, 'utf-8');
    // Only one section header for the server.
    const headerCount = toml.split('\n').filter((l) => l.trim() === `[mcp_servers.${MCP_SERVER_NAME}]`).length;
    expect(headerCount).toBe(1);
    expect(toml).toContain('url = "http://new/mcp"');
    expect(toml).not.toContain('http://old/mcp');
    expect(toml).not.toContain('stale');
  });

  it('honors removeKeys (omits removed keys from the section)', () => {
    const cfg = path.join(tmpDir, 'config.toml');
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { enabled: true, url: 'http://h/mcp', type: 'http' }, ['type']);
    const toml = fs.readFileSync(cfg, 'utf-8');
    expect(toml).toContain('url = "http://h/mcp"');
    expect(toml).not.toContain('type =');
  });

  it('escapes special characters in string values', () => {
    const cfg = path.join(tmpDir, 'config.toml');
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { url: 'C:\\a "b"' }, []);
    const toml = fs.readFileSync(cfg, 'utf-8');
    expect(toml).toContain('url = "C:\\\\a \\"b\\""');
  });

  it('escapes special characters inside array elements', () => {
    const cfg = path.join(tmpDir, 'config.toml');
    writeTomlAgentConfig(cfg, 'mcp_servers', MCP_SERVER_NAME, { args: ['C:\\x', 'a "q"'] }, []);
    const toml = fs.readFileSync(cfg, 'utf-8');
    expect(toml).toContain('args = ["C:\\\\x", "a \\"q\\""]');
  });
});
