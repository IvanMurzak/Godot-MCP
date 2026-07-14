import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  resolveConnection,
  resolveOpenAuthToken,
  CLOUD_MCP_URL,
  DEFAULT_CLOUD_BASE_URL,
  DEFAULT_CUSTOM_HOST,
  ENV_HOST,
  ENV_CLOUD_URL,
  ENV_TOKEN,
  ENV_CONNECTION_MODE,
} from '../src/utils/connection.js';
import { writeCredentials } from '../src/utils/credentials.js';
import { writeMachineCredentials, MACHINE_STORE_DIR_ENV } from '../src/utils/machine-credentials.js';

describe('resolveConnection', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE];

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
  });

  it('uses an explicit --url (trailing slash stripped) over everything', () => {
    process.env[ENV_HOST] = 'http://env-host:1111';
    const { url } = resolveConnection('/proj', { url: 'http://explicit:9000/' });
    expect(url).toBe('http://explicit:9000');
  });

  it('uses GODOT_MCP_HOST when no --url is given', () => {
    process.env[ENV_HOST] = 'http://localhost:5544/';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe('http://localhost:5544');
  });

  it('strips a trailing /mcp from GODOT_MCP_CLOUD_URL to get the base', () => {
    process.env[ENV_CLOUD_URL] = 'https://example.test/mcp';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe('https://example.test');
  });

  it('falls back to the cloud base when mode is Cloud', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe(DEFAULT_CLOUD_BASE_URL);
  });

  it('falls back to the default custom host otherwise', () => {
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe(DEFAULT_CUSTOM_HOST);
  });

  it('resolves the token from --token, then GODOT_MCP_TOKEN', () => {
    process.env[ENV_TOKEN] = '"env-tok"';
    expect(resolveConnection('/proj', { token: 'flag-tok' }).token).toBe('flag-tok');
    // env value is normalized (wrapping quotes stripped)
    expect(resolveConnection('/proj', {}).token).toBe('env-tok');
  });

  it('exposes the cloud MCP-client URL constant as <base>/mcp', () => {
    expect(CLOUD_MCP_URL).toBe(`${DEFAULT_CLOUD_BASE_URL}/mcp`);
  });
});

describe('resolveConnection — persisted cloud token fallback', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE];
  let tmpDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-conn-'));
    writeCredentials(tmpDir, { cloudToken: 'persisted-tok', cloudBaseUrl: DEFAULT_CLOUD_BASE_URL });
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('falls back to the persisted cloud token in Cloud mode when no --token / env token', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(resolveConnection(tmpDir, {}).token).toBe('persisted-tok');
  });

  it('does NOT use the persisted token outside Cloud mode', () => {
    expect(resolveConnection(tmpDir, {}).token).toBeUndefined();
  });

  it('lets --token and GODOT_MCP_TOKEN win over the persisted token', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(resolveConnection(tmpDir, { token: 'flag-tok' }).token).toBe('flag-tok');
    process.env[ENV_TOKEN] = 'env-tok';
    expect(resolveConnection(tmpDir, {}).token).toBe('env-tok');
  });
});

describe('resolveOpenAuthToken', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_TOKEN, ENV_CONNECTION_MODE];
  let tmpDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-open-'));
    writeCredentials(tmpDir, { cloudToken: 'persisted-tok' });
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('returns an explicit --token verbatim', () => {
    expect(resolveOpenAuthToken(tmpDir, { token: 'flag-tok', mode: 'Cloud' })).toBe('flag-tok');
  });

  it('returns undefined when GODOT_MCP_TOKEN is set (env propagates naturally)', () => {
    process.env[ENV_TOKEN] = 'env-tok';
    expect(resolveOpenAuthToken(tmpDir, { mode: 'Cloud' })).toBeUndefined();
  });

  it('returns the persisted cloud token when --mode Cloud and no token', () => {
    expect(resolveOpenAuthToken(tmpDir, { mode: 'Cloud' })).toBe('persisted-tok');
  });

  it('honors GODOT_MCP_CONNECTION_MODE=Cloud when --mode is absent', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(resolveOpenAuthToken(tmpDir, {})).toBe('persisted-tok');
  });

  it('returns undefined in Custom mode', () => {
    expect(resolveOpenAuthToken(tmpDir, { mode: 'Custom' })).toBeUndefined();
  });
});

describe('token readers — shared machine-store fallback', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE, MACHINE_STORE_DIR_ENV];
  let projectDir: string;
  let storeDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    projectDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-conn-proj-'));
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-conn-store-'));
    // Redirect the machine store to a temp dir so the real ~/.ai-game-dev is never read.
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(projectDir, { recursive: true, force: true });
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it('resolveConnection falls back to the machine store when there is no project token (Cloud mode)', () => {
    writeMachineCredentials({ accessToken: 'machine-tok', serverTarget: DEFAULT_CLOUD_BASE_URL }, storeDir);
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(resolveConnection(projectDir, {}).token).toBe('machine-tok');
  });

  it('resolveConnection prefers the project-local token over the machine store', () => {
    writeCredentials(projectDir, { cloudToken: 'project-tok', cloudBaseUrl: DEFAULT_CLOUD_BASE_URL });
    writeMachineCredentials({ accessToken: 'machine-tok', serverTarget: DEFAULT_CLOUD_BASE_URL }, storeDir);
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(resolveConnection(projectDir, {}).token).toBe('project-tok');
  });

  it('resolveOpenAuthToken falls back to the machine store in Cloud mode', () => {
    writeMachineCredentials({ accessToken: 'machine-tok' }, storeDir);
    expect(resolveOpenAuthToken(projectDir, { mode: 'Cloud' })).toBe('machine-tok');
  });

  it('does not use the machine store outside Cloud mode', () => {
    writeMachineCredentials({ accessToken: 'machine-tok' }, storeDir);
    expect(resolveConnection(projectDir, {}).token).toBeUndefined();
    expect(resolveOpenAuthToken(projectDir, { mode: 'Custom' })).toBeUndefined();
  });
});
