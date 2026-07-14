import { verbose } from './ui.js';

/**
 * Redeem a D13 **enrollment code** against the cloud AS `POST /api/auth/enroll/redeem`
 * endpoint (design 05 §Enrollment endpoints) — the agent-first path that plants a
 * plugin credential with NO browser hop. The code is **burned on the first redeem
 * attempt** (server-side), and every failure returns the SAME uniform error (no
 * oracle), so a spent/invalid/expired code is indistinguishable — the CLI surfaces
 * one clean message.
 *
 * Injectable `fetch` so the round-trip is fully unit-testable with no live network.
 * Never throws past its boundary — returns a discriminated union.
 */

/** The redeem-endpoint success body (mirrors AGS `EnrollRedeemResponse`). */
export interface EnrollRedeemSuccessBody {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token: string;
  scope: string;
  /** The server target the code was minted for (the plugin boots against this hub). */
  server_url: string;
}

export type EnrollRedeemResult =
  | { success: true; credential: EnrollRedeemSuccessBody }
  | { success: false; message: string };

export interface RedeemEnrollmentOptions {
  /** The enrollment code to redeem. */
  code: string;
  /** Cloud AS base URL (default https://ai-game.dev). */
  baseUrl: string;
  /** Injectable fetch for tests (defaults to global fetch). */
  fetchImpl?: typeof fetch;
  /** Request timeout in ms (default 30000). */
  timeoutMs?: number;
}

export async function redeemEnrollment(options: RedeemEnrollmentOptions): Promise<EnrollRedeemResult> {
  const code = (options.code ?? '').trim();
  if (code.length === 0) {
    return { success: false, message: 'No enrollment code supplied.' };
  }

  const baseUrl = (options.baseUrl ?? '').replace(/\/$/, '');
  const redeemUrl = `${baseUrl}/api/auth/enroll/redeem`;
  const doFetch = options.fetchImpl ?? fetch;
  const timeoutMs = options.timeoutMs ?? 30_000;

  verbose(`POST ${redeemUrl}`);

  let response: Response;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    response = await doFetch(redeemUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enroll_code: code }),
      signal: controller.signal,
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    if (message.includes('ECONNREFUSED') || message.includes('fetch failed')) {
      return { success: false, message: `Cannot reach the enrollment server at ${baseUrl}.` };
    }
    if (message.toLowerCase().includes('abort')) {
      return { success: false, message: `Enrollment request to ${baseUrl} timed out.` };
    }
    return { success: false, message: `Enrollment request failed: ${message}` };
  } finally {
    clearTimeout(timer);
  }

  if (!response.ok) {
    // Uniform-error contract: the server returns the same {error, error_description}
    // for any invalid/expired/used/mismatched code. Surface one clean message.
    let description: string | undefined;
    try {
      const body = (await response.json()) as { error?: string; error_description?: string };
      description = body.error_description ?? body.error;
    } catch {
      description = undefined;
    }
    verbose(`Redeem failed: HTTP ${response.status} — ${description ?? ''}`);
    return {
      success: false,
      message:
        description ??
        `Enrollment code could not be redeemed (HTTP ${response.status}). The code may be invalid, expired, or already used.`,
    };
  }

  let body: EnrollRedeemSuccessBody;
  try {
    body = (await response.json()) as EnrollRedeemSuccessBody;
  } catch {
    return { success: false, message: 'The enrollment server returned an unreadable response.' };
  }

  if (typeof body.access_token !== 'string' || body.access_token.length === 0 || typeof body.server_url !== 'string') {
    return { success: false, message: 'The enrollment server returned an incomplete credential.' };
  }

  return { success: true, credential: body };
}
