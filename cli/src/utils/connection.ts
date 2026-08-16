import * as fs from 'fs';
import * as path from 'path';
import { verbose } from './ui.js';
import * as ui from './ui.js';
import { readCredentials, readCloudToken } from './credentials.js';
import {
  createMachineCredentialProvider,
  openMachineStore,
  openMachineStoreLock,
  openProjectStore,
  resolveProjectStoreDir,
  hasUsableFamily,
} from './machine-store.js';
import { deriveProjectIdentityV2 } from './project-identity.js';
import { readProjectMarker, isLocalhostUrl } from './project-marker.js';

// --- Godot MCP connection constants (mirror addons/godot_mcp GodotMcpConfig). ---

/** Default hosted cloud base URL (GodotMcpConfig.DefaultCloudBaseUrl). */
export const DEFAULT_CLOUD_BASE_URL = 'https://ai-game.dev';

/** The MCP hub path appended to a base host (GodotMcpConfig.CloudHubPath). */
export const MCP_HUB_PATH = '/mcp';

/** Resolved cloud MCP-client URL (base + /mcp). */
export const CLOUD_MCP_URL = `${DEFAULT_CLOUD_BASE_URL}${MCP_HUB_PATH}`;

// --- Environment-variable names the addon reads (GodotMcpConfig EnvX consts). ---

export const ENV_CLOUD_URL = 'GODOT_MCP_CLOUD_URL';
export const ENV_HOST = 'GODOT_MCP_HOST';
export const ENV_TOKEN = 'GODOT_MCP_TOKEN';
export const ENV_CONNECTION_MODE = 'GODOT_MCP_CONNECTION_MODE';
export const ENV_AUTH_OPTION = 'GODOT_MCP_AUTH_OPTION';
export const ENV_LOG_LEVEL = 'GODOT_MCP_LOG_LEVEL';

export interface ConnectionOptions {
  path?: string;
  url?: string;
  token?: string;
}

/**
 * Returns true if the given directory looks like a Godot project — i.e.
 * it contains a `project.godot` file. Pure / no I/O beyond `fs.existsSync`.
 */
export function isGodotProject(dir: string): boolean {
  return fs.existsSync(path.join(dir, 'project.godot'));
}

/**
 * Resolve the project path from positional arg, --path option, or cwd.
 */
export function resolveProjectPath(positionalPath: string | undefined, options: ConnectionOptions): string {
  const resolved = path.resolve(positionalPath ?? options.path ?? process.cwd());
  if ((positionalPath !== undefined || options.path !== undefined) && !fs.existsSync(resolved)) {
    ui.error(`Project path does not exist: ${resolved}`);
    process.exit(1);
  }
  return resolved;
}

/**
 * Resolve the project path and validate it is a Godot project.
 * Skips validation when --url is provided (explicit server override).
 */
export function resolveAndValidateProjectPath(positionalPath: string | undefined, options: ConnectionOptions): string {
  const resolved = resolveProjectPath(positionalPath, options);

  // Skip Godot project validation when --url is explicitly provided
  if (options.url) {
    return resolved;
  }

  if (!isGodotProject(resolved)) {
    ui.error(`Not a Godot project (missing project.godot): ${resolved}`);
    ui.info('Provide a Godot project path as an argument, or use --url to connect to a server directly.');
    process.exit(1);
  }

  return resolved;
}

/**
 * Strip a single pair of wrapping double-quotes and surrounding whitespace
 * from an environment value, mirroring the addon's `GodotMcpConfig.NormalizeEnv`
 * so `GODOT_MCP_TOKEN="abc"` yields `abc`.
 */
function normalizeEnv(raw: string | undefined): string | undefined {
  if (raw === undefined) return undefined;
  const trimmed = raw.trim();
  if (trimmed.length === 0) return undefined;
  return trimmed.replace(/^"(.*)"$/, '$1');
}

