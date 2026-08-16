import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as http from 'http';
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
import { getCredentialsPath } from '../src/utils/credentials.js';
import { MACHINE_STORE_DIR_ENV } from '../src/utils/machine-store.js';
import { derivePortV2 } from '../src/utils/project-identity.js';
import { writeProjectMarker } from '../src/utils/project-marker.js';
import { runTool } from '../src/lib/run-tool.js';
import { MachineCredentialStore, type MachineCredentials } from '@baizor/gamedev-cli-core';

/**
 * Seed a LEGACY `<project>/.godot-mcp/credentials.json` file the way an old CLI release left it
 * on disk. The CLI itself no longer has a write path for this sink (06 D7) — raw `fs` only.
 */
function seedLegacyStore(projectPath: string, credentials: Record<string, unknown>): void {
  const credentialsPath = getCredentialsPath(projectPath);
  fs.mkdirSync(path.dirname(credentialsPath), { recursive: true });
  fs.writeFileSync(credentialsPath, JSON.stringify(credentials, null, 2) + '\n');
}

/** Seed a cli-core credential store at `dir`. */
function seedStore(dir: string, credentials: MachineCredentials): void {
  new MachineCredentialStore(dir).write(credentials);
}

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

  it('uses an explicit --url (trailing slash stripped) over everything', async () => {
    process.env[ENV_HOST] = 'http://env-host:1111';
    const { url } = await resolveConnection('/proj', { url: 'http://explicit:9000/' });
    expect(url).toBe('http://explicit:9000');
  });

  it('uses GODOT_MCP_HOST when no --url is given', async () => {
    process.env[ENV_HOST] = 'http://localhost:5544/';
    const { url } = await resolveConnection('/proj', {});
    expect(url).toBe('http://localhost:5544');
  });

  it('normalizes GODOT_MCP_CLOUD_URL to its /mcp hub URL (keeps an existing /mcp, appends when absent)', async () => {
    // Fix A: the cloud target MUST retain /mcp so <base>/api/tools/<name> reaches
    // the hub, not the 404'ing backend. (Old behavior stripped /mcp — the defect.)
    process.env[ENV_CLOUD_URL] = 'https://example.test/mcp';
    expect((await resolveConnection('/proj', {})).url).toBe('https://example.test/mcp');
    process.env[ENV_CLOUD_URL] = 'https://example.test';
    expect((await resolveConnection('/proj', {})).url).toBe('https://example.test/mcp');
  });

  it('falls back to the cloud /mcp hub URL when mode is Cloud (fix A)', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url } = await resolveConnection('/proj', {});
    expect(url).toBe(CLOUD_MCP_URL);
    expect(url).toBe(`${DEFAULT_CLOUD_BASE_URL}/mcp`);
  });

  it('falls back to the v2 derived local port otherwise — no :8080 (fix B)', async () => {
    const { url } = await resolveConnection('/proj', {});
    expect(url).toBe(`http://localhost:${derivePortV2('/proj')}`);
    expect(url).not.toContain('8080');
  });

  it('resolves the token from --token, then GODOT_MCP_TOKEN', async () => {
    process.env[ENV_TOKEN] = '"env-tok"';
    expect((await resolveConnection('/proj', { token: 'flag-tok' })).token).toBe('flag-tok');
    // env value is normalized (wrapping quotes stripped)
    expect((await resolveConnection('/proj', {})).token).toBe('env-tok');
  });

  it('exposes the cloud MCP-client URL constant as <base>/mcp', () => {
    expect(CLOUD_MCP_URL).toBe(`${DEFAULT_CLOUD_BASE_URL}/mcp`);
  });
});

