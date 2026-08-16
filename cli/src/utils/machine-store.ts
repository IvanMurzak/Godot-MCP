import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import {
  MachineCredentialStore,
  MachineCredentialLock,
  MachineCredentialProvider,
  HttpTokenRefresher,
  godotAdapter,
  effectiveFamilies,
  MACHINE_STORE_DIR_NAME,
  DEFAULT_CLOUD_BASE_URL,
  type MachineCredentials,
} from '@baizor/gamedev-cli-core';

/**
 * Thin resolution shim over the `@baizor/gamedev-cli-core` machine credential store — the ONLY
 * store implementation this CLI ships (unified-machine-auth 06 D7: "CLI-local store copies …
 * deleted; cli-core is the only TS implementation"). The former local copy
 * (`machine-credentials.ts`, whitelist serializer + non-atomic write) is gone; everything here
 * merely resolves WHERE a store lives and wires the shared provider/lock around cli-core.
 *
 * Two store locations exist:
 *
 *  - **Machine store** — `~/.ai-game-dev/` (or the {@link MACHINE_STORE_DIR_ENV} override):
 *    the per-machine single sign-on credential every engine plugin/CLI shares (design 06 D12).
 *  - **Per-project store** — `<project>/.ai-game-dev/` (the `login --project` override): a
 *    per-project account kept OUT of the legacy `.godot-mcp/credentials.json` sink, which the
 *    CLI no longer writes (06 D7; read-fallback lives in `credentials.ts` until f4 removes it).
 */

/**
 * Optional env override for the machine-store directory (advanced use / tests). When set, this
 * exact directory is used verbatim; otherwise the store lives at `~/.ai-game-dev`. The default
 * matches the C# store so cross-tool interop holds — the override never changes the production
 * path. (cli-core's `MachineCredentialStore` takes an explicit base directory; honoring this env
 * var is the CLI's own affordance, kept for behavioral compatibility.)
 */
export const MACHINE_STORE_DIR_ENV = 'AI_GAME_DEV_CREDENTIALS_DIR';

/** Resolve the machine-store directory: explicit override → env override → `~/.ai-game-dev`. */
export function resolveMachineStoreDir(baseDirOverride?: string): string {
  if (baseDirOverride) return baseDirOverride;
  const envDir = process.env[MACHINE_STORE_DIR_ENV];
  if (envDir && envDir.trim().length > 0) return envDir;
  return path.join(os.homedir(), MACHINE_STORE_DIR_NAME);
}

/** The shared machine credential store (honoring the env override). */
export function openMachineStore(baseDirOverride?: string): MachineCredentialStore {
  return new MachineCredentialStore(resolveMachineStoreDir(baseDirOverride));
}

/** Absolute path of the machine credential file (for user-facing messages). */
export function getMachineCredentialsPath(baseDirOverride?: string): string {
  return openMachineStore(baseDirOverride).credentialsPath;
}

/** The 04 §2 cross-process lock guarding the machine store's directory. */
export function openMachineStoreLock(baseDirOverride?: string): MachineCredentialLock {
  return new MachineCredentialLock(resolveMachineStoreDir(baseDirOverride));
}

/** Directory name of the per-project credential store (same layout as the machine store). */
export const PROJECT_STORE_DIR_NAME = MACHINE_STORE_DIR_NAME;

/** Absolute path of a project's per-project store directory (`<project>/.ai-game-dev`). */
export function resolveProjectStoreDir(projectPath: string): string {
  return path.join(projectPath, PROJECT_STORE_DIR_NAME);
}

/** The per-project credential store (`login --project` — cli-core's documented per-project use). */
export function openProjectStore(projectPath: string): MachineCredentialStore {
  return new MachineCredentialStore(resolveProjectStoreDir(projectPath));
}

/**
 * Ensure `<project>/.ai-game-dev/.gitignore` ignores the credential + lock files, so a
 * per-project credential is never committed while the sibling `project.json` marker stays
 * version-controllable. Idempotent and best-effort — a failure never blocks persisting.
 */
