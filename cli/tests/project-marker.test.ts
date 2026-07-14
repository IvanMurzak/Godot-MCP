import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import {
  readProjectMarker,
  writeProjectMarker,
  getProjectMarkerPath,
  isLocalhostUrl,
  applyPinToUrl,
  upsertPinIntoAgentConfigs,
} from '../src/utils/project-marker.js';
import { derivePin } from '../src/utils/project-identity.js';

describe('project-marker — read/write merge', () => {
  let tmp: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-marker-'));
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  it('returns null when the marker is absent', () => {
    expect(readProjectMarker(tmp)).toBeNull();
  });

  it('writes the marker under .ai-game-dev/project.json', () => {
    writeProjectMarker(tmp, { serverTarget: 'https://ai-game.dev', pin: 'deadbeef' });
    expect(fs.existsSync(getProjectMarkerPath(tmp))).toBe(true);
    const marker = readProjectMarker(tmp);
    expect(marker?.serverTarget).toBe('https://ai-game.dev');
    expect(marker?.pin).toBe('deadbeef');
  });

  it('merges over an existing marker, preserving a prior portOverride + unknown keys', () => {
    writeProjectMarker(tmp, { portOverride: 27123, custom: 'keep-me' } as never);
    writeProjectMarker(tmp, { serverTarget: 'http://localhost:20001', pin: 'cafef00d', port: 20001 });
    const marker = readProjectMarker(tmp);
    expect(marker?.portOverride).toBe(27123);
    expect((marker as Record<string, unknown>).custom).toBe('keep-me');
    expect(marker?.serverTarget).toBe('http://localhost:20001');
    expect(marker?.port).toBe(20001);
  });

  it('a malformed marker reads as null (never throws)', () => {
    fs.mkdirSync(path.dirname(getProjectMarkerPath(tmp)), { recursive: true });
    fs.writeFileSync(getProjectMarkerPath(tmp), '{ not json');
    expect(readProjectMarker(tmp)).toBeNull();
  });
});

describe('project-marker — isLocalhostUrl', () => {
  it('recognizes loopback hosts', () => {
    expect(isLocalhostUrl('http://localhost:8080')).toBe(true);
    expect(isLocalhostUrl('http://127.0.0.1:20001/mcp')).toBe(true);
    expect(isLocalhostUrl('http://[::1]:5300')).toBe(true);
  });
  it('rejects hosted + malformed', () => {
    expect(isLocalhostUrl('https://ai-game.dev/mcp')).toBe(false);
    expect(isLocalhostUrl('not a url')).toBe(false);
  });
});

describe('project-marker — applyPinToUrl (D14 routing pin)', () => {
  it('appends /p/<pin> to a bare /mcp path', () => {
    expect(applyPinToUrl('https://ai-game.dev/mcp', 'deadbeef')).toBe('https://ai-game.dev/mcp/p/deadbeef');
  });
  it('replaces an existing /p/<pin> segment (idempotent re-enroll)', () => {
    expect(applyPinToUrl('https://ai-game.dev/mcp/p/00000000', 'deadbeef')).toBe(
      'https://ai-game.dev/mcp/p/deadbeef',
    );
  });
  it('works on a localhost URL with a port', () => {
    expect(applyPinToUrl('http://localhost:20001/mcp', 'cafef00d')).toBe('http://localhost:20001/mcp/p/cafef00d');
  });
  it('returns a malformed URL unchanged', () => {
    expect(applyPinToUrl('not a url', 'deadbeef')).toBe('not a url');
  });
});

describe('project-marker — upsertPinIntoAgentConfigs', () => {
  let tmp: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-pinup-'));
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  it("pins the ai-game-developer entry in an existing project-local .mcp.json (Claude Code)", () => {
    const pin = derivePin(tmp);
    const mcpJson = path.join(tmp, '.mcp.json');
    fs.writeFileSync(
      mcpJson,
      JSON.stringify({ mcpServers: { 'ai-game-developer': { type: 'http', url: 'https://ai-game.dev/mcp' } } }, null, 2),
    );
    const updated = upsertPinIntoAgentConfigs(tmp, pin);
    expect(updated).toContain(mcpJson);
    const root = JSON.parse(fs.readFileSync(mcpJson, 'utf-8'));
    expect(root.mcpServers['ai-game-developer'].url).toBe(`https://ai-game.dev/mcp/p/${pin}`);
  });

  it('leaves a config with no ai-game-developer entry untouched', () => {
    const pin = derivePin(tmp);
    const mcpJson = path.join(tmp, '.mcp.json');
    const original = JSON.stringify({ mcpServers: { other: { url: 'https://x.test' } } }, null, 2);
    fs.writeFileSync(mcpJson, original);
    const updated = upsertPinIntoAgentConfigs(tmp, pin);
    expect(updated).not.toContain(mcpJson);
    expect(fs.readFileSync(mcpJson, 'utf-8')).toBe(original);
  });

  it('pins the Antigravity-style serverUrl key too (when project-local)', () => {
    // Antigravity's config is user-global by default and thus skipped; assert the
    // serverUrl key is handled by pinning a project-local Custom-style config that
    // carries serverUrl.
    const pin = derivePin(tmp);
    const custom = path.join(tmp, 'mcp.json'); // the `custom` agent's project-local path
    fs.writeFileSync(
      custom,
      JSON.stringify({ mcpServers: { 'ai-game-developer': { serverUrl: 'http://localhost:20001/mcp' } } }, null, 2),
    );
    const updated = upsertPinIntoAgentConfigs(tmp, pin);
    expect(updated).toContain(custom);
    const root = JSON.parse(fs.readFileSync(custom, 'utf-8'));
    expect(root.mcpServers['ai-game-developer'].serverUrl).toBe(`http://localhost:20001/mcp/p/${pin}`);
  });

  it('skips user-global configs (never rewrites files outside the project)', () => {
    // No project-local config present → nothing updated, and no throw.
    expect(upsertPinIntoAgentConfigs(tmp, derivePin(tmp))).toEqual([]);
  });
});
