import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';
import {
  readConfig,
  getConfigPath,
  CONFIG_RELATIVE_PATH,
  updateFeatures,
  createDefaultConfig,
  type GodotMcpFeaturesConfig,
} from '../src/utils/config.js';

describe('configure — config util (prompts/resources, malformed input)', () => {
  it('updateFeatures operates on prompts and resources, not just tools', () => {
    const config = createDefaultConfig();
    updateFeatures(config, 'prompts', { enableNames: ['p1'] });
    updateFeatures(config, 'resources', { disableNames: ['r1'] });
    expect(config.prompts).toEqual([{ name: 'p1', visible: true }]);
    expect(config.resources).toEqual([{ name: 'r1', visible: false }]);
  });

  it('updateFeatures tolerates a feature group that is not an array', () => {
    const config: GodotMcpFeaturesConfig = { tools: 'garbage' as unknown as never };
    updateFeatures(config, 'tools', { enableNames: ['only'] });
    expect(config.tools).toEqual([{ name: 'only', visible: true }]);
  });

  it('updateFeatures filters out malformed entries before applying changes', () => {
    const config: GodotMcpFeaturesConfig = {
      // a non-object, a wrong-typed name, and a valid entry
      tools: [42, { name: 5, visible: true }, { name: 'good', visible: false }] as unknown as never,
    };
    updateFeatures(config, 'tools', { enableNames: ['good'] });
    expect(config.tools).toEqual([{ name: 'good', visible: true }]);
  });

  it('config path is the project-local override file', () => {
    const p = getConfigPath('/some/proj');
    expect(p).toBe(path.join('/some/proj', CONFIG_RELATIVE_PATH));
  });
});

describe('configure — CLI smoke (depth beyond cli.test.ts)', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-cfg-deep-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('accepts the positional [path] argument form', async () => {
    const { exitCode } = await runCliAsync(['configure', tmpDir, '--enable-tools', 'pos-tool']);
    expect(exitCode).toBe(0);
    const cfg = readConfig(tmpDir);
    expect(cfg?.tools).toContainEqual({ name: 'pos-tool', visible: true });
  });

  it('enables and disables prompts', async () => {
    await runCliAsync(['configure', '--path', tmpDir, '--enable-prompts', 'pr-a,pr-b']);
    await runCliAsync(['configure', '--path', tmpDir, '--disable-prompts', 'pr-a']);
    const cfg = readConfig(tmpDir);
    expect(cfg?.prompts).toContainEqual({ name: 'pr-a', visible: false });
    expect(cfg?.prompts).toContainEqual({ name: 'pr-b', visible: true });
  });

  it('enables and disables resources', async () => {
    await runCliAsync(['configure', '--path', tmpDir, '--enable-resources', 'res-a']);
    const cfg = readConfig(tmpDir);
    expect(cfg?.resources).toContainEqual({ name: 'res-a', visible: true });
  });

  it('--disable-all-tools flips every present tool to disabled', async () => {
    await runCliAsync(['configure', '--path', tmpDir, '--enable-tools', 't1,t2']);
    await runCliAsync(['configure', '--path', tmpDir, '--disable-all-tools']);
    const { stdout } = await runCliAsync(['configure', '--path', tmpDir, '--list']);
    expect(stdout).toContain('[hidden] t1');
    expect(stdout).toContain('[hidden] t2');
  });

  it('--enable-all-tools flips every present tool to visible', async () => {
    await runCliAsync(['configure', '--path', tmpDir, '--disable-tools', 't1,t2']);
    await runCliAsync(['configure', '--path', tmpDir, '--enable-all-tools']);
    const { stdout } = await runCliAsync(['configure', '--path', tmpDir, '--list']);
    expect(stdout).toContain('[visible] t1');
    expect(stdout).toContain('[visible] t2');
  });

  it('--list with no overrides reports the all-visible-by-default state', async () => {
    const { stdout, exitCode } = await runCliAsync(['configure', '--path', tmpDir, '--list']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('(none configured - all visible by default)');
  });

  it('exits 1 when neither a positional path nor --path is provided', async () => {
    const { stdout, exitCode } = await runCliAsync(['configure', '--list']);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Path is required');
  });
});
