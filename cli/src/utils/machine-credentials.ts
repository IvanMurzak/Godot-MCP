import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { execFileSync } from 'child_process';

/**
 * The shared **machine credential store** at `~/.ai-game-dev/` — a single per-machine home for the
 * ai-game.dev account credential (`credentials.json`). Engines, CLIs, and the local server all read
 * this store, so **sign-in happens once per machine** (design `06-engine-plugins.md` / D12).
 *
 * This is the TypeScript peer of the shared C# `MachineCredentialStore`
 * (`MCP-Plugin-dotnet/McpPlugin/src/AgentConfig/MachineCredentialStore.cs`). The on-disk contract is
 * matched byte-for-byte so a credential written by `godot-cli login` is read by the engine plugin
 * (which auto-adopts it on editor boot) and vice-versa:
 *
 * - **POSIX** — `credentials.json` is written plaintext with `0600` permissions inside a `0700`
 *   directory.
 * - **Windows** — the file content is **DPAPI-encrypted** with the current user's key
 *   (`CryptProtectData`, `CurrentUser` scope, no entropy) so the on-disk bytes are never plaintext.
 *   Node has no built-in DPAPI, so we interop via PowerShell's `System.Security.Cryptography.
 *   ProtectedData`, which is a managed wrapper over the very same `CryptProtectData`/
 *   `CryptUnprotectData` calls the C# store uses — the blobs are cross-readable.
 *
 * Credentials are NEVER written to a project file / VCS. The optional `--project` per-project override
 * (project-local `.godot-mcp/credentials.json`, gitignored) is handled by `credentials.ts`, not here.
 */

/** Directory name under the user home that holds the store (matches C# `MachineCredentialStore.DirectoryName`). */
export const MACHINE_STORE_DIR_NAME = '.ai-game-dev';

/** File name of the secret credential document. */
export const MACHINE_CREDENTIALS_FILE_NAME = 'credentials.json';

/**
 * Optional env override for the store directory (advanced use / tests). When set, this exact
 * directory is used verbatim; otherwise the store lives at `~/.ai-game-dev`. The DEFAULT matches the
 * C# store so cross-tool interop holds — the override never changes the production path.
 */
export const MACHINE_STORE_DIR_ENV = 'AI_GAME_DEV_CREDENTIALS_DIR';

// Schema version of the persisted document (matches C# `MachineCredentials.Version`).
const SCHEMA_VERSION = 1;

/**
 * The secret credential material persisted per machine (mirrors the C# `MachineCredentials` document,
 * camelCase field names). Unknown fields are ignored on read so the schema can grow forwards.
 */
export interface MachineCredentials {
  /** Schema version of the persisted document (currently 1). */
  version?: number;
  /** The current short-lived JWT access token (the cloud bearer token). */
  accessToken?: string;
  /** The rotating refresh token used to mint a new access token before `expiresAt`. */
  refreshToken?: string;
  /** Absolute expiry of `accessToken` (ISO 8601); used to schedule proactive refresh. */
  expiresAt?: string;
  /** The server target the credential was issued for (hosted `https://ai-game.dev` or a local URL). */
  serverTarget?: string;
  /** The account id (`sub`) the credential resolves to. Audit/diagnostic only. */
  subject?: string;
}

/** Absolute path of the store directory (honoring the env override). */
export function getMachineStoreDir(baseDirOverride?: string): string {
  if (baseDirOverride) return baseDirOverride;
  const envDir = process.env[MACHINE_STORE_DIR_ENV];
  if (envDir && envDir.trim().length > 0) return envDir;
  return path.join(os.homedir(), MACHINE_STORE_DIR_NAME);
}

/** Absolute path of the secret credential file. */
export function getMachineCredentialsPath(baseDirOverride?: string): string {
  return path.join(getMachineStoreDir(baseDirOverride), MACHINE_CREDENTIALS_FILE_NAME);
}

/** True when a credential file exists in the store. */
export function machineCredentialsExist(baseDirOverride?: string): boolean {
  return fs.existsSync(getMachineCredentialsPath(baseDirOverride));
}

/**
 * Read and (on Windows) decrypt the stored credentials, or `null` when none are present / empty.
 * Throws on a decryption failure or malformed JSON (a corrupt or foreign-user credential); callers on
 * a hot path should use {@link readMachineAccessToken}, which swallows those.
 */
export function readMachineCredentials(baseDirOverride?: string): MachineCredentials | null {
  const credentialsPath = getMachineCredentialsPath(baseDirOverride);
  if (!fs.existsSync(credentialsPath)) {
    return null;
  }
  const raw = fs.readFileSync(credentialsPath);
  if (raw.length === 0) {
    return null;
  }
  const plaintext = isWindows() ? unprotectDpapi(raw) : raw;
  const json = plaintext.toString('utf8');
  if (json.trim().length === 0) {
    return null;
  }
  const parsed = JSON.parse(json) as MachineCredentials;
  return parsed;
}

