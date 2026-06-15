import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';
import { installPlugin, removePlugin } from '../src/lib/install-plugin.js';
import { GODOT_MCP_PLUGIN_PATH, parseEnabledPlugins } from '../src/utils/project-godot.js';

const MINIMAL_PROJECT = '; Engine configuration file.\nconfig_version=5\n\n[application]\n\nconfig/name="Test"\n';

describe('install-plugin / remove-plugin — lib logic', () => {
  let tmpDir: string;
  let projectGodot: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-plugin-lib-'));
    projectGodot = path.join(tmpDir, 'project.godot');
    fs.writeFileSync(projectGodot, MINIMAL_PROJECT);
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('installPlugin enables the addon and reports changed:true with the plugin in the list', async () => {
    const result = await installPlugin({ godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.changed).toBe(true);
      expect(result.enabledPlugins).toContain(GODOT_MCP_PLUGIN_PATH);
      // It must surface the NuGet PackageReference warning.
      expect(result.warnings.some((w) => w.includes('PackageReference'))).toBe(true);
    }
    expect(parseEnabledPlugins(fs.readFileSync(projectGodot, 'utf-8'))).toContain(GODOT_MCP_PLUGIN_PATH);
  });

  it('installPlugin is idempotent — re-enabling reports changed:false', async () => {
    await installPlugin({ godotProjectPath: tmpDir });
    const second = await installPlugin({ godotProjectPath: tmpDir });
    expect(second.kind).toBe('success');
    if (second.kind === 'success') expect(second.changed).toBe(false);
  });

  it('removePlugin disables a previously-enabled addon (changed:true)', async () => {
    await installPlugin({ godotProjectPath: tmpDir });
    const result = await removePlugin({ godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.changed).toBe(true);
      expect(result.enabledPlugins).not.toContain(GODOT_MCP_PLUGIN_PATH);
    }
    expect(parseEnabledPlugins(fs.readFileSync(projectGodot, 'utf-8'))).not.toContain(GODOT_MCP_PLUGIN_PATH);
  });

  it('removePlugin is idempotent — disabling an already-absent addon reports changed:false', async () => {
    const result = await removePlugin({ godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') expect(result.changed).toBe(false);
  });

  it('installPlugin returns a structured failure (never throws) for a non-Godot dir', async () => {
    const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-plugin-empty-'));
    try {
      const result = await installPlugin({ godotProjectPath: empty });
      expect(result.kind).toBe('failure');
      if (result.kind === 'failure') {
        expect(result.error).toBeInstanceOf(Error);
      }
    } finally {
      fs.rmSync(empty, { recursive: true, force: true });
    }
  });

  it('removePlugin returns a structured failure (never throws) for a non-Godot dir', async () => {
    const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-plugin-empty2-'));
    try {
      const result = await removePlugin({ godotProjectPath: empty });
      expect(result.kind).toBe('failure');
    } finally {
      fs.rmSync(empty, { recursive: true, force: true });
    }
  });
});

describe('remove-plugin — CLI smoke', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-rm-smoke-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['remove-plugin', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--path');
  });

  it('exits 1 when project.godot is missing', async () => {
    const { stdout, exitCode } = await runCliAsync(['remove-plugin', '--path', tmpDir]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Not a valid Godot project');
  });

  it('reports "was not enabled" when the addon is already absent (exit 0)', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), MINIMAL_PROJECT);
    const { stdout, exitCode } = await runCliAsync(['remove-plugin', '--path', tmpDir]);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('was not enabled');
  });

  it('disables an enabled addon and reports success (exit 0)', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), MINIMAL_PROJECT);
    await runCliAsync(['install-plugin', '--path', tmpDir]);
    const { stdout, exitCode } = await runCliAsync(['remove-plugin', '--path', tmpDir]);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('godot_mcp addon disabled');
    const text = fs.readFileSync(path.join(tmpDir, 'project.godot'), 'utf-8');
    expect(text).not.toContain(GODOT_MCP_PLUGIN_PATH);
  });

  it('accepts the positional [path] argument form', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), MINIMAL_PROJECT);
    await runCliAsync(['install-plugin', '--path', tmpDir]);
    const { exitCode } = await runCliAsync(['remove-plugin', tmpDir]);
    expect(exitCode).toBe(0);
  });
});