/** True when the configured/env mode is Cloud (case-insensitive, name only). */
function isCloudMode(): boolean {
  const mode = normalizeEnv(process.env[ENV_CONNECTION_MODE]);
  return mode !== undefined && mode.toLowerCase() === 'cloud';
}

/**
 * Normalize a cloud/hub base URL so it ends in exactly one `/mcp` hub segment
 * (mirrors `setup-mcp`'s `resolveMcpClientUrl`). The mcp-server's REST
 * direct-tool API is reachable ONLY under the `/mcp`-prefixed route — nginx maps
 * `^/mcp(/|$)` to the mcp-server (stripping `/mcp`) while `/api` alone goes to the
 * cloud backend — so a cloud base that LOSES `/mcp` sends `<base>/api/tools/<name>`
 * to the backend, which 404s (defect A). Trailing slash trimmed; an already-`/mcp`
 * base is left as-is (case-insensitive, idempotent).
 */
function ensureMcpHubPath(baseUrl: string): string {
  const trimmed = baseUrl.replace(/\/$/, '');
  if (trimmed.toLowerCase().endsWith(MCP_HUB_PATH)) return trimmed;
  return trimmed + MCP_HUB_PATH;
}

/** A resolved connection target: the base URL + whether it is the cloud hub. */
interface ResolvedTarget {
  /** The base URL to which `<base>/api/tools/<name>` is appended. */
  url: string;
  /** True when the target is the cloud hub — a persisted cloud token then applies. */
  isCloud: boolean;
}

/**
 * Resolve the base URL a direct-tool-call POSTs to, plus whether that target is
 * the cloud hub. Priority (first match wins):
 *   1. `--url` flag — explicit override, verbatim (trailing slash trimmed).
 *   2. `GODOT_MCP_HOST` env — an explicit Custom-mode host, verbatim.
 *   3. `GODOT_MCP_CLOUD_URL` env — a cloud base, normalized to its `/mcp` hub URL (fix A).
 *   4. `GODOT_MCP_CONNECTION_MODE=Cloud` — the default hosted `/mcp` hub (fix A).
 *   5. Enrolled project marker (`.ai-game-dev/project.json` `serverTarget`) — the
 *      ZERO-env-config path (fix B): a hosted target routes to its `/mcp` hub (cloud,
 *      so an enrolled project reaches the cloud with no env var set); a localhost
 *      target is used verbatim (the derived port enrollment recorded).
 *   6. Local fallback — `http://localhost:<v2-derived-port>`, the deterministic port
 *      the addon binds locally (fix B; replaces the dead `http://localhost:8080`). An
 *      explicit marker `portOverride` still wins over the hash-derived port.
 */
function resolveTargetUrl(projectPath: string, options: ConnectionOptions): ResolvedTarget {
  if (options.url) {
    const url = options.url.replace(/\/$/, '');
    verbose(`Using explicit --url: ${url}`);
    return { url, isCloud: false };
  }

  const envHost = normalizeEnv(process.env[ENV_HOST]);
  if (envHost) {
    const url = envHost.replace(/\/$/, '');
    verbose(`Using ${ENV_HOST}: ${url}`);
    return { url, isCloud: false };
  }

  const envCloud = normalizeEnv(process.env[ENV_CLOUD_URL]);
  if (envCloud) {
    const url = ensureMcpHubPath(envCloud);
    verbose(`Using ${ENV_CLOUD_URL} hub URL: ${url}`);
    return { url, isCloud: true };
  }

  if (isCloudMode()) {
    verbose(`Using default cloud hub URL: ${CLOUD_MCP_URL}`);
    return { url: CLOUD_MCP_URL, isCloud: true };
  }

  // The enrolled marker (and any port override) is read once here — it backs both
  // the zero-config marker target (5) and the derived-port fallback (6).
  const marker = readProjectMarker(projectPath);
  const rawTarget = marker?.serverTarget;
  const serverTarget =
    typeof rawTarget === 'string' && rawTarget.trim().length > 0 ? rawTarget.trim() : undefined;
  if (serverTarget) {
    if (isLocalhostUrl(serverTarget)) {
      const url = serverTarget.replace(/\/$/, '');
      verbose(`Using enrolled marker localhost target: ${url}`);
      return { url, isCloud: false };
    }
    const url = ensureMcpHubPath(serverTarget);
    verbose(`Using enrolled marker cloud hub URL: ${url}`);
    return { url, isCloud: true };
  }

  const portOverride = typeof marker?.portOverride === 'number' ? marker.portOverride : null;
  const { port } = deriveProjectIdentityV2(projectPath, portOverride);
  const url = `http://localhost:${port}`;
  verbose(`Using derived local port fallback: ${url}`);
  return { url, isCloud: false };
}

