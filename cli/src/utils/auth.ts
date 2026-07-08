import { verbose } from './ui.js';

// --- RFC 8628 Device Authorization Grant flow (against ai-game.dev). ---
//
// Ported from the sibling `unity-mcp-cli` (`cli/src/utils/auth.ts`), which is the
// verified-working implementation against the same backend. This variant accepts
// injectable `fetch` / `sleep` / `now` so the polling loop is fully unit-testable
// with no live network and no real timers (see `tests/auth.test.ts`).

// ─── Types ───────────────────────────────────────────────────────────────────

export interface DeviceAuthorizeResponse {
  device_code: string;
  user_code: string;
  verification_uri: string;
  verification_uri_complete: string;
  expires_in: number;
  interval: number;
}

interface DeviceTokenSuccessResponse {
  access_token: string;
  token_type: string;
}

interface DeviceTokenErrorResponse {
  error: string;
  error_description?: string;
}

export type DeviceAuthResult =
  | { success: true; accessToken: string }
  | { success: false; reason: 'expired' | 'denied' | 'error'; message: string };

export interface DeviceAuthCallbacks {
  /** Invoked once the device+user codes are issued so the caller can display them / open a browser. */
  onUserCode: (userCode: string, verificationUrl: string) => void;
  /** Invoked once, right before polling begins (e.g. to start a spinner). */
  onPolling?: () => void;
}

/** Injectable dependencies — all default to the real implementations. */
export interface DeviceAuthDeps {
  /** HTTP client (defaults to global `fetch`). */
  fetchImpl?: typeof fetch;
  /** Delay primitive (defaults to a real `setTimeout` sleep). */
  sleepImpl?: (ms: number) => Promise<void>;
  /** Monotonic clock in ms (defaults to `Date.now`). */
  nowImpl?: () => number;
  /** Floor for the poll interval in ms (defaults to 5000). */
  minIntervalMs?: number;
}

// ─── Flow ────────────────────────────────────────────────────────────────────

/**
 * Run the RFC 8628 Device Authorization Grant flow against `baseUrl`.
 *
 * 1. POST `/api/auth/device/authorize` → device_code + user_code.
 * 2. Invoke `onUserCode` so the caller can display instructions / open a browser.
 * 3. Poll `/api/auth/device/token` until success, denial, or expiry, honoring
 *    `authorization_pending` / `slow_down` / `access_denied` / `expired_token`.
 */
export async function deviceAuthFlow(
  baseUrl: string,
  clientLabel: string,
  callbacks: DeviceAuthCallbacks,
  deps: DeviceAuthDeps = {},
): Promise<DeviceAuthResult> {
  const doFetch = deps.fetchImpl ?? fetch;
  const sleep = deps.sleepImpl ?? defaultSleep;
  const now = deps.nowImpl ?? (() => Date.now());
  const minIntervalMs = deps.minIntervalMs ?? 5000;

  const authorizeUrl = `${baseUrl}/api/auth/device/authorize`;
  verbose(`POST ${authorizeUrl}`);

  const initResponse = await fetchWithTimeout(
    doFetch,
    authorizeUrl,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ client_label: clientLabel }),
    },
    30_000,
  );

  if (!initResponse.ok) {
    const text = await initResponse.text();
    return {
      success: false,
      reason: 'error',
      message: `Failed to initiate device auth (HTTP ${initResponse.status}): ${text}`,
    };
  }

  const auth = (await initResponse.json()) as DeviceAuthorizeResponse;
  verbose(`Device code received, user code: ${auth.user_code}, expires in ${auth.expires_in}s`);

  callbacks.onUserCode(auth.user_code, auth.verification_uri_complete);
  callbacks.onPolling?.();

  const tokenUrl = `${baseUrl}/api/auth/device/token`;
  const deadline = now() + auth.expires_in * 1000;
  let interval = Math.max(auth.interval * 1000, minIntervalMs);

  while (now() < deadline) {
    await sleep(interval);

    if (now() >= deadline) break;

    verbose(`Polling ${tokenUrl} (interval=${interval / 1000}s)`);

    const pollResponse = await fetchWithTimeout(
      doFetch,
      tokenUrl,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          device_code: auth.device_code,
          grant_type: 'urn:ietf:params:oauth:grant-type:device_code',
        }),
      },
      15_000,
    );

    if (pollResponse.ok) {
      const data = (await pollResponse.json()) as DeviceTokenSuccessResponse;
      return { success: true, accessToken: data.access_token };
    }

    let errorData: DeviceTokenErrorResponse;
    try {
      errorData = (await pollResponse.json()) as DeviceTokenErrorResponse;
    } catch {
      return {
        success: false,
        reason: 'error',
        message: `Unexpected response from token endpoint (HTTP ${pollResponse.status})`,
      };
    }
    verbose(`Poll response: ${errorData.error} — ${errorData.error_description ?? ''}`);

    switch (errorData.error) {
      case 'authorization_pending':
        break;

      case 'slow_down':
        interval = Math.min(interval + 5000, 30_000);
        verbose(`Slowing down, new interval: ${interval / 1000}s`);
        break;

      case 'expired_token':
        return {
          success: false,
          reason: 'expired',
          message: errorData.error_description ?? 'Device code expired. Please try again.',
        };

      case 'access_denied':
        return {
          success: false,
          reason: 'denied',
          message: errorData.error_description ?? 'Authorization was denied.',
        };

      default:
        return {
          success: false,
          reason: 'error',
          message: errorData.error_description ?? `Unexpected error: ${errorData.error}`,
        };
    }
  }

  return {
    success: false,
    reason: 'expired',
    message: 'Device code expired. Please try again.',
  };
}

function defaultSleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function fetchWithTimeout(
  doFetch: typeof fetch,
  url: string,
  options: RequestInit,
  timeoutMs: number,
): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await doFetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}
