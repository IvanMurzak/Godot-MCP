import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCloudLogin } from '../src/utils/cloud-login.js';
import { getCredentialsPath } from '../src/utils/credentials.js';
import { MACHINE_STORE_DIR_ENV } from '../src/utils/machine-store.js';
import {
  MachineCredentialStore,
  type DeviceAuthTransport,
  type DeviceAuthorizeResponse,
  type DeviceTokenResponse,
  type TokenExchangeClient,
  type TokenExchangeRequest,
} from '@baizor/gamedev-cli-core';

const AUTHORIZE_OK: DeviceAuthorizeResponse = {
  device_code: 'dev-code',
  user_code: 'CODE-1234',
  verification_uri: 'https://example.test/device',
  verification_uri_complete: 'https://example.test/device?code=CODE-1234',
  expires_in: 900,
  interval: 5,
};

/** Base64url-encode (for building unsigned test JWTs whose `sub` the flow can decode). */
function b64url(input: string): string {
  return Buffer.from(input, 'utf8').toString('base64url');
}

/** A structurally valid (unsigned) JWT carrying `sub` — enough for `decodeJwtSubject`. */
function makeJwt(sub: string): string {
  return `${b64url(JSON.stringify({ alg: 'ES256', typ: 'JWT' }))}.${b64url(JSON.stringify({ sub }))}.sig`;
}

/**
 * Build a mock core {@link DeviceAuthTransport}: the device-authorization request always yields
 * AUTHORIZE_OK; each poll returns the next queued token response (a success body, or an RFC 6749
 * §5.2 soft error like `access_denied`). Once the queue drains it reports `authorization_pending`.
 */
function transport(polls: DeviceTokenResponse[]): DeviceAuthTransport {
  const queue = [...polls];
  return {
    requestDeviceCode: async () => AUTHORIZE_OK,
    pollToken: async () => queue.shift() ?? { error: 'authorization_pending' },
  };
}

const AGENT_TOKEN = (accessToken: string): DeviceTokenResponse => ({
  access_token: accessToken,
  refresh_token: 'agent-refresh',
  token_type: 'Bearer',
  expires_in: 3600,
  scope: 'mcp:agent',
});

const PLUGIN_TOKEN = (accessToken: string): DeviceTokenResponse => ({
  access_token: accessToken,
  refresh_token: 'plugin-refresh',
  token_type: 'Bearer',
  expires_in: 3600,
  scope: 'mcp:plugin',
});

/** A fake RFC 8693 exchange client: scripted results, calls recorded. */
function fakeExchange(
  results: Array<{ ok: true; accessToken: string } | { ok: false; reason: string }>,
): TokenExchangeClient & { calls: TokenExchangeRequest[] } {
  const queue = [...results];
  const calls: TokenExchangeRequest[] = [];
  return {
    calls,
    exchange: async (request) => {
      calls.push(request);
      const next = queue.shift() ?? { ok: false as const, reason: 'exchange queue drained' };
      if (!next.ok) return next;
      return {
        ok: true,
        accessToken: next.accessToken,
        refreshToken: 'derived-refresh',
        expiresAt: '2030-01-01T00:00:00.000Z',
        scope: 'mcp:plugin',
        sub: 'user-a',
      };
    },
  };
}

/** A 200-OK fetch spy (serves the RFC 7009 revocation POSTs the commit flow issues). */
function fetchSpy() {
  return vi.fn(async () => new Response('{}', { status: 200 }));
}

