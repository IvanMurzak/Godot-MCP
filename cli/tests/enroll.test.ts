import { describe, it, expect } from 'vitest';
import { redeemEnrollment } from '../src/utils/enroll.js';

const OK_BODY = {
  access_token: 'plugin.jwt.token',
  token_type: 'Bearer',
  expires_in: 3600,
  refresh_token: 'refresh.token',
  scope: 'mcp:plugin',
  server_url: 'https://ai-game.dev',
};

function mockFetchJson(status: number, body: unknown): typeof fetch {
  return (async (url: string, init?: RequestInit) => {
    // Assert the CLI targets the redeem endpoint with the right body.
    expect(url).toBe('https://ai-game.dev/api/auth/enroll/redeem');
    expect(JSON.parse(String(init?.body))).toEqual({ enroll_code: 'CODE-1234' });
    return {
      ok: status >= 200 && status < 300,
      status,
      statusText: 'x',
      json: async () => body,
    } as unknown as Response;
  }) as unknown as typeof fetch;
}

describe('redeemEnrollment', () => {
  it('returns the credential + server_url on a 200', async () => {
    const result = await redeemEnrollment({ code: 'CODE-1234', baseUrl: 'https://ai-game.dev', fetchImpl: mockFetchJson(200, OK_BODY) });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.credential.access_token).toBe('plugin.jwt.token');
      expect(result.credential.refresh_token).toBe('refresh.token');
      expect(result.credential.server_url).toBe('https://ai-game.dev');
    }
  });

  it('surfaces the uniform error message on an invalid/expired/used code', async () => {
    const result = await redeemEnrollment({
      code: 'CODE-1234',
      baseUrl: 'https://ai-game.dev',
      fetchImpl: mockFetchJson(400, { error: 'invalid_grant', error_description: 'Invalid or expired enrollment code.' }),
    });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.message).toBe('Invalid or expired enrollment code.');
  });

  it('empty code fails before any network call', async () => {
    let called = false;
    const spy = (async () => {
      called = true;
      throw new Error('should not be called');
    }) as unknown as typeof fetch;
    const result = await redeemEnrollment({ code: '   ', baseUrl: 'https://ai-game.dev', fetchImpl: spy });
    expect(result.success).toBe(false);
    expect(called).toBe(false);
  });

  it('maps a connection error to a reachability message', async () => {
    const spy = (async () => {
      throw new Error('fetch failed');
    }) as unknown as typeof fetch;
    const result = await redeemEnrollment({ code: 'CODE-1234', baseUrl: 'http://localhost:9', fetchImpl: spy });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.message).toMatch(/Cannot reach the enrollment server/);
  });

  it('rejects a 200 with an incomplete body (no access_token / server_url)', async () => {
    const result = await redeemEnrollment({
      code: 'CODE-1234',
      baseUrl: 'https://ai-game.dev',
      fetchImpl: mockFetchJson(200, { token_type: 'Bearer' }),
    });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.message).toMatch(/incomplete credential/);
  });
});