/**
 * Encrypt (Windows) / restrict (POSIX) and write `credentials` to the store, creating the store
 * directory with owner-only permissions if needed.
 */
export function writeMachineCredentials(credentials: MachineCredentials, baseDirOverride?: string): void {
  const dir = getMachineStoreDir(baseDirOverride);
  const credentialsPath = path.join(dir, MACHINE_CREDENTIALS_FILE_NAME);

  ensureStoreDirectory(dir);

  const json = serialize(credentials);
  const plaintext = Buffer.from(json, 'utf8');
  const bytes = isWindows() ? protectDpapi(plaintext) : plaintext;

  fs.writeFileSync(credentialsPath, bytes, { mode: 0o600 });
  setPosixPermissions(credentialsPath, 0o600);
}

/**
 * Convenience reader for the persisted access token. Swallows a missing / malformed / undecryptable
 * file (returns undefined) so a broken store can never crash a command; `login` re-authenticates and
 * overwrites it.
 */
export function readMachineAccessToken(baseDirOverride?: string): string | undefined {
  let credentials: MachineCredentials | null;
  try {
    credentials = readMachineCredentials(baseDirOverride);
  } catch {
    return undefined;
  }
  const token = credentials?.accessToken;
  return typeof token === 'string' && token.trim().length > 0 ? token : undefined;
}

/** Delete the stored credentials (sign-out). No-op when none exist. */
export function deleteMachineCredentials(baseDirOverride?: string): void {
  const credentialsPath = getMachineCredentialsPath(baseDirOverride);
  if (fs.existsSync(credentialsPath)) {
    fs.rmSync(credentialsPath, { force: true });
  }
}

// ── Serialization ────────────────────────────────────────────────────────────────────────────────

function serialize(credentials: MachineCredentials): string {
  // Mirror the C# store's camelCase + WhenWritingNull: always emit `version`, omit null/undefined
  // optional fields. Indentation is cosmetic (the reader is whitespace-insensitive).
  const doc: Record<string, unknown> = { version: credentials.version ?? SCHEMA_VERSION };
  if (credentials.accessToken != null) doc.accessToken = credentials.accessToken;
  if (credentials.refreshToken != null) doc.refreshToken = credentials.refreshToken;
  if (credentials.expiresAt != null) doc.expiresAt = credentials.expiresAt;
  if (credentials.serverTarget != null) doc.serverTarget = credentials.serverTarget;
  if (credentials.subject != null) doc.subject = credentials.subject;
  return JSON.stringify(doc, null, 2) + '\n';
}

// ── Filesystem permissions ───────────────────────────────────────────────────────────────────────

function ensureStoreDirectory(dir: string): void {
  fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
  setPosixPermissions(dir, 0o700);
}

function setPosixPermissions(target: string, mode: number): void {
  if (isWindows()) return; // chmod is a no-op on Windows; DPAPI provides at-rest protection there.
  try {
    fs.chmodSync(target, mode);
  } catch {
    // Best-effort only — never fail persistence over a chmod (e.g. exotic filesystems).
  }
}

// ── Windows DPAPI via PowerShell (System.Security.Cryptography.ProtectedData) ───────────────────────
// ProtectedData.Protect/Unprotect wrap CryptProtectData/CryptUnprotectData with the SAME CurrentUser
// scope + null entropy the C# MachineCredentialStore uses, so the on-disk blobs are cross-readable.

function isWindows(): boolean {
  return process.platform === 'win32';
}

const DPAPI_PROTECT_SCRIPT =
  "Add-Type -AssemblyName System.Security; " +
  "$in = [Console]::In.ReadToEnd().Trim(); " +
  "$bytes = [Convert]::FromBase64String($in); " +
  "$prot = [System.Security.Cryptography.ProtectedData]::Protect($bytes, $null, 'CurrentUser'); " +
  "[Console]::Out.Write([Convert]::ToBase64String($prot))";

const DPAPI_UNPROTECT_SCRIPT =
  "Add-Type -AssemblyName System.Security; " +
  "$in = [Console]::In.ReadToEnd().Trim(); " +
  "$bytes = [Convert]::FromBase64String($in); " +
  "$plain = [System.Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, 'CurrentUser'); " +
  "[Console]::Out.Write([Convert]::ToBase64String($plain))";

function protectDpapi(plaintext: Buffer): Buffer {
  return runDpapi(DPAPI_PROTECT_SCRIPT, plaintext, 'encrypt');
}

function unprotectDpapi(cipher: Buffer): Buffer {
  return runDpapi(DPAPI_UNPROTECT_SCRIPT, cipher, 'decrypt');
}

function runDpapi(script: string, input: Buffer, op: 'encrypt' | 'decrypt'): Buffer {
  try {
    const out = execFileSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', script], {
      input: input.toString('base64'),
      encoding: 'utf8',
      windowsHide: true,
    });
    return Buffer.from(out.trim(), 'base64');
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    throw new Error(`Failed to ${op} the machine credential store via DPAPI: ${message}`);
  }
}