describe('runCloudLogin — agent login (F1: agent family + derived plugin family)', () => {
  let storeDir: string;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-machine-'));
  });
  afterEach(() => {
    fs.rmSync(storeDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('commits the agent family, derives the plugin family, and mirrors it (v2 document)', async () => {
    const agentJwt = makeJwt('user-a');
    const exchange = fakeExchange([{ ok: true, accessToken: 'plugin-tok' }]);
    const openBrowserImpl = vi.fn();
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl,
      transport: transport([AGENT_TOKEN(agentJwt)]),
      exchangeClient: exchange,
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBe('plugin-tok');
    expect(openBrowserImpl).toHaveBeenCalledWith('https://example.test/device?code=CODE-1234');

    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.version).toBe(2);
    // Agent family: the raw mint, stamped with this CLI's own client id + agent scope.
    expect(doc?.families?.agent?.accessToken).toBe(agentJwt);
    expect(doc?.families?.agent?.refreshToken).toBe('agent-refresh');
    expect(doc?.families?.agent?.clientId).toBe('godot-cli');
    expect(doc?.families?.agent?.scope).toBe('mcp:agent');
    // Plugin family: derived via the RFC 8693 exchange, own client id + plugin scope.
    expect(doc?.families?.plugin?.accessToken).toBe('plugin-tok');
    expect(doc?.families?.plugin?.clientId).toBe('godot-cli');
    expect(doc?.families?.plugin?.scope).toBe('mcp:plugin');
    // v1 compat mirror = the plugin family (old readers key on the top-level triple).
    expect(doc?.accessToken).toBe('plugin-tok');
    expect(doc?.serverTarget).toBe('https://example.test');
    expect(doc?.subject).toBe('user-a');
    // The exchange presented the fresh agent token + this CLI's own client id.
    expect(exchange.calls[0]?.subjectToken).toBe(agentJwt);
    expect(exchange.calls[0]?.clientId).toBe('godot-cli');
  });

  it('defaults to the machine store when no sink is supplied (env-override redirected)', async () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
    try {
      const token = await runCloudLogin({
        baseUrl: 'https://example.test',
        openBrowserImpl: vi.fn(),
        transport: transport([AGENT_TOKEN(makeJwt('user-a'))]),
        exchangeClient: fakeExchange([{ ok: true, accessToken: 'default-tok' }]),
        delay: async () => {},
      });
      expect(token).toBe('default-tok');
      expect(new MachineCredentialStore(storeDir).read()?.families?.plugin?.accessToken).toBe('default-tok');
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });

  it('returns null and writes nothing when authorization is denied', async () => {
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([{ error: 'access_denied' }]),
      exchangeClient: fakeExchange([]),
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBeNull();
    expect(fs.existsSync(path.join(storeDir, 'credentials.json'))).toBe(false);
  });

  it('keeps the committed agent family and retries the derivation once on an exchange failure', async () => {
    const exchange = fakeExchange([{ ok: false, reason: 'boom' }, { ok: true, accessToken: 'late-plugin-tok' }]);
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([AGENT_TOKEN(makeJwt('user-a'))]),
      exchangeClient: exchange,
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBe('late-plugin-tok');
    expect(exchange.calls).toHaveLength(2);
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.families?.agent?.accessToken).toBeTruthy();
    expect(doc?.families?.plugin?.accessToken).toBe('late-plugin-tok');
  });

  it('surfaces the F1 partial state (agent committed, no plugin family) when derivation keeps failing', async () => {
    const exchange = fakeExchange([
      { ok: false, reason: 'boom-1' },
      { ok: false, reason: 'boom-2' },
    ]);
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([AGENT_TOKEN(makeJwt('user-a'))]),
      exchangeClient: exchange,
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBeNull();
    const doc = new MachineCredentialStore(storeDir).read();
    // The agent family survives (03 F1 failure path: separate lock holds by design)...
    expect(doc?.families?.agent?.accessToken).toBeTruthy();
    // ...but no plugin family was written, and the v1 mirror is absent (no plugin-plane family).
    expect(doc?.families?.plugin).toBeUndefined();
    expect(doc?.accessToken).toBeUndefined();
  });
});

describe('runCloudLogin — project sink (--project): per-project store, legacy sink NEVER written', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-proj-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('persists to <project>/.ai-game-dev/ and never touches the legacy .godot-mcp sink (06 D7)', async () => {
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([AGENT_TOKEN(makeJwt('user-a'))]),
      exchangeClient: fakeExchange([{ ok: true, accessToken: 'proj-plugin-tok' }]),
      delay: async () => {},
      sink: { kind: 'project', projectPath: tmpDir },
    });

    expect(token).toBe('proj-plugin-tok');
    const storeDir = path.join(tmpDir, '.ai-game-dev');
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.families?.plugin?.accessToken).toBe('proj-plugin-tok');
    // The legacy project sink must NEVER be written again (read-fallback only until f4).
    expect(fs.existsSync(getCredentialsPath(tmpDir))).toBe(false);
    expect(fs.existsSync(path.join(tmpDir, '.godot-mcp'))).toBe(false);
    // The per-project credential + lock files are git-ignored beside the committable marker.
    const gitignore = fs.readFileSync(path.join(storeDir, '.gitignore'), 'utf-8');
    expect(gitignore).toContain('credentials.json');
    expect(gitignore).toContain('credentials.lock');
  });

  it('returns null and writes nothing when authorization is denied', async () => {
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([{ error: 'access_denied' }]),
      exchangeClient: fakeExchange([]),
      delay: async () => {},
      sink: { kind: 'project', projectPath: tmpDir },
    });

    expect(token).toBeNull();
    expect(fs.existsSync(getCredentialsPath(tmpDir))).toBe(false);
    expect(fs.existsSync(path.join(tmpDir, '.ai-game-dev', 'credentials.json'))).toBe(false);
  });
});

