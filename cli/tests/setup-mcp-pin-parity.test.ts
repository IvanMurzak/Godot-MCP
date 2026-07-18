import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { setupMcp } from '../src/lib/setup-mcp.js';
import { MCP_SERVER_NAME } from '../src/utils/agents.js';
import { godotAdapter, pinUrl, derivePinV2, DEFAULT_HOSTED_MCP_URL } from '@baizor/gamedev-cli-core';

/**
 * DoD parity gate (task i1-godot-cli-migration): the CLI's `setup-mcp` must write the SAME pinned
 * MCP-client URL the Godot editor's **Configure** writes, so a terminal-written and an editor-written
 * `.mcp.json` route identically (design 02 §T4 / 03 F1 step 3). The editor Configure builds
 * `pinUrl(canonical /mcp, GodotProjectIdentity-v2 pin)` via the shared C# `AgentConfigurator`;
 * cli-core's `pinUrl` + `derivePinV2` ARE the TypeScript source of truth for that exact logic (the
 * pin is byte-for-byte golden-vector-gated against the C# `ProjectIdentity`). Anchoring the CLI's
 * output to cli-core's own computation is therefore the parity proof.
 */
describe('setup-mcp pinned-URL parity with the editor Configure', () => {
  let tmpDir: string;
  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-pin-parity-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('writes <base>/mcp/p/<pin-v2> — identical to the editor Configure URL', async () => {
    const result = await setupMcp({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    const resolved = path.resolve(tmpDir);
    const expected = pinUrl(DEFAULT_HOSTED_MCP_URL, derivePinV2(resolved));
    expect(result.serverUrl).toBe(expected);
    expect(result.serverUrl).toBe(`https://ai-game.dev/mcp/p/${derivePinV2(resolved)}`);
    expect(result.pinned).toBe(true);

    const json = JSON.parse(fs.readFileSync(path.join(tmpDir, '.mcp.json'), 'utf-8'));
    expect(json.mcpServers[MCP_SERVER_NAME].url).toBe(expected);
  });

  it('the CLI server-entry name matches the shared Godot engine adapter (ai-game-developer)', () => {
    expect(MCP_SERVER_NAME).toBe(godotAdapter.serverName);
    expect(godotAdapter.serverName).toBe('ai-game-developer');
  });

  it('the canonical OAuth resource stays unpinned — the pin is routing-only (decision M8)', () => {
    // The pin lives ONLY in the connection URL; the OAuth identity resource is the bare canonical URL.
    expect(DEFAULT_HOSTED_MCP_URL).toBe('https://ai-game.dev/mcp');
    expect(DEFAULT_HOSTED_MCP_URL).not.toMatch(/\/p\/[0-9a-f]{8}/);
  });

  it('Godot adapter is http-only (M6 — no stdio this wave) with client id godot-cli', () => {
    expect(godotAdapter.stdioSupported).toBe(false);
    expect(godotAdapter.clientId).toBe('godot-cli');
  });
});