describe('resolveConnection — fix A: cloud run-tool targets the /mcp hub', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE, MACHINE_STORE_DIR_ENV];
  let storeDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    // Redirect the machine store to an (empty) temp dir so the real ~/.ai-game-dev is never read.
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-fixa-store-'));
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it('composes https://ai-game.dev/mcp/api/tools/<name> in Cloud mode', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url } = await resolveConnection('/proj', {});
    expect(`${url}/api/tools/ping`).toBe('https://ai-game.dev/mcp/api/tools/ping');
  });

  it('runTool actually POSTs to https://ai-game.dev/mcp/api/tools/<name> (end-to-end)', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url, token } = await resolveConnection('/proj', {});
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

  it('uses the v2 derived local port when there is no marker and no env (no :8080)', async () => {
    const { url } = await resolveConnection(projectDir, {});
    expect(url).toBe(`http://localhost:${derivePortV2(projectDir)}`);
    expect(url).not.toContain('8080');
  });

  it('respects an enrolled localhost marker target verbatim', async () => {
    writeProjectMarker(projectDir, { serverTarget: 'http://localhost:23456', pin: 'abcdef01', port: 23456 });
    const { url, token } = await resolveConnection(projectDir, {});
    expect(url).toBe('http://localhost:23456');
    // A localhost target is not the cloud hub → no persisted-token injection.
    expect(token).toBeUndefined();
  });

  it('honors an explicit marker portOverride in the derived-port fallback', async () => {
    writeProjectMarker(projectDir, { portOverride: 25555 });
    const { url } = await resolveConnection(projectDir, {});
    expect(url).toBe('http://localhost:25555');
  });

  it('reaches the cloud /mcp hub with a persisted token and ZERO env in an enrolled cloud project (DoD)', async () => {
    // Enrolled hosted project: the marker records the hosted serverTarget and the
    // credential lives in the shared machine store — no env var is set anywhere.
    writeProjectMarker(projectDir, { serverTarget: DEFAULT_CLOUD_BASE_URL, pin: 'abcdef01' });
    seedStore(storeDir, { accessToken: 'enrolled-tok', serverTarget: DEFAULT_CLOUD_BASE_URL });
    const { url, token } = await resolveConnection(projectDir, {});
    expect(url).toBe(CLOUD_MCP_URL); // https://ai-game.dev/mcp
    expect(token).toBe('enrolled-tok'); // zero-env cloud auth via the enrolled credential
    expect(`${url}/api/tools/ping`).toBe('https://ai-game.dev/mcp/api/tools/ping');
  });

  it('lets explicit env (GODOT_MCP_HOST) override the enrolled marker', async () => {
    writeProjectMarker(projectDir, { serverTarget: DEFAULT_CLOUD_BASE_URL, pin: 'abcdef01' });
    process.env[ENV_HOST] = 'http://localhost:9999';
    expect((await resolveConnection(projectDir, {})).url).toBe('http://localhost:9999');
  });
});

describe('resolveConnection — legacy project store read-fallback + migrate-on-touch (06 D7 / F11.2)', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE, MACHINE_STORE_DIR_ENV];
  let tmpDir: string;
  let storeDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-conn-'));
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-conn-mstore-'));
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
    seedLegacyStore(tmpDir, { cloudToken: 'persisted-tok', cloudBaseUrl: DEFAULT_CLOUD_BASE_URL });
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it('falls back to the legacy persisted cloud token in Cloud mode when no --token / env token', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect((await resolveConnection(tmpDir, {})).token).toBe('persisted-tok');
  });

  it('MIGRATES the legacy credential into the empty machine store on touch (families.legacy + v1 mirror)', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(new MachineCredentialStore(storeDir).read()).toBeNull();

    const { token } = await resolveConnection(tmpDir, {});
    expect(token).toBe('persisted-tok');

    // On touch: the legacy credential was adopted into the machine store, under the lock,
    // as a legacy family (mint client unknown by definition), with the v1 compat mirror.
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.version).toBe(2);
    expect(doc?.families?.legacy?.accessToken).toBe('persisted-tok');
    expect(doc?.families?.legacy?.clientId).toBeUndefined();
    expect(doc?.accessToken).toBe('persisted-tok'); // v1 mirror (legacy IS the plugin plane here)
    expect(doc?.serverTarget).toBe(DEFAULT_CLOUD_BASE_URL);

    // The legacy file itself is left in place — the read-fallback stays for this release (f4 removes).
    expect(fs.existsSync(getCredentialsPath(tmpDir))).toBe(true);
  });

  it('does NOT migrate (or overwrite) when the machine store already holds a credential', async () => {
    seedStore(storeDir, { accessToken: 'machine-tok', subject: 'machine-user', serverTarget: DEFAULT_CLOUD_BASE_URL });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';

    // Status quo preserved: the project-local legacy token still wins on reads...
    expect((await resolveConnection(tmpDir, {})).token).toBe('persisted-tok');

    // ...and the machine store was NOT touched by any migration.
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.accessToken).toBe('machine-tok');
    expect(doc?.families).toBeUndefined();
  });

  it('does NOT use the persisted token outside Cloud mode', async () => {
    expect((await resolveConnection(tmpDir, {})).token).toBeUndefined();
  });

  it('lets --token and GODOT_MCP_TOKEN win over the persisted token', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect((await resolveConnection(tmpDir, { token: 'flag-tok' })).token).toBe('flag-tok');
    process.env[ENV_TOKEN] = 'env-tok';
    expect((await resolveConnection(tmpDir, {})).token).toBe('env-tok');
  });
});

