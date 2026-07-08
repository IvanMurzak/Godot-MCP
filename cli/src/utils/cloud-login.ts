import * as ui from './ui.js';
import { DEFAULT_CLOUD_BASE_URL } from './connection.js';
import { readCredentials, writeCredentials } from './credentials.js';
import { deviceAuthFlow, type DeviceAuthDeps } from './auth.js';
import { openBrowser } from './browser.js';

export interface CloudLoginOptions {
  /** Cloud base URL to authenticate against (default https://ai-game.dev). */
  baseUrl?: string;
  /** Client label sent to the authorize endpoint. */
  clientLabel?: string;
  /** Browser opener (injectable for tests; defaults to the real opener). */
  openBrowserImpl?: (url: string) => void;
  /** Device-flow dependency injection (fetch/sleep/clock) for tests. */
  authDeps?: DeviceAuthDeps;
}

/**
 * Run the cloud device-auth flow: initiate, display the user code, open the
 * browser, poll to completion, and persist the token to the project's
 * `.godot-mcp/credentials.json`.
 *
 * Returns the access token on success, or null on failure (errors are printed).
 */
export async function runCloudLogin(
  projectPath: string,
  options: CloudLoginOptions = {},
): Promise<string | null> {
  const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');
  const clientLabel = options.clientLabel ?? 'Godot-MCP CLI';
  const openBrowserImpl = options.openBrowserImpl ?? openBrowser;

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

      // A corrupt existing credentials file must not block a fresh login.
      let existing = {};
      try {
        existing = readCredentials(projectPath) ?? {};
      } catch {
        existing = {};
      }
      writeCredentials(projectPath, {
        ...existing,
        cloudToken: result.accessToken,
        cloudBaseUrl: baseUrl,
      });

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