describe('runCloudLogin — --tools-only (O10/F10): plugin family ONLY', () => {
  let storeDir: string;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-tools-'));
  });
  afterEach(() => {
    fs.rmSync(storeDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('commits a plugin family only — NO agent family, no token exchange', async () => {
    const pluginJwt = makeJwt('user-ci');
    const exchange = fakeExchange([]); // must never be consulted
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([PLUGIN_TOKEN(pluginJwt)]),
      exchangeClient: exchange,
      delay: async () => {},
      toolsOnly: true,
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBe(pluginJwt);
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.version).toBe(2);
    // Plugin family present, stamped with this CLI's own client id + plugin scope...
    expect(doc?.families?.plugin?.accessToken).toBe(pluginJwt);
    expect(doc?.families?.plugin?.refreshToken).toBe('plugin-refresh');
    expect(doc?.families?.plugin?.clientId).toBe('godot-cli');
    expect(doc?.families?.plugin?.scope).toBe('mcp:plugin');
    // ...and NO agent family anywhere: App pickup is impossible by design (F10).
    expect(doc?.families?.agent).toBeUndefined();
    expect(doc?.families?.legacy).toBeUndefined();
    // The v1 mirror follows the plugin family.
    expect(doc?.accessToken).toBe(pluginJwt);
    // Tools-only never runs the RFC 8693 exchange.
    expect(exchange.calls).toHaveLength(0);
  });
});

describe('runCloudLogin — D6/F7 account-switch guard (--yes-gated)', () => {
  let storeDir: string;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-guard-'));
    // The machine is already authorized as user-a.
    new MachineCredentialStore(storeDir).write({
      version: 2,
      serverTarget: 'https://example.test',
      subject: 'user-a',
      families: {
        plugin: {
          accessToken: 'stored-plugin-tok',
          refreshToken: 'stored-plugin-refresh',
          clientId: 'godot-cli',
          scope: 'mcp:plugin',
        },
      },
      accessToken: 'stored-plugin-tok',
    });
  });
  afterEach(() => {
    fs.rmSync(storeDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('DECLINES a subject mismatch without --yes (non-interactive): store untouched, mint revoked', async () => {
    const fetchImpl = fetchSpy();
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([AGENT_TOKEN(makeJwt('user-b'))]),
      exchangeClient: fakeExchange([{ ok: true, accessToken: 'should-never-commit' }]),
      fetchImpl: fetchImpl as unknown as typeof fetch,
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
      // No assumeYes, no injected confirm: the default (non-TTY test process) fails closed.
    });

    expect(token).toBeNull();
    // Store untouched — still user-a's credential.
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.subject).toBe('user-a');
    expect(doc?.families?.plugin?.accessToken).toBe('stored-plugin-tok');
    expect(doc?.families?.agent).toBeUndefined();
    // The just-minted user-b family was revoked best-effort (RFC 7009) — no orphan device row.
    const revokeCalls = fetchImpl.mock.calls.filter(([url]) => String(url).includes('/oauth/revoke'));
    expect(revokeCalls.length).toBeGreaterThanOrEqual(1);
  });

  it('proceeds with --yes: the store is REPLACED with the new account (old families revoked best-effort)', async () => {
    const fetchImpl = fetchSpy();
    const agentJwt = makeJwt('user-b');
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([AGENT_TOKEN(agentJwt)]),
      exchangeClient: fakeExchange([{ ok: true, accessToken: 'user-b-plugin-tok' }]),
      fetchImpl: fetchImpl as unknown as typeof fetch,
      delay: async () => {},
      assumeYes: true,
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBe('user-b-plugin-tok');
    const doc = new MachineCredentialStore(storeDir).read();
    expect(doc?.subject).toBe('user-b');
    expect(doc?.families?.agent?.accessToken).toBe(agentJwt);
    expect(doc?.families?.plugin?.accessToken).toBe('user-b-plugin-tok');
    // user-a's old plugin family is gone (single-account store, D6) and was revoked best-effort.
    const revokeCalls = fetchImpl.mock.calls.filter(([url]) => String(url).includes('/oauth/revoke'));
    expect(revokeCalls.length).toBeGreaterThanOrEqual(1);
  });

  it('does not prompt at all when the subjects match (same account re-login)', async () => {
    const confirmAccountSwitch = vi.fn(async () => true);
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([AGENT_TOKEN(makeJwt('user-a'))]),
      exchangeClient: fakeExchange([{ ok: true, accessToken: 'fresh-plugin-tok' }]),
      delay: async () => {},
      confirmAccountSwitch,
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBe('fresh-plugin-tok');
    expect(confirmAccountSwitch).not.toHaveBeenCalled();
  });
});