/**
 * Resolve the MCP server base URL + auth token for direct-tool-call requests
 * (the `<base>/api/tools/<name>` HTTP API exposed by a Godot-MCP server).
 *
 * URL priority — see {@link resolveTargetUrl}. A cloud target carries the `/mcp`
 * hub segment so `<base>/api/tools/<name>` reaches the hub (not the 404'ing
 * backend); a local target is the addon's v2 derived port (or the enrolled
 * marker's recorded localhost target).
 *
 * Token priority:
 *   1. --token flag (explicit override)
 *   2. GODOT_MCP_TOKEN env
 *   3. persisted cloud credential (cloud targets only — env Cloud mode OR an enrolled
 *      hosted marker) — see {@link resolveCloudAccessToken}: the per-project store
 *      `<project>/.ai-game-dev/` (a `--project` login), then the legacy read-fallback
 *      `<project>/.godot-mcp/credentials.json` (migrate-on-touch, never written — 06 D7),
 *      then the shared machine store `~/.ai-game-dev/` (a default `godot-cli login`), so a
 *      sign-once-per-machine credential is picked up here too. Machine/per-project stores are
 *      served through the cli-core {@link createMachineCredentialProvider provider}, which
 *      proactively refreshes an (about-to-)expired token under the cross-process lock — the CLI
 *      works through expiry instead of handing out a stale bearer.
 *      An enrolled cloud project therefore authenticates with zero env config.
 */
export async function resolveConnection(
  projectPath: string,
  options: ConnectionOptions,
): Promise<{ url: string; token: string | undefined }> {
  const { url, isCloud } = resolveTargetUrl(projectPath, options);

  let token = options.token ?? normalizeEnv(process.env[ENV_TOKEN]);
  if (options.token) {
    verbose('Using explicit --token');
  } else if (token) {
    verbose(`Using ${ENV_TOKEN}`);
  } else if (isCloud) {
    token = await resolveCloudAccessToken(projectPath);
  }

  return { url, token };
}

/**
 * Resolve the persisted cloud access token for a cloud target, working through expiry via the
 * cli-core `MachineCredentialProvider` (unified-machine-auth 02/04 — proactive refresh under the
 * cross-process lock, presenting each family's stored `clientId`):
 *
 *   1. **Per-project store** `<project>/.ai-game-dev/` (a `login --project` credential) — when it
 *      holds a usable credential it WINS and its unusable states are surfaced, never silently
 *      shadowed by the machine account.
 *   2. **Legacy read-fallback** `<project>/.godot-mcp/credentials.json` (transition window, 06
 *      D7): still honored for one release, NEVER written, and **migrated on touch** into the
 *      machine store (under the lock) when the machine store is empty — so the credential
 *      survives the f4 removal of this fallback.
 *   3. **Machine store** `~/.ai-game-dev/` via the provider.
 *
 * Returns undefined when signed out / the store is unusable — the command then proceeds
 * unauthenticated exactly as before (the server answers 401 and the user is pointed at `login`).
 */
