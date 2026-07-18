import * as ui from './ui.js';
import { DEFAULT_CLOUD_BASE_URL } from './connection.js';
import { readCredentials, writeCredentials } from './credentials.js';
import { readMachineCredentials, writeMachineCredentials } from './machine-credentials.js';
import { openBrowser } from './browser.js';
import {
  deviceLogin,
  HttpDeviceAuthTransport,
  godotAdapter,
  DEFAULT_PLUGIN_SCOPE,
  type DeviceAuthTransport,
  type MachineCredentials,
} from '@baizor/gamedev-cli-core';

/**
 * Where a successful `login` persists the credential.
 *
 * - `machine` (the default) → the shared per-machine store `~/.ai-game-dev/credentials.json`
 *   (0600 on POSIX / DPAPI on Windows), so the engine plugin auto-adopts it — sign in once per
 *   machine (design 06 · D12). `storeBaseDir` overrides the store directory (tests only).
 * - `project` → the legacy project-local override `<projectPath>/.godot-mcp/credentials.json`
 *   (gitignored), kept for per-project accounts (the `--project` flag).
 */
export type CredentialSink =
  | { kind: 'machine'; storeBaseDir?: string }
  | { kind: 'project'; projectPath: string };

export interface CloudLoginOptions {
  /** Cloud base URL to authenticate against (default https://ai-game.dev). */
  baseUrl?: string;
  /** Browser opener (injectable for tests; defaults to the real opener). */
  openBrowserImpl?: (url: string) => void;
  /** Where to persist the credential. Defaults to the shared machine store. */
  sink?: CredentialSink;
  /**
   * Injectable device-authorization transport (tests). Defaults to the real fetch-backed
   * {@link HttpDeviceAuthTransport} which POSTs to `{base}/oauth/device_authorization` +
   * `{base}/oauth/token` (the OAuth 2.1 device grant — NOT the retired legacy JSON device flow).
   */
  transport?: DeviceAuthTransport;
  /** Injectable fetch for the default transport (tests). */
  fetchImpl?: typeof fetch;
  /** Injectable poll delay (tests) — bypass the real RFC 8628 polling wait. */
  delay?: (ms: number, signal?: AbortSignal) => Promise<void>;
  /** Injectable clock in ms (tests) — for deadline control. */
  now?: () => number;
}

/**
 * Run the OAuth 2.1 device-authorization login (RFC 8628) via `@baizor/gamedev-cli-core`
 * {@link deviceLogin}: request a device+user code, display the user code + verification URL, open the
 * browser, poll `/oauth/token` to completion, and persist the **full** {@link MachineCredentials}
 * (access + rotating refresh + expiry + subject) to the resolved {@link CredentialSink}.
 *
 * This REPLACES the retired legacy JSON device flow (which minted a non-expiring PAT and
 * dropped the refresh/expiry — defects B2/B3). The client id is the product id `godot-cli`
 * ({@link godotAdapter.clientId}) with scope `mcp:plugin`, and **no PAT is ever minted**. On any
 * failure NOTHING is written (design 03 F4) — the store survives a denied/expired/network error
 * intact.
 *
 * Returns the access token on success, or null on failure (errors are printed).
 */
export async function runCloudLogin(options: CloudLoginOptions = {}): Promise<string | null> {
  const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');
  const openBrowserImpl = options.openBrowserImpl ?? openBrowser;
  const sink: CredentialSink = options.sink ?? { kind: 'machine' };

  let spinner: ReturnType<typeof ui.startSpinner> | undefined;

  const transport =
    options.transport ??
    new HttpDeviceAuthTransport({
      serverBaseUrl: baseUrl,
      clientId: godotAdapter.clientId, // 'godot-cli'
      scope: DEFAULT_PLUGIN_SCOPE, // 'mcp:plugin'
      fetchImpl: options.fetchImpl,
    });

  try {
    const result = await deviceLogin({
      serverBaseUrl: baseUrl,
      clientId: godotAdapter.clientId,
      scope: DEFAULT_PLUGIN_SCOPE,
      serverTarget: baseUrl,
      transport,
      delay: options.delay,
      now: options.now,
      onUserCode: (userCode, verificationUri) => {
        ui.info('Open this URL to authorize:');
        console.log();
        console.log(`  ${verificationUri}`);
        console.log();
        ui.label('Code', userCode);
      },
      onPolling: () => {
        spinner = ui.startSpinner('Waiting for authorization...');
      },
      openBrowser: openBrowserImpl,
    });

    if (result.ok) {
      spinner?.success('Authorized');
      persistCredential(sink, result.credentials, baseUrl);
      return result.credentials.accessToken ?? null;
    }

    spinner?.stop();
    ui.error(result.message);
    return null;
  } catch (err) {
    spinner?.stop();
    const message = err instanceof Error ? err.message : String(err);
    if (message.includes('ECONNREFUSED') || message.includes('fetch failed')) {
      ui.error(`Cannot reach cloud server at ${baseUrl}`);
    } else {
      ui.error(`Authentication failed: ${message}`);
    }
    return null;
  }
}

function persistCredential(sink: CredentialSink, credentials: MachineCredentials, baseUrl: string): void {
  if (sink.kind === 'project') {
    // Legacy per-project override: keep the project store's `cloudToken`/`cloudBaseUrl` shape a
    // `--project` login and `open --mode Cloud` read back (credentials.ts). A corrupt existing
    // credentials file must not block a fresh login.
    let existing = {};
    try {
      existing = readCredentials(sink.projectPath) ?? {};
    } catch {
      existing = {};
    }
    writeCredentials(sink.projectPath, {
      ...existing,
      cloudToken: credentials.accessToken,
      cloudBaseUrl: baseUrl,
    });
    return;
  }

  // Machine store: persist the FULL credential set (access + refresh + expiry + subject — the B3
  // fix), preserving any prior identity / forward-compat fields, and recording the server target
  // the credential was issued for.
  let existing = {};
  try {
    existing = readMachineCredentials(sink.storeBaseDir) ?? {};
  } catch {
    existing = {};
  }
  writeMachineCredentials(
    {
      ...existing,
      ...credentials,
      version: 1,
      serverTarget: baseUrl,
    },
    sink.storeBaseDir,
  );
}
