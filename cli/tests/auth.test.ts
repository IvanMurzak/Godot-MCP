import { describe, it, expect, vi } from 'vitest';
import { deviceAuthFlow, type DeviceAuthDeps } from '../src/utils/auth.js';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

const AUTHORIZE_OK = {
  device_code: 'dev-code-123',
  user_code: 'WXYZ-1234',
  verification_uri: 'https://ai-game.dev/device',
  verification_uri_complete: 'https://ai-game.dev/device?code=WXYZ-1234',
  expires_in: 900,
  interval: 5,
};

/**
 * Build an injectable fetch: `/authorize` returns `authorize`; every `/token`
 * poll shifts the next queued response (defaulting to authorization_pending).
 */
function makeFetch(authorize: Response, polls: Response[]): typeof fetch {
  const queue = [...polls];
  return vi.fn(async (url: string | URL | Request) => {
    if (String(url).endsWith('/authorize')) return authorize;
    return queue.shift() ?? jsonResponse(400, { error: 'authorization_pending' });
  }) as unknown as typeof fetch;
}

function deps(fetchImpl: typeof fetch, intervals: number[] = []): DeviceAuthDeps {
  return {
    fetchImpl,
    sleepImpl: async (ms: number) => {
      intervals.push(ms);
    },
    minIntervalMs: 10,
  };
}

describe('deviceAuthFlow', () => {
  it('returns the access token on immediate success', async () => {
    const fetchImpl = makeFetch(
      jsonResponse(200, AUTHORIZE_OK),
      [jsonResponse(200, { access_token: 'tok-abc', token_type: 'Bearer' })],
    );
    const onUserCode = vi.fn();
    const result = await deviceAuthFlow('https://ai-game.dev', 'Godot-MCP CLI', { onUserCode }, deps(fetchImpl));

    expect(result).toEqual({ success: true, accessToken: 'tok-abc' });
    expect(onUserCode).toHaveBeenCalledWith('WXYZ-1234', 'https://ai-game.dev/device?code=WXYZ-1234');
  });

  it('polls through authorization_pending until success', async () => {
    const fetchImpl = makeFetch(jsonResponse(200, AUTHORIZE_OK), [
      jsonResponse(400, { error: 'authorization_pending' }),
      jsonResponse(400, { error: 'authorization_pending' }),
      jsonResponse(200, { access_token: 'tok-final', token_type: 'Bearer' }),
    ]);
    const result = await deviceAuthFlow('https://ai-game.dev', 'Godot-MCP CLI', { onUserCode: vi.fn() }, deps(fetchImpl));
    expect(result).toEqual({ success: true, accessToken: 'tok-final' });
  });

  it('honors slow_down by increasing the poll interval', async () => {
    const intervals: number[] = [];
    const fetchImpl = makeFetch(jsonResponse(200, AUTHORIZE_OK), [
      jsonResponse(400, { error: 'slow_down' }),
      jsonResponse(200, { access_token: 'tok', token_type: 'Bearer' }),
    ]);
    const result = await deviceAuthFlow(
      'https://ai-game.dev',
      'Godot-MCP CLI',
      { onUserCode: vi.fn() },
      deps(fetchImpl, intervals),
    );
    expect(result.success).toBe(true);
    // The interval used before the 2nd poll must exceed the first (slow_down += 5s).
    expect(intervals.length).toBe(2);
    expect(intervals[1]).toBeGreaterThan(intervals[0]!);
  });

  it('maps access_denied to a denied result', async () => {
    const fetchImpl = makeFetch(jsonResponse(200, AUTHORIZE_OK), [
      jsonResponse(400, { error: 'access_denied', error_description: 'user said no' }),
    ]);
    const result = await deviceAuthFlow('https://ai-game.dev', 'Godot-MCP CLI', { onUserCode: vi.fn() }, deps(fetchImpl));
    expect(result).toEqual({ success: false, reason: 'denied', message: 'user said no' });
  });

  it('maps expired_token to an expired result', async () => {
    const fetchImpl = makeFetch(jsonResponse(200, AUTHORIZE_OK), [
      jsonResponse(400, { error: 'expired_token' }),
    ]);
    const result = await deviceAuthFlow('https://ai-game.dev', 'Godot-MCP CLI', { onUserCode: vi.fn() }, deps(fetchImpl));
    expect(result.success).toBe(false);
    expect(result).toMatchObject({ reason: 'expired' });
  });

  it('reports an error when the authorize endpoint is not OK', async () => {
    const fetchImpl = makeFetch(new Response('boom', { status: 503 }), []);
    const onUserCode = vi.fn();
    const result = await deviceAuthFlow('https://ai-game.dev', 'Godot-MCP CLI', { onUserCode }, deps(fetchImpl));
    expect(result.success).toBe(false);
    expect(result).toMatchObject({ reason: 'error' });
    expect((result as { message: string }).message).toContain('503');
    expect(onUserCode).not.toHaveBeenCalled();
  });

  it('expires without polling when the device code is already past its deadline', async () => {
    const fetchImpl = makeFetch(jsonResponse(200, { ...AUTHORIZE_OK, expires_in: 0 }), [
      jsonResponse(200, { access_token: 'should-not-be-used', token_type: 'Bearer' }),
    ]);
    const onUserCode = vi.fn();
    const result = await deviceAuthFlow(
      'https://ai-game.dev',
      'Godot-MCP CLI',
      { onUserCode },
      { ...deps(fetchImpl), nowImpl: () => 1000 },
    );
    expect(result.success).toBe(false);
    expect(result).toMatchObject({ reason: 'expired' });
    // The user code is still surfaced even when the deadline is immediate.
    expect(onUserCode).toHaveBeenCalledTimes(1);
  });
});
