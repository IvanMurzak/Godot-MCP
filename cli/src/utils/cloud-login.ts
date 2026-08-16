import * as ui from './ui.js';
import { DEFAULT_CLOUD_BASE_URL } from './connection.js';
import {
  openMachineStore,
  openProjectStore,
  ensureProjectStoreGitignored,
  createMachineCredentialProvider,
  hasPluginPlaneCredential,
} from './machine-store.js';
import { openBrowser } from './browser.js';
import {
  deviceLogin,
  HttpDeviceAuthTransport,
  HttpTokenExchangeClient,
  commitAgentLogin,
  commitToolsOnlyLogin,
  derivePluginFamily,
  effectiveFamilies,
  godotAdapter,
  DEFAULT_PLUGIN_SCOPE,
  MCP_AGENT_SCOPE,
  type DeviceAuthTransport,
  type MachineCredentials,
  type MachineCredentialStore,
  type TokenExchangeClient,
} from '@baizor/gamedev-cli-core';

/**
 * Where a successful `login` persists the credential.
 *
 * - `machine` (the default) → the shared per-machine store `~/.ai-game-dev/credentials.json`
 *   (0600 on POSIX / DPAPI on Windows), so the engine plugin auto-adopts it — sign in once per
 *   machine (design 06 · D12). `storeBaseDir` overrides the store directory (tests only).
 * - `project` → the per-project store `<projectPath>/.ai-game-dev/credentials.json` (gitignored),
 *   kept for per-project accounts (the `--project` flag). The CLI NEVER writes the legacy
 *   `<projectPath>/.godot-mcp/credentials.json` sink any more (06 D7 — read-fallback only, f4
 *   removes it).
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
   * O10/F10 `--tools-only`: mint a `scope=mcp:plugin` credential and commit it as a plugin
   * family ONLY — the store then holds no agent family, so desktop-App pickup is impossible by
   * design and the runner appears as its own revocable device group.
   */
  toolsOnly?: boolean;
  /**
   * `--yes`: auto-confirm the D6/F7 account-switch guard. Without it a subject mismatch prompts
   * on a TTY and is DECLINED in a non-interactive session (fail closed — the just-minted family
   * is revoked best-effort and the store stays untouched).
   */
  assumeYes?: boolean;
  /** Injectable account-switch confirmation (tests). Overrides the `assumeYes`/TTY prompt. */
  confirmAccountSwitch?: (info: {
    storedSubject: string;
    newSubject: string;
  }) => boolean | Promise<boolean>;
  /**
   * Injectable device-authorization transport (tests). Defaults to the real fetch-backed
   * {@link HttpDeviceAuthTransport} which POSTs to `{base}/oauth/device_authorization` +
   * `{base}/oauth/token` (the OAuth 2.1 device grant — NOT the retired legacy JSON device flow).
   */
  transport?: DeviceAuthTransport;
  /** Injectable RFC 8693 exchange client (tests). Defaults to {@link HttpTokenExchangeClient}. */
  exchangeClient?: TokenExchangeClient;
  /** Injectable fetch for the default transport/exchange/revocation clients (tests). */
  fetchImpl?: typeof fetch;
  /** Injectable poll delay (tests) — bypass the real RFC 8628 polling wait. */
  delay?: (ms: number, signal?: AbortSignal) => Promise<void>;
  /** Injectable clock in ms (tests) — for deadline control. */
  now?: () => number;
}

/**
 * Run the OAuth 2.1 device-authorization login (RFC 8628) and commit the mint through the shared
 * cli-core login-commit machinery (unified-machine-auth 03 F1 / F10):
 *
 * - **Default (agent login, F1):** the device flow requests **`scope=mcp:agent`** with the
 *   product client id `godot-cli`; {@link commitAgentLogin} then writes the agent family under a
 *   FIRST lock hold, derives the plugin family via RFC 8693 token exchange with the lock
 *   released, and writes it (+ the v1 compat mirror) under a SECOND hold. A failed exchange
 *   leaves the agent family committed ("partially authorized") and the derivation is retried
 *   once here with backoff.
 * - **`--tools-only` (O10/F10):** the device flow requests `scope=mcp:plugin` and
 *   {@link commitToolsOnlyLogin} writes a plugin family ONLY.
 * - **Account-switch guard (D6/F7):** a subject mismatch against the stored credential requires
 *   confirmation (`--yes` / TTY prompt); a decline revokes the just-minted family (best effort,
 *   RFC 7009) and aborts with the store untouched.
 *
 * On any failure before the commit NOTHING is written (design 03 F4) — the store survives a
 * denied/expired/network error intact. Nothing here logs token material.
 *
 * Returns the plugin-plane access token on success, or null on failure (errors are printed).
 */