describe('resolveOpenAuthToken', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_TOKEN, ENV_CONNECTION_MODE, MACHINE_STORE_DIR_ENV];
  let tmpDir: string;
  let storeDir: string;

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-open-'));
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-open-mstore-'));
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
    seedLegacyStore(tmpDir, { cloudToken: 'persisted-tok' });
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it('returns an explicit --token verbatim', async () => {
    expect(await resolveOpenAuthToken(tmpDir, { token: 'flag-tok', mode: 'Cloud' })).toBe('flag-tok');
  });

  it('returns undefined when GODOT_MCP_TOKEN is set (env propagates naturally)', async () => {
    process.env[ENV_TOKEN] = 'env-tok';
    expect(await resolveOpenAuthToken(tmpDir, { mode: 'Cloud' })).toBeUndefined();
  });

  it('returns the persisted cloud token when --mode Cloud and no token', async () => {
    expect(await resolveOpenAuthToken(tmpDir, { mode: 'Cloud' })).toBe('persisted-tok');
  });

  it('honors GODOT_MCP_CONNECTION_MODE=Cloud when --mode is absent', async () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect(await resolveOpenAuthToken(tmpDir, {})).toBe('persisted-tok');
  });

  it('returns undefined in Custom mode', async () => {
    expect(await resolveOpenAuthToken(tmpDir, { mode: 'Custom' })).toBeUndefined();
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

  it('resolveConnection falls back to the machine store when there is no project token (Cloud mode)', async () => {
    seedStore(storeDir, { accessToken: 'machine-tok', serverTarget: DEFAULT_CLOUD_BASE_URL });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect((await resolveConnection(projectDir, {})).token).toBe('machine-tok');
  });

  it('resolveConnection prefers the project-local legacy token over the machine store', async () => {
    seedLegacyStore(projectDir, { cloudToken: 'project-tok', cloudBaseUrl: DEFAULT_CLOUD_BASE_URL });
    seedStore(storeDir, { accessToken: 'machine-tok', serverTarget: DEFAULT_CLOUD_BASE_URL });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect((await resolveConnection(projectDir, {})).token).toBe('project-tok');
  });

  it('resolveConnection prefers a per-project store (login --project) over legacy and machine', async () => {
    seedStore(path.join(projectDir, '.ai-game-dev'), {
      version: 2,
      serverTarget: DEFAULT_CLOUD_BASE_URL,
      families: {
        plugin: { accessToken: 'per-project-tok', clientId: 'godot-cli', scope: 'mcp:plugin' },
      },
      accessToken: 'per-project-tok',
    });
    seedLegacyStore(projectDir, { cloudToken: 'legacy-tok', cloudBaseUrl: DEFAULT_CLOUD_BASE_URL });
    seedStore(storeDir, { accessToken: 'machine-tok', serverTarget: DEFAULT_CLOUD_BASE_URL });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    expect((await resolveConnection(projectDir, {})).token).toBe('per-project-tok');
  });

  it('an UNREADABLE per-project store is surfaced, never silently shadowed by the machine account (A1 / F11.4)', async () => {
    // A per-project store file that can be neither DPAPI-decrypted (Windows) nor JSON-parsed
    // (POSIX) — cli-core reads it as the structured "unreadable" state on both platforms.
    const projectStoreDir = path.join(projectDir, '.ai-game-dev');
    fs.mkdirSync(projectStoreDir, { recursive: true });
    fs.writeFileSync(path.join(projectStoreDir, 'credentials.json'), 'garbage-not-json-not-dpapi');
    seedStore(storeDir, { accessToken: 'machine-tok', serverTarget: DEFAULT_CLOUD_BASE_URL });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';

    const { token } = await resolveConnection(projectDir, {});

    // The machine credential must NOT be used — that would run the command as a different
    // account than the project chose; the user is told to re-authorize instead.
    expect(token).toBeUndefined();
    // The unreadable file is left untouched (04 §1: never deleted/overwritten by a read path).
    expect(fs.readFileSync(path.join(projectStoreDir, 'credentials.json'), 'utf-8')).toBe(
      'garbage-not-json-not-dpapi',
    );
  });

  it('resolveOpenAuthToken falls back to the machine store in Cloud mode', async () => {
    seedStore(storeDir, { accessToken: 'machine-tok' });
    expect(await resolveOpenAuthToken(projectDir, { mode: 'Cloud' })).toBe('machine-tok');
  });

  it('does not use the machine store outside Cloud mode', async () => {
    seedStore(storeDir, { accessToken: 'machine-tok' });
    expect((await resolveConnection(projectDir, {})).token).toBeUndefined();
    expect(await resolveOpenAuthToken(projectDir, { mode: 'Custom' })).toBeUndefined();
  });
});

