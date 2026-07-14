import * as ui from './ui.js';
import { DEFAULT_CLOUD_BASE_URL } from './connection.js';
import { readCredentials, writeCredentials } from './credentials.js';
import { readMachineCredentials, writeMachineCredentials } from './machine-credentials.js';
import { deviceAuthFlow, type DeviceAuthDeps } from './auth.js';
import { openBrowser } from './browser.js';

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
  /** Client label sent to the authorize endpoint. */
  clientLabel?: string;
  /** Browser opener (injectable for tests; defaults to the real opener). */
  openBrowserImpl?: (url: string) => void;
  /** Device-flow dependency injection (fetch/sleep/clock) for tests. */
  authDeps?: DeviceAuthDeps;
  /** Where to persist the credential. Defaults to the shared machine store. */
  sink?: CredentialSink;
}

/**
 * Run the cloud device-auth flow: initiate, display the user code, open the browser, poll to
 * completion, and persist the token to the resolved {@link CredentialSink} (the shared machine store
 * by default; the project-local override when `--project` is used).
 *
 * Returns the access token on success, or null on failure (errors are printed).
 */
export async function runCloudLogin(options: CloudLoginOptions = {}): Promise<string | null> {
  const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');
  const clientLabel = options.clientLabel ?? 'Godot-MCP CLI';
  const openBrowserImpl = options.openBrowserImpl ?? openBrowser;
  const sink: CredentialSink = options.sink ?? { kind: 'machine' };

  let spinner: ReturnType<typeof ui.startSpinner> | undefined;

  try {
    const result = await deviceAuthFlow(
      baseUrl,
      clientLabel,
      {
        onUserCode: (userCode, verificationUrl) => {
          ui.info('Open this URL to authorize:');
          console.log();
          console.log(`  ${verificationUrl}`);
          console.log();
          ui.label('Code', userCode);
          openBrowserImpl(verificationUrl);
        },
        onPolling: () => {
          spinner = ui.startSpinner('Waiting for authorization...');
        },
      },
      options.authDeps,
    );

    if (result.success) {
      spinner?.success('Authorized');
      persistCredential(sink, result.accessToken, baseUrl);
      return result.accessToken;
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

function persistCredential(sink: CredentialSink, accessToken: string, baseUrl: string): void {
  if (sink.kind === 'project') {
    // A corrupt existing credentials file must not block a fresh login.
    let existing = {};
    try {
      existing = readCredentials(sink.projectPath) ?? {};
    } catch {
      existing = {};
    }
    writeCredentials(sink.projectPath, {
      ...existing,
      cloudToken: accessToken,
      cloudBaseUrl: baseUrl,
    });
    return;
  }

  // Machine store: preserve any stored identity/refresh fields, replacing the token material and
  // recording the server target the credential was issued for.
  let existing = {};
  try {
    existing = readMachineCredentials(sink.storeBaseDir) ?? {};
  } catch {
    existing = {};
  }
  writeMachineCredentials(
    {
      ...existing,
      version: 1,
      accessToken,
      serverTarget: baseUrl,
    },
    sink.storeBaseDir,
  );
}
