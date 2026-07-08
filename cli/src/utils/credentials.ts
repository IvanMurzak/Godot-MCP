import * as fs from 'fs';
import * as path from 'path';

/**
 * Project-local cloud credentials the CLI persists after a successful `login`.
 *
 * Stored SEPARATELY from `.godot-mcp/features.json` (the committable feature
 * override list) because a bearer token must never be committed: `writeCredentials`
 * drops a `.gitignore` alongside so `credentials.json` is ignored while the
 * sibling `features.json` stays version-controllable.
 *
 * `godot-cli login` writes it; `godot-cli open --mode Cloud` (and the direct
 * tool-call path in `connection.ts`) read the token back so no manual `--token`
 * is needed. The addon's own live config still lives in `user://` — this file is
 * purely the CLI's in-project pickup surface, mirroring the Unity CLI's
 * `cloudToken` config field.
 */
export const CREDENTIALS_RELATIVE_PATH = '.godot-mcp/credentials.json';

export interface GodotMcpCredentials {
  /** The cloud bearer token issued by the device-authorization flow. */
  cloudToken?: string;
  /** The cloud base URL the token was issued against (default https://ai-game.dev). */
  cloudBaseUrl?: string;
  [key: string]: unknown;
}

export function getCredentialsPath(projectPath: string): string {
  return path.join(projectPath, CREDENTIALS_RELATIVE_PATH);
}

/** Read the credentials file. Returns null when absent; throws on malformed JSON. */
export function readCredentials(projectPath: string): GodotMcpCredentials | null {
  const credentialsPath = getCredentialsPath(projectPath);
  if (!fs.existsSync(credentialsPath)) {
    return null;
  }
  const json = fs.readFileSync(credentialsPath, 'utf-8');
  try {
    return JSON.parse(json) as GodotMcpCredentials;
  } catch (err) {
    if (err instanceof SyntaxError) {
      throw new SyntaxError(`Malformed JSON in credentials file: ${credentialsPath}\n${err.message}`);
    }
    throw err;
  }
}

/**
 * Write the credentials file, creating the `.godot-mcp/` directory if needed and
 * ensuring `credentials.json` is git-ignored so the token is never committed.
 */
export function writeCredentials(projectPath: string, credentials: GodotMcpCredentials): void {
  const credentialsPath = getCredentialsPath(projectPath);
  const dir = path.dirname(credentialsPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  fs.writeFileSync(credentialsPath, JSON.stringify(credentials, null, 2) + '\n');
  ensureGitignored(dir, 'credentials.json');
}

/**
 * Convenience reader for the persisted cloud token. Swallows a missing or
 * malformed file (returns undefined) so a broken credentials file can never
 * crash `open` / `run-tool`; `login` re-authenticates and overwrites it.
 */
export function readCloudToken(projectPath: string): string | undefined {
  let credentials: GodotMcpCredentials | null;
  try {
    credentials = readCredentials(projectPath);
  } catch {
    return undefined;
  }
  const token = credentials?.cloudToken;
  return typeof token === 'string' && token.trim().length > 0 ? token : undefined;
}

/**
 * Ensure `<dir>/.gitignore` ignores `entry`, so the secret is not committed.
 * Idempotent: creates the file when absent, appends the line only when missing.
 * Best-effort — a failure here never blocks persisting the token.
 */
function ensureGitignored(dir: string, entry: string): void {
  try {
    const gitignorePath = path.join(dir, '.gitignore');
    if (!fs.existsSync(gitignorePath)) {
      fs.writeFileSync(gitignorePath, `${entry}\n`);
      return;
    }
    const existing = fs.readFileSync(gitignorePath, 'utf-8');
    const lines = existing.split(/\r?\n/).map((l) => l.trim());
    if (!lines.includes(entry)) {
      const prefix = existing.length > 0 && !existing.endsWith('\n') ? '\n' : '';
      fs.appendFileSync(gitignorePath, `${prefix}${entry}\n`);
    }
  } catch {
    // Best-effort only — never fail the login over a .gitignore write.
  }
}