export async function runCloudLogin(options: CloudLoginOptions = {}): Promise<string | null> {
  const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');
  const openBrowserImpl = options.openBrowserImpl ?? openBrowser;
  const sink: CredentialSink = options.sink ?? { kind: 'machine' };
  const clientId = godotAdapter.clientId; // 'godot-cli' (cli-core engine-adapter)
  const scope = options.toolsOnly ? DEFAULT_PLUGIN_SCOPE : MCP_AGENT_SCOPE;

  let spinner: ReturnType<typeof ui.startSpinner> | undefined;

  const transport =
    options.transport ??
    new HttpDeviceAuthTransport({
      serverBaseUrl: baseUrl,
      clientId,
      scope,
      fetchImpl: options.fetchImpl,
    });

  try {
    const result = await deviceLogin({
      serverBaseUrl: baseUrl,
      clientId,
      scope,
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

    if (!result.ok) {
      spinner?.stop();
      ui.error(result.message);
      return null;
    }

    spinner?.success('Authorized');
    return await commitCredential(sink, result.credentials, baseUrl, options);
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

/** Resolve the credential store a sink addresses (and gitignore a per-project store). */
function resolveSinkStore(sink: CredentialSink): MachineCredentialStore {
  if (sink.kind === 'project') {
    ensureProjectStoreGitignored(sink.projectPath);
    return openProjectStore(sink.projectPath);
  }
  return openMachineStore(sink.storeBaseDir);
}

/**
 * The D6/F7 confirmation used when the caller supplies none: `--yes` auto-confirms; a TTY
 * prompts; a non-interactive session DECLINES (fail closed — cli-core then revokes the
 * just-minted family and leaves the store untouched).
 */
function buildConfirmAccountSwitch(
  options: CloudLoginOptions,
): (info: { storedSubject: string; newSubject: string }) => boolean | Promise<boolean> {
  if (options.confirmAccountSwitch) return options.confirmAccountSwitch;
  return async ({ storedSubject, newSubject }) => {
    if (options.assumeYes) return true;
    if (!process.stdin.isTTY || !process.stdout.isTTY) {
      ui.error(
        `This machine is signed in as a different account (${storedSubject}). ` +
          'Re-run with --yes to replace it with the new account.',
      );
      return false;
    }
    return ui.confirm(
      `This machine is signed in as account ${storedSubject}; you are signing in as ${newSubject}. ` +
        'Replace the machine credential (signs the old account out of all tools on this machine)?',
    );
  };
}

/** Commit the mint via the shared cli-core helpers and report the outcome. */
async function commitCredential(
  sink: CredentialSink,
  credentials: MachineCredentials,
  baseUrl: string,
  options: CloudLoginOptions,
): Promise<string | null> {
  const store = resolveSinkStore(sink);
  const confirmAccountSwitch = buildConfirmAccountSwitch(options);
  const clientId = godotAdapter.clientId;

  if (options.toolsOnly) {
    const commit = await commitToolsOnlyLogin({
      store,
      clientId,
      credentials,
      confirmAccountSwitch,
      fetchImpl: options.fetchImpl,
      onWarning: ui.warn,
    });
    if (commit.status === 'committed') {
      return pluginAccessToken(commit.document);
    }
    if (commit.status === 'switch-declined') {
      ui.error('Login aborted: the stored account was kept and the new credential was revoked.');
      return null;
    }
    ui.error('Login aborted: the credential store changed concurrently. Re-run `godot-cli login`.');
    return null;
  }

  const exchangeClient =
    options.exchangeClient ??
    new HttpTokenExchangeClient({
      defaultServerBaseUrl: baseUrl,
      ...(options.fetchImpl ? { fetchImpl: options.fetchImpl } : {}),
    });

  const commit = await commitAgentLogin({
    store,
    exchangeClient,
    clientId,
    credentials,
    confirmAccountSwitch,
    fetchImpl: options.fetchImpl,
    onWarning: ui.warn,
  });

  if (commit.status === 'committed') {
    return pluginAccessToken(commit.document);
  }
  if (commit.status === 'switch-declined') {
    ui.error('Login aborted: the stored account was kept and the new credential was revoked.');
    return null;
  }
  if (commit.status === 'aborted') {
    ui.error(
      'Login aborted: the credential store changed concurrently ' +
        `(${commit.reason}). Re-run \`godot-cli login\`.`,
    );
    return null;
  }

  // `partial` (F1 failure path): the agent family IS committed; retry the derivation leg once
  // with backoff before surfacing the partial state.
  ui.warn('Partially authorized: the plugin credential could not be derived yet. Retrying...');
  const delay = options.delay ?? ((ms: number) => new Promise<void>((r) => setTimeout(r, ms)));
  await delay(2000);
  const derived = await derivePluginFamily({
    store,
    exchangeClient,
    clientId,
    agentAccessToken: credentials.accessToken ?? '',
    ...(credentials.subject !== undefined ? { expectedSubject: credentials.subject } : {}),
    serverTarget: baseUrl,
    fetchImpl: options.fetchImpl,
    onWarning: ui.warn,
  });
  if (derived.status === 'derived') {
    return pluginAccessToken(derived.document);
  }
  ui.error(
    'Partially authorized: your account is signed in, but the tools credential could not be ' +
      'derived. Re-run `godot-cli login` to finish signing in.',
  );
  return null;
}

/** The plugin-plane access token of a committed document (falls back to the v1 mirror). */
function pluginAccessToken(document: MachineCredentials): string | null {
  const families = effectiveFamilies(document);
  return families.plugin?.accessToken ?? families.legacy?.accessToken ?? document.accessToken ?? null;
}

/**
 * What a stored credential document means for a `login` against `baseUrl`:
 *
 *  - `signed-in` — a usable **plugin-plane** credential exists for this server; `login` without
 *    `--force` short-circuits.
 *  - `partial` — the F1 failure state: an agent family is committed for this server but the
 *    plugin family was never derived (exchange failed). `login` must FINISH the derivation (no
 *    second device flow), never report "Already authenticated" — the agent family alone serves
 *    no command, so treating it as signed-in wedges the CLI (review f2 B1: `login` said
 *    "already authenticated" while every command raised `login required`, with `--force` as the
 *    only, unnamed exit).
 *  - `signed-out` — no credential for this server (missing/empty store, or a credential issued
 *    against a different base URL): run the full device flow.
 */
export type LoginState = 'signed-in' | 'partial' | 'signed-out';

/** Classify a stored document for the `login` gate (see {@link LoginState}). */
export function classifyLoginState(document: MachineCredentials | null, baseUrl: string): LoginState {
  if (!document || document.serverTarget !== baseUrl) return 'signed-out';
  if (hasPluginPlaneCredential(document)) return 'signed-in';
  const agent = effectiveFamilies(document).agent;
  if (typeof agent?.accessToken === 'string' && agent.accessToken.trim().length > 0) return 'partial';
  return 'signed-out';
}

/** Options for {@link completePartialLogin}. */
export interface CompletePartialLoginOptions {
  /** Cloud base URL the credential belongs to (default https://ai-game.dev). */
  baseUrl?: string;
  /** The store the partial credential lives in. Defaults to the shared machine store. */
  sink?: CredentialSink;
  /** Injectable RFC 8693 exchange client (tests). Defaults to {@link HttpTokenExchangeClient}. */
  exchangeClient?: TokenExchangeClient;
  /** Injectable fetch for the default exchange client / provider refresher (tests). */
  fetchImpl?: typeof fetch;
}

/**
 * Finish a partially-authorized login (03 F1 failure path): the agent family is already
 * committed, so re-run ONLY the derivation leg — obtain a fresh agent access token through the
 * provider (refreshing under the lock if it expired) and {@link derivePluginFamily} from it.
 * **No second device flow, no browser.** On failure nothing is written and the agent family
 * survives for the next attempt.
 *
 * Returns the derived plugin-plane access token, or null (errors printed, store untouched).
 */
export async function completePartialLogin(
  options: CompletePartialLoginOptions = {},
): Promise<string | null> {
  const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');
  const sink: CredentialSink = options.sink ?? { kind: 'machine' };
  const store = resolveSinkStore(sink);

  let document: MachineCredentials | null;
  try {
    document = store.read();
  } catch (err) {
    ui.error(
      `The credential store is unreadable (${err instanceof Error ? err.message : String(err)}). ` +
        'Run `godot-cli login --force` to re-authorize.',
    );
    return null;
  }
  if (classifyLoginState(document, baseUrl) !== 'partial') {
    ui.error('No partially-authorized sign-in found for this server. Run `godot-cli login`.');
    return null;
  }

  // A FRESH agent access token: the provider serves the agent plane, refreshing under the
  // cross-process lock when the stored one is (about to be) expired.
  const provider = createMachineCredentialProvider({
    storeDir: store.baseDirectory,
    serverBaseUrl: baseUrl,
    ...(options.fetchImpl ? { fetchImpl: options.fetchImpl } : {}),
    onWarning: ui.warn,
  });
  let agentAccessToken: string;
  try {
    agentAccessToken = await provider.getAccessToken({ family: 'agent' });
  } catch (err) {
    ui.error(
      `The partially-authorized sign-in can no longer be used (${err instanceof Error ? err.message : String(err)}). ` +
        'Run `godot-cli login --force` to start a fresh sign-in.',
    );
    return null;
  }

  const exchangeClient =
    options.exchangeClient ??
    new HttpTokenExchangeClient({
      defaultServerBaseUrl: baseUrl,
      ...(options.fetchImpl ? { fetchImpl: options.fetchImpl } : {}),
    });

  const derived = await derivePluginFamily({
    store,
    exchangeClient,
    clientId: godotAdapter.clientId,
    agentAccessToken,
    ...(document?.subject !== undefined ? { expectedSubject: document.subject } : {}),
    serverTarget: document?.serverTarget ?? baseUrl,
    fetchImpl: options.fetchImpl,
    onWarning: ui.warn,
  });
  if (derived.status === 'derived') {
    return pluginAccessToken(derived.document);
  }
  ui.error(
    derived.status === 'exchange-failed'
      ? `Could not derive the tools credential (${derived.reason}).`
      : `Could not derive the tools credential — the credential store changed concurrently (${derived.reason}).`,
  );
  return null;
}
