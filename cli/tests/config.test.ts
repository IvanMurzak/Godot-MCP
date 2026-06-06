import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  getConfigPath,
  getOrCreateConfig,
  readConfig,
  updateFeatures,
  writeConfig,
  type GodotMcpFeaturesConfig,
} from '../src/utils/config.js';

describe('features config', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cfg-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('config path is project-local .godot-mcp/features.json', () => {
    expect(getConfigPath(tmpDir)).toBe(path.join(tmpDir, '.godot-mcp', 'features.json'));
  });

  it('returns null when no config exists', () => {
    expect(readConfig(tmpDir)).toBeNull();
  });

  it('getOrCreateConfig writes a default empty config', () => {
    const cfg = getOrCreateConfig(tmpDir);
    expect(cfg).toEqual({ tools: [], prompts: [], resources: [] });
    expect(fs.existsSync(getConfigPath(tmpDir))).toBe(true);
  });

  it('updateFeatures enables and disables named features', () => {
    const cfg: GodotMcpFeaturesConfig = { tools: [], prompts: [], resources: [] };
    updateFeatures(cfg, 'tools', { enableNames: ['a', 'b'] });
    expect(cfg.tools).toEqual([
      { name: 'a', enabled: true },
      { name: 'b', enabled: true },
    ]);
    updateFeatures(cfg, 'tools', { disableNames: ['a'] });
    expect(cfg.tools).toContainEqual({ name: 'a', enabled: false });
    expect(cfg.tools).toContainEqual({ name: 'b', enabled: true });
  });

  it('updateFeatures enableAll / disableAll flips every present feature', () => {
    const cfg: GodotMcpFeaturesConfig = {
      tools: [
        { name: 'a', enabled: false },
        { name: 'b', enabled: false },
      ],
    };
    updateFeatures(cfg, 'tools', { enableAll: true });
    expect(cfg.tools?.every((f) => f.enabled)).toBe(true);
    updateFeatures(cfg, 'tools', { disableAll: true });
    expect(cfg.tools?.every((f) => !f.enabled)).toBe(true);
  });

  it('round-trips through write/read', () => {
    const cfg = getOrCreateConfig(tmpDir);
    updateFeatures(cfg, 'resources', { enableNames: ['res-a'] });
    writeConfig(tmpDir, cfg);
    const reread = readConfig(tmpDir);
    expect(reread?.resources).toContainEqual({ name: 'res-a', enabled: true });
  });
});
