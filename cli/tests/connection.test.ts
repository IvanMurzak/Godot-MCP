import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  resolveConnection,
  resolveOpenAuthToken,
  CLOUD_MCP_URL,
  DEFAULT_CLOUD_BASE_URL,
  ENV_HOST,
  ENV_CLOUD_URL,
  ENV_TOKEN,
  ENV_CONNECTION_MODE,
} from '../src/utils/connection.js';
import { writeCredentials } from '../src/utils/credentials.js';
import { writeMachineCredentials, MACHINE_STORE_DIR_ENV } from '../src/utils/machine-credentials.js';
import { derivePortV2 } from '../src/utils/project-identity.js';
import { writeProjectMarker } from '../src/utils/project-marker.js';
import { runTool } from '../src/lib/run-tool.js';

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

  it('normalizes GODOT_MCP_CLOUD_URL to its /mcp hub URL (keeps an existing /mcp, appends when absent)', () => {
    // Fix A: the cloud target MUST retain /mcp so <base>/api/tools/<name> reaches
    // the hub, not the 404'ing backend. (Old behavior stripped /mcp — the defect.)
    process.env[ENV_CLOUD_URL] = 'https://example.test/mcp';
    expect(resolveConnection('/proj', {}).url).toBe('https://example.test/mcp');
    process.env[ENV_CLOUD_URL] = 'https://example.test';
    expect(resolveConnection('/proj', {}).url).toBe('https://example.test/mcp');
  });

  it('falls back to the cloud /mcp hub URL when mode is Cloud (fix A)', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe(CLOUD_MCP_URL);
    expect(url).toBe(`${DEFAULT_CLOUD_BASE_URL}/mcp`);
  });

  it('falls back to the v2 derived local port otherwise — no :8080 (fix B)', () => {
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe(`http://localhost:${derivePortV2('/proj')}`);
    expect(url).not.toContain('8080');
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

describe('resolveConnection — fix A: cloud run-tool targets the /mcp hub', () => {
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

  it('composes https://ai-game.dev/mcp/api/tools/<name> in Cloud mode', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url } = resolveConnection('/proj', {});
    expect(`${url}/api/tools/ping`).toBe('https://ai-game.dev/mcp/api/tools/ping');
  });

  it('runTool actually POSTs to https://ai-game.dev/mcp/api/tools/<name> (end-to-end)', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url, token } = resolveConnection('/proj', {});
    const fetchImpl = vi.fn(
      async () => new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }),
    );
    await runTool({
      toolName: 'ping',
      url,
      ...(token ? { token } : {}),
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const [endpoint] = fetchImpl.mock.calls[0] as [string];
    expect(endpoint).toBe('https://ai-game.dev/mcp/api/tools/ping');
  });
});

describe('resolveConnection — fix B: enrolled marker + v2 derived-port fallback', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE, MACHINE_STORE_DIR_ENV];
  let projectDir: string;
  let storeDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    projectDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-marker-proj-'));
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-marker-store-'));
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

  it('uses the v2 derived local port when there is no marker and no env (no :8080)', () => {
    const { url } = resolveConnection(projectDir, {});
    expect(url).toBe(`http://localhost:${derivePortV2(projectDir)}`);
    expect(url).not.toContain('8080');
  });

  it('respects an enrolled localhost marker target verbatim', () => {
    writeProjectMarker(projectDir, { serverTarget: 'http://localhost:23456', pin: 'abcdef01', port: 23456 });
    const { url, token } = resolveConnection(projectDir, {});
    expect(url).toBe('http://localhost:23456');
    // A localhost target is not the cloud hub → no persisted-token injection.
    expect(token).toBeUndefined();
  });

  it('honors an explicit marker portOverride in the derived-port fallback', () => {
    writeProjectMarker(projectDir, { portOverride: 25555 });
    const { url } = resolveConnection(projectDir, {});
    expect(url).toBe('http://localhost:25555');
  });

  it('reaches the cloud /mcp hub with a persisted token and ZERO env in an enrolled cloud project (DoD)', () => {
    // Enrolled hosted project: the marker records the hosted serverTarget and the
    // credential lives in the shared machine store — no env var is set anywhere.
    writeProjectMarker(projectDir, { serverTarget: DEFAULT_CLOUD_BASE_URL, pin: 'abcdef01' });
    writeMachineCredentials({ accessToken: 'enrolled-tok', serverTarget: DEFAULT_CLOUD_BASE_URL }, storeDir);
    const { url, token } = resolveConnection(projectDir, {});
    expect(url).toBe(CLOUD_MCP_URL); // https://ai-game.dev/mcp
    expect(token).toBe('enrolled-tok'); // zero-env cloud auth via the enrolled credential
    expect(`${url}/api/tools/ping`).toBe('https://ai-game.dev/mcp/api/tools/ping');
  });

  it('lets explicit env (GODOT_MCP_HOST) override the enrolled marker', () => {
    writeProjectMarker(projectDir, { serverTarget: DEFAULT_CLOUD_BASE_URL, pin: 'abcdef01' });
    process.env[ENV_HOST] = 'http://localhost:9999';
    expect(resolveConnection(projectDir, {}).url).toBe('http://localhost:9999');
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
