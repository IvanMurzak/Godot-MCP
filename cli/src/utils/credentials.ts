import * as fs from 'fs';
import * as path from 'path';

/**
 * READ-FALLBACK for the legacy project-local cloud credential sink — transition window only
 * (unified-machine-auth 06 D7 / F11.2).
 *
 * Older CLI releases persisted the cloud bearer token to `<project>/.godot-mcp/credentials.json`.
 * The CLI **no longer writes this file anywhere** — a default `login` commits to the shared
 * machine store (`~/.ai-game-dev/`), and `login --project` commits to the per-project store
 * (`<project>/.ai-game-dev/`), both via `@baizor/gamedev-cli-core`. This module only READS a
 * pre-existing legacy file so those users keep working for one release; on first use the
 * credential is migrated into the machine store (`connection.ts` migrate-on-touch), and the f4
 * follow-up removes this fallback entirely. There is deliberately no write function left here —
 * a v2-era write to this sink must never ship.
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

/** Read the legacy credentials file. Returns null when absent; throws on malformed JSON. */
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
 * Convenience reader for the persisted legacy cloud token. Swallows a missing or
 * malformed file (returns undefined) so a broken credentials file can never
 * crash `open` / `run-tool`; `login` writes the cli-core stores instead.
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