describe('resolveConnection — works THROUGH expiry via the cli-core provider (DoD, fake AS)', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE, MACHINE_STORE_DIR_ENV];
  let projectDir: string;
  let storeDir: string;
  let server: http.Server;
  let asBaseUrl: string;
  let tokenRequests: Array<Record<string, string>>;

  beforeEach(async () => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
    projectDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-exp-proj-'));
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-exp-store-'));
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
    tokenRequests = [];

    // A minimal fake authorization server: answers POST /oauth/token (refresh_token grant)
    // with a rotated token pair, recording every form body it received.
    server = http.createServer((req, res) => {
      let body = '';
      req.on('data', (chunk) => (body += chunk));
      req.on('end', () => {
        if (req.method === 'POST' && req.url === '/oauth/token') {
          tokenRequests.push(Object.fromEntries(new URLSearchParams(body).entries()));
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(
            JSON.stringify({
              access_token: 'fresh-tok',
              refresh_token: 'rotated-refresh',
              token_type: 'Bearer',
              expires_in: 3600,
            }),
          );
          return;
        }
        res.writeHead(404);
        res.end();
      });
    });
    await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
    const address = server.address();
    if (address === null || typeof address === 'string') throw new Error('no address');
    asBaseUrl = `http://127.0.0.1:${address.port}`;
  });

  afterEach(async () => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
    await new Promise<void>((resolve) => server.close(() => resolve()));
    fs.rmSync(projectDir, { recursive: true, force: true });
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it('refreshes an EXPIRED machine credential under the lock and returns the fresh token', async () => {
    // The stored plugin-family access token expired an hour ago; only the refresh token is alive.
    seedStore(storeDir, {
      version: 2,
      serverTarget: asBaseUrl,
      subject: 'user-a',
      families: {
        plugin: {
          accessToken: 'expired-tok',
          refreshToken: 'live-refresh',
          expiresAt: new Date(Date.now() - 3600_000).toISOString(),
          clientId: 'godot-cli',
          scope: 'mcp:plugin',
        },
      },
      accessToken: 'expired-tok',
    });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';

    const { token } = await resolveConnection(projectDir, {});

    // The CLI worked THROUGH expiry: the provider refreshed against the (fake) AS and handed
    // out the rotated token — never the stale one.
    expect(token).toBe('fresh-tok');

    // The refresh presented the family's stored clientId with grant_type=refresh_token, and
    // omitted `scope`/`resource` entirely (04 §3 rules 2-3).
    expect(tokenRequests).toHaveLength(1);
    expect(tokenRequests[0]).toMatchObject({
      grant_type: 'refresh_token',
      refresh_token: 'live-refresh',
      client_id: 'godot-cli',
    });
    expect(tokenRequests[0]).not.toHaveProperty('scope');
    expect(tokenRequests[0]).not.toHaveProperty('resource');

    // The rotation was persisted (plugin family + v1 mirror) — the next call needs no network.
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.families?.plugin?.accessToken).toBe('fresh-tok');
    expect(doc?.families?.plugin?.refreshToken).toBe('rotated-refresh');
    expect(doc?.accessToken).toBe('fresh-tok');

    const second = await resolveConnection(projectDir, {});
    expect(second.token).toBe('fresh-tok');
    expect(tokenRequests).toHaveLength(1); // still one network refresh — the store served the second call
  });

  it('returns a still-valid token without any network round trip', async () => {
    seedStore(storeDir, {
      version: 2,
      serverTarget: asBaseUrl,
      families: {
        plugin: {
          accessToken: 'valid-tok',
          refreshToken: 'live-refresh',
          expiresAt: new Date(Date.now() + 3600_000).toISOString(),
          clientId: 'godot-cli',
          scope: 'mcp:plugin',
        },
      },
      accessToken: 'valid-tok',
    });
    process.env[ENV_CONNECTION_MODE] = 'Cloud';

    const { token } = await resolveConnection(projectDir, {});
    expect(token).toBe('valid-tok');
    expect(tokenRequests).toHaveLength(0);
  });
});
