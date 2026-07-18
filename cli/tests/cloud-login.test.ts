import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCloudLogin } from '../src/utils/cloud-login.js';
import { readCredentials, getCredentialsPath } from '../src/utils/credentials.js';
import { readMachineCredentials, getMachineCredentialsPath } from '../src/utils/machine-credentials.js';
import type {
  DeviceAuthTransport,
  DeviceAuthorizeResponse,
  DeviceTokenResponse,
} from '@baizor/gamedev-cli-core';

const AUTHORIZE_OK: DeviceAuthorizeResponse = {
  device_code: 'dev-code',
  user_code: 'CODE-1234',
  verification_uri: 'https://example.test/device',
  verification_uri_complete: 'https://example.test/device?code=CODE-1234',
  expires_in: 900,
  interval: 5,
};

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

const SUCCESS_TOKEN = (accessToken: string): DeviceTokenResponse => ({
  access_token: accessToken,
  refresh_token: 'refresh-tok',
  token_type: 'Bearer',
  expires_in: 3600,
  scope: 'mcp:plugin',
});

describe('runCloudLogin — project sink (--project override)', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('persists the token to the project store and returns it on success', async () => {
    const openBrowserImpl = vi.fn();
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl,
      transport: transport([SUCCESS_TOKEN('cloud-tok')]),
      delay: async () => {},
      sink: { kind: 'project', projectPath: tmpDir },
    });

    expect(token).toBe('cloud-tok');
    expect(openBrowserImpl).toHaveBeenCalledWith('https://example.test/device?code=CODE-1234');
    expect(readCredentials(tmpDir)).toEqual({ cloudToken: 'cloud-tok', cloudBaseUrl: 'https://example.test' });
  });

  it('returns null and writes nothing when authorization is denied', async () => {
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([{ error: 'access_denied' }]),
      delay: async () => {},
      sink: { kind: 'project', projectPath: tmpDir },
    });

    expect(token).toBeNull();
    expect(fs.existsSync(getCredentialsPath(tmpDir))).toBe(false);
  });
});

describe('runCloudLogin — machine sink (default)', () => {
  let storeDir: string;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-machine-'));
  });
  afterEach(() => {
    fs.rmSync(storeDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('persists the FULL credential set (access + refresh + expiry) to the shared machine store', async () => {
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([SUCCESS_TOKEN('machine-tok')]),
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBe('machine-tok');
    const creds = readMachineCredentials(storeDir);
    expect(creds?.accessToken).toBe('machine-tok');
    // B3 fix: the refresh token + expiry are now persisted (legacy PAT flow dropped them).
    expect(creds?.refreshToken).toBe('refresh-tok');
    expect(creds?.expiresAt).toBeTruthy();
    expect(creds?.serverTarget).toBe('https://example.test');
    expect(creds?.version).toBe(1);
    // No project-local credentials file is created anywhere near the store dir.
    expect(fs.existsSync(path.join(storeDir, '.godot-mcp', 'credentials.json'))).toBe(false);
  });

  it('defaults to the machine store when no sink is supplied', async () => {
    // Redirect the default store to a temp dir via the env override so the real ~/.ai-game-dev
    // is never touched by the test.
    const prev = process.env.AI_GAME_DEV_CREDENTIALS_DIR;
    process.env.AI_GAME_DEV_CREDENTIALS_DIR = storeDir;
    try {
      const token = await runCloudLogin({
        baseUrl: 'https://example.test',
        openBrowserImpl: vi.fn(),
        transport: transport([SUCCESS_TOKEN('default-tok')]),
        delay: async () => {},
      });
      expect(token).toBe('default-tok');
      expect(fs.existsSync(getMachineCredentialsPath(storeDir))).toBe(true);
      expect(readMachineCredentials(storeDir)?.accessToken).toBe('default-tok');
    } finally {
      if (prev === undefined) delete process.env.AI_GAME_DEV_CREDENTIALS_DIR;
      else process.env.AI_GAME_DEV_CREDENTIALS_DIR = prev;
    }
  });

  it('returns null and writes nothing to the machine store when denied', async () => {
    const token = await runCloudLogin({
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      transport: transport([{ error: 'access_denied' }]),
      delay: async () => {},
      sink: { kind: 'machine', storeBaseDir: storeDir },
    });

    expect(token).toBeNull();
    expect(fs.existsSync(getMachineCredentialsPath(storeDir))).toBe(false);
  });
});
