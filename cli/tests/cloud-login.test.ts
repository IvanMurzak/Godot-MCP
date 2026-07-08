import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCloudLogin } from '../src/utils/cloud-login.js';
import { readCredentials, getCredentialsPath } from '../src/utils/credentials.js';
import type { DeviceAuthDeps } from '../src/utils/auth.js';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

const AUTHORIZE_OK = {
  device_code: 'dev-code',
  user_code: 'CODE-1234',
  verification_uri: 'https://example.test/device',
  verification_uri_complete: 'https://example.test/device?code=CODE-1234',
  expires_in: 900,
  interval: 5,
};

function authDeps(polls: Response[]): DeviceAuthDeps {
  const queue = [...polls];
  return {
    fetchImpl: vi.fn(async (url: string | URL | Request) => {
      if (String(url).endsWith('/authorize')) return jsonResponse(200, AUTHORIZE_OK);
      return queue.shift() ?? jsonResponse(400, { error: 'authorization_pending' });
    }) as unknown as typeof fetch,
    sleepImpl: async () => {},
    minIntervalMs: 1,
  };
}

describe('runCloudLogin', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('persists the token and returns it on success', async () => {
    const openBrowserImpl = vi.fn();
    const token = await runCloudLogin(tmpDir, {
      baseUrl: 'https://example.test',
      openBrowserImpl,
      authDeps: authDeps([jsonResponse(200, { access_token: 'cloud-tok', token_type: 'Bearer' })]),
    });

    expect(token).toBe('cloud-tok');
    expect(openBrowserImpl).toHaveBeenCalledWith('https://example.test/device?code=CODE-1234');
    expect(readCredentials(tmpDir)).toEqual({ cloudToken: 'cloud-tok', cloudBaseUrl: 'https://example.test' });
  });

  it('returns null and writes nothing when authorization is denied', async () => {
    const token = await runCloudLogin(tmpDir, {
      baseUrl: 'https://example.test',
      openBrowserImpl: vi.fn(),
      authDeps: authDeps([jsonResponse(400, { error: 'access_denied' })]),
    });

    expect(token).toBeNull();
    expect(fs.existsSync(getCredentialsPath(tmpDir))).toBe(false);
  });
});
