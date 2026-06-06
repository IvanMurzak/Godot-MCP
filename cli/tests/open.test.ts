import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { spawn } from 'child_process';
import { buildOpenEnv, isGodotProjectDir, openProject } from '../src/lib/open.js';

// ESM module namespaces are non-configurable; mock child_process so we can
// observe spawn and stub the process-enumeration calls (execFileSync/execSync)
// used by the already-running check. The stubs return empty so no editor is
// detected as already running.
vi.mock('child_process', () => ({
  spawn: vi.fn(),
  execFileSync: vi.fn(() => ''),
  execSync: vi.fn(() => ''),
}));

const spawnMock = vi.mocked(spawn);

describe('buildOpenEnv', () => {
  it('returns undefined when noConnect is true', () => {
    expect(buildOpenEnv({ noConnect: true, url: 'http://localhost:8080' })).toBeUndefined();
  });

  it('returns undefined when no connection inputs are provided', () => {
    expect(buildOpenEnv({})).toBeUndefined();
  });

  it('maps each input to the matching GODOT_MCP_* env var', () => {
    const env = buildOpenEnv({
      url: 'http://localhost:8080',
      cloudUrl: 'https://ai-game.dev',
      token: 'abc',
      auth: 'Required',
      mode: 'Custom',
      logLevel: 'Trace',
    });
    expect(env).toEqual({
      GODOT_MCP_HOST: 'http://localhost:8080',
      GODOT_MCP_CLOUD_URL: 'https://ai-game.dev',
      GODOT_MCP_TOKEN: 'abc',
      GODOT_MCP_AUTH_OPTION: 'Required',
      GODOT_MCP_CONNECTION_MODE: 'Custom',
      GODOT_MCP_LOG_LEVEL: 'Trace',
    });
  });

  it('throws on an invalid auth value', () => {
    expect(() => buildOpenEnv({ auth: 'bogus' as never })).toThrow(/auth must be/);
  });

  it('throws on an invalid mode value', () => {
    expect(() => buildOpenEnv({ mode: 'bogus' as never })).toThrow(/mode must be/);
  });
});

describe('isGodotProjectDir', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-open-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('is true when project.godot exists', () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    expect(isGodotProjectDir(tmpDir)).toBe(true);
  });

  it('is false when project.godot is absent', () => {
    expect(isGodotProjectDir(tmpDir)).toBe(false);
  });
});

describe('openProject', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-open-proj-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('fails for a non-Godot directory', async () => {
    const result = await openProject({ projectPath: tmpDir, editorPath: 'x', noConnect: true });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('Not a Godot project');
    }
  });

  it('fails when no editor can be resolved', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    const result = await openProject({
      projectPath: tmpDir,
      editorPath: path.join(tmpDir, 'does-not-exist'),
      noConnect: true,
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('No Godot editor binary found');
    }
  });

  it('launches the resolved editor with --editor --path + GODOT_MCP_* env', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    const fakeBin = path.join(tmpDir, 'godot-bin');
    fs.writeFileSync(fakeBin, '');

    const on = vi.fn();
    const unref = vi.fn();
    const fakeChild = { on, unref, pid: 9999, once: vi.fn() } as unknown as ReturnType<typeof spawn>;
    spawnMock.mockReset();
    spawnMock.mockReturnValue(fakeChild);

    const result = await openProject({
      projectPath: tmpDir,
      editorPath: fakeBin,
      url: 'http://localhost:8080',
      token: 'secret',
    });

    expect(result.kind).toBe('success');
    expect(spawnMock).toHaveBeenCalledTimes(1);
    const [bin, args, opts] = spawnMock.mock.calls[0];
    expect(bin).toBe(path.resolve(fakeBin));
    expect(args).toEqual(['--editor', '--path', path.resolve(tmpDir)]);
    const env = (opts as { env: Record<string, string> }).env;
    expect(env['GODOT_MCP_HOST']).toBe('http://localhost:8080');
    expect(env['GODOT_MCP_TOKEN']).toBe('secret');
  });
});