async function resolveCloudAccessToken(projectPath: string): Promise<string | undefined> {
  // 1. Per-project store (a `login --project` credential).
  const projectStore = openProjectStore(projectPath);
  if (hasUsableFamily(safeRead(() => projectStore.read()))) {
    const provider = createMachineCredentialProvider({
      storeDir: resolveProjectStoreDir(projectPath),
      onWarning: ui.warn,
    });
    try {
      const token = await provider.getAccessToken();
      verbose('Using persisted cloud token (per-project store)');
      return token;
    } catch (err) {
      verbose(`Per-project credential unusable: ${err instanceof Error ? err.message : String(err)}`);
      return undefined;
    }
  }

  // 2. Legacy project-store read-fallback (+ migrate-on-touch; never written — 06 D7, f4 removes).
  const legacyToken = readCloudToken(projectPath);
  if (legacyToken) {
    await migrateLegacyProjectCredential(projectPath, legacyToken);
    verbose('Using persisted cloud token (legacy project store read-fallback)');
    return legacyToken;
  }

  // 3. Shared machine store via the provider (refreshes through expiry).
  const provider = createMachineCredentialProvider({ onWarning: ui.warn });
  try {
    const token = await provider.getAccessToken();
    verbose('Using persisted cloud token (machine store)');
    return token;
  } catch (err) {
    verbose(`No usable machine credential: ${err instanceof Error ? err.message : String(err)}`);
    return undefined;
  }
}

/**
 * F11.2 migrate-on-touch: on first use of a legacy `<project>/.godot-mcp/credentials.json`
 * credential, adopt it into the shared machine store — as a `families.legacy` family (mint
 * client unknown by definition), under the 04 §2 cross-process lock, and ONLY when the machine
 * store is empty (a present credential is never overwritten by a migration). The legacy file
 * itself is left untouched: the read-fallback stays for this release and f4 removes it.
 * Best-effort — a busy lock or unreadable store never blocks the command.
 */
async function migrateLegacyProjectCredential(projectPath: string, legacyToken: string): Promise<void> {
  try {
    const store = openMachineStore();
    if (store.readState().status !== 'missing') return;
    const serverTarget = safeRead(() => readCredentials(projectPath))?.cloudBaseUrl;
    const lock = openMachineStoreLock();
    await lock.withLock(() => {
      // Double-checked under the lock: a peer may have signed in while we waited.
      if (store.readState().status !== 'missing') return;
      store.writeFamily(
        'legacy',
        { accessToken: legacyToken },
        { serverTarget: typeof serverTarget === 'string' ? serverTarget : DEFAULT_CLOUD_BASE_URL },
      );
      verbose('Migrated the legacy project credential into the machine store (families.legacy)');
    });
  } catch (err) {
    // Never log token material; the reason is safe.
    verbose(`Legacy credential migration skipped: ${err instanceof Error ? err.message : String(err)}`);
  }
}

/** Run a store read, mapping any throw (malformed/unreadable) to null. */
function safeRead<T>(read: () => T | null): T | null {
  try {
    return read();
  } catch {
    return null;
  }
}

/**
 * Resolve the auth token to inject into the launched editor's environment for
 * the `open` command. The editor inherits the CLI's environment, so a set
 * `GODOT_MCP_TOKEN` already propagates untouched — this only needs to surface
 * the persisted cloud token (which is NOT in the environment) when Cloud mode is
 * selected and neither `--token` nor `GODOT_MCP_TOKEN` is present.
 *
 * Returns the token to pass as `openProject({ token })`, or undefined to leave
 * `open`'s env exactly as before (explicit env var / no token).
 */
export async function resolveOpenAuthToken(
  projectPath: string,
  options: { token?: string; mode?: string },
): Promise<string | undefined> {
  if (options.token !== undefined) {
    return options.token;
  }
  // An env token already reaches the editor via inheritance — don't double-inject.
  if (normalizeEnv(process.env[ENV_TOKEN]) !== undefined) {
    return undefined;
  }
  const selectedMode = options.mode ?? normalizeEnv(process.env[ENV_CONNECTION_MODE]);
  if (selectedMode !== undefined && selectedMode.toLowerCase() === 'cloud') {
    return resolveCloudAccessToken(projectPath);
  }
  return undefined;
}