export function ensureProjectStoreGitignored(projectPath: string): void {
  const entries = ['credentials.json', 'credentials.lock', 'credentials.lock.takeover'];
  try {
    const dir = resolveProjectStoreDir(projectPath);
    fs.mkdirSync(dir, { recursive: true });
    const gitignorePath = path.join(dir, '.gitignore');
    if (!fs.existsSync(gitignorePath)) {
      fs.writeFileSync(gitignorePath, entries.join('\n') + '\n');
      return;
    }
    const existing = fs.readFileSync(gitignorePath, 'utf-8');
    const lines = existing.split(/\r?\n/).map((l) => l.trim());
    const missing = entries.filter((e) => !lines.includes(e));
    if (missing.length > 0) {
      const prefix = existing.length > 0 && !existing.endsWith('\n') ? '\n' : '';
      fs.appendFileSync(gitignorePath, `${prefix}${missing.join('\n')}\n`);
    }
  } catch {
    // Best-effort only — never fail a login over a .gitignore write.
  }
}

/** Options for {@link createMachineCredentialProvider}. */
export interface CreateProviderOptions {
  /** Store the provider serves: an explicit directory (per-project store / tests). */
  storeDir?: string;
  /** AS root used when a credential carries no `serverTarget`. Default `https://ai-game.dev`. */
  serverBaseUrl?: string;
  /** Injectable `fetch` for the refresher (tests / fake AS). */
  fetchImpl?: typeof fetch;
  /** Structured warning sink (never receives token material). */
  onWarning?: (message: string) => void;
}

/**
 * Build the cli-core {@link MachineCredentialProvider} for a store — THE single entry point for
 * credential access + refresh (unified-machine-auth 02/04): proactive refresh inside the 60 s
 * skew, reactive refresh on 401, all writes under the 04 §2 cross-process lock, presenting the
 * family's stored `clientId` (`godot-cli` only as the `families.legacy` default — 04 §3.7).
 */
export function createMachineCredentialProvider(
  options: CreateProviderOptions = {},
): MachineCredentialProvider {
  const store = options.storeDir
    ? new MachineCredentialStore(options.storeDir)
    : openMachineStore();
  const refresher = new HttpTokenRefresher({
    defaultServerBaseUrl: options.serverBaseUrl ?? DEFAULT_CLOUD_BASE_URL,
    ...(options.fetchImpl ? { fetchImpl: options.fetchImpl } : {}),
  });
  return new MachineCredentialProvider(store, refresher, {
    defaultClientId: godotAdapter.clientId, // 'godot-cli'
    ...(options.onWarning ? { onWarning: options.onWarning } : {}),
  });
}

/**
 * True when `document` holds at least one usable (access-token-bearing) family — the families
 * view is schema-version independent (a v1 document reads as `families.legacy`).
 */
export function hasUsableFamily(document: MachineCredentials | null): boolean {
  if (!document) return false;
  return Object.values(effectiveFamilies(document)).some(
    (family) => typeof family?.accessToken === 'string' && family.accessToken.trim().length > 0,
  );
}

/**
 * True when `document` holds a usable **plugin-plane** credential — `families.plugin`, falling
 * back to `families.legacy` (an adopted v1 credential IS the plugin-plane credential), mirroring
 * the provider's plane resolution. An **agent-only** store (the F1 `partial` state: exchange
 * failed after the agent family committed) is NOT signed in for CLI purposes — every command
 * consumes the plugin plane, so treating the agent family as "signed in" wedges `login` into
 * "Already authenticated" while every command raises `login required` (review f2 B1).
 */
export function hasPluginPlaneCredential(document: MachineCredentials | null): boolean {
  if (!document) return false;
  const families = effectiveFamilies(document);
  const plane = families.plugin ?? families.legacy;
  return typeof plane?.accessToken === 'string' && plane.accessToken.trim().length > 0;
}
