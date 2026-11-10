// The connection mode the project's addon runs in, and the connection a command that talks to the editor uses.
//
// Owner rule: a machine signed in to ai-game.dev is never switched to a local server. Only a machine that NEVER signed
// in (no machine store, no per-project store, no legacy credential) falls back to the project's local server, in
// memory, with a warning.

import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { verbose } from './ui.js';
import { ExitCode } from './exit-codes.js';
import type { CommandOutput } from './output.js';
import { openMachineStore, openProjectStore } from './machine-store.js';
import { getCredentialsPath } from './credentials.js';
import { isLocalhostUrl, readProjectMarker } from './project-marker.js';
import {
  DEFAULT_CLOUD_BASE_URL,
  ENV_CLOUD_URL,
  ENV_CONNECTION_MODE,
  ENV_HOST,
  ENV_TOKEN,
  ensureMcpHubPath,
  localProjectUrl,
  normalizeEnv,
  resolveCloudAccessToken,
  type ConnectionOptions,
} from './connection.js';

export type GodotConnectionMode = 'Cloud' | 'Custom';

/** The `GODOT_MCP_*` keys the addon reads from the project's `.env`. */
const ENV_FILE_KEYS = [ENV_CONNECTION_MODE, ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN];

/** Strip whitespace and one pair of wrapping double or single quotes (the addon's `.env` sanitizer). */
function sanitizeEnvValue(raw: string): string | undefined {
  let v = raw.trim();
  if (v.length >= 2 && ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'")))) v = v.slice(1, -1).trim();
  return v.length > 0 ? v : undefined;
}

/** The project's `.env`, parsed like `GodotMcpEnvFile.Parse`: `#` lines skipped, split on the first `=`, last wins. */
export function readProjectEnvFile(projectPath: string): Record<string, string> {
  let text: string;
  try {
    text = fs.readFileSync(path.join(projectPath, '.env'), 'utf-8');
  } catch {
    return {};
  }
  const values: Record<string, string> = {};
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (line.length === 0 || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim();
    if (!ENV_FILE_KEYS.includes(key)) continue;
    const value = sanitizeEnvValue(line.slice(eq + 1));
    if (value !== undefined) values[key] = value;
  }
  return values;
}

/** `config/name` and the custom user-dir settings of project.godot. */
function readProjectUserDirSettings(projectPath: string): { name: string; customDir: string | undefined } {
  let text = '';
  try {
    text = fs.readFileSync(path.join(projectPath, 'project.godot'), 'utf-8');
  } catch {
    // no project.godot: the defaults below
  }
  const value = (key: string): string | undefined => {
    const m = new RegExp(`^${key.replace(/\//g, '\\/')}\\s*=\\s*(.+)$`, 'm').exec(text);
    if (!m) return undefined;
    const v = m[1].trim();
    return v.startsWith('"') && v.endsWith('"') ? v.slice(1, -1).replace(/\\"/g, '"').replace(/\\\\/g, '\\') : v;
  };
  const customName = value('config/custom_user_dir_name');
  const useCustom = value('config/use_custom_user_dir') === 'true';
  return { name: value('config/name') ?? '[unnamed project]', customDir: useCustom && customName ? customName : undefined };
}

/** Godot's `String::validate_filename`-style `get_safe_dir_name`: reserved characters become `-`. */
function safeDirName(name: string, allowPaths: boolean): string {
  const reserved = allowPaths ? /[:*?"<>|]/g : /[:*?"<>|\\/]/g;
  return name.replace(reserved, '-').trim().replace(/[. ]+$/, '');
}

/**
 * The project's `user://` folder, where the addon persists `godot-mcp-config.json`: `app_userdata/<name>` under
 * Godot's data folder, or the custom user dir the project names (which may contain `/`). Self-contained editors
 * (`._sc_`) keep it elsewhere and are not covered.
 */
export function godotUserDataDir(projectPath: string, platform: NodeJS.Platform = process.platform): string {
  const home = os.homedir();
  const dataRoot = platform === 'win32'
    ? process.env.APPDATA ?? path.join(home, 'AppData', 'Roaming')
    : platform === 'darwin'
      ? path.join(home, 'Library', 'Application Support')
      : process.env.XDG_DATA_HOME ?? path.join(home, '.local', 'share');
  const { name, customDir } = readProjectUserDirSettings(projectPath);
  if (customDir) return path.join(dataRoot, safeDirName(customDir, true));
  return path.join(dataRoot, platform === 'linux' ? 'godot' : 'Godot', 'app_userdata', safeDirName(name, false));
}

interface PersistedConfig {
  connectionMode?: unknown;
  cloudBaseUrl?: unknown;
}

function readPersistedConfig(projectPath: string): PersistedConfig {
  try {
    return JSON.parse(fs.readFileSync(path.join(godotUserDataDir(projectPath), 'godot-mcp-config.json'), 'utf-8')) as PersistedConfig;
  } catch {
    return {};
  }
}

function pickMode(raw: unknown): GodotConnectionMode | undefined {
  const lower = typeof raw === 'string' ? raw.trim().toLowerCase() : undefined;
  return lower === 'cloud' ? 'Cloud' : lower === 'custom' ? 'Custom' : undefined;
}

export interface ProjectConnection {
  mode: GodotConnectionMode;
  /** Custom mode: a loopback host the `.env` or the marker names (else the project's derived port). */
  customHost?: string;
  /** Cloud mode: the hub URL (`.env`, persisted `cloudBaseUrl`, marker origin, else ai-game.dev). */
  cloudUrl: string;
  /** A token from the project's `.env`. */
  envFileToken?: string;
}

/**
 * The connection the project's addon uses, by the addon's own precedence: process env mode, the `.env` (its mode, or
 * a loopback host → Custom), the persisted `user://godot-mcp-config.json`, the enrolled marker, then Cloud.
 */
export function readProjectConnection(projectPath: string): ProjectConnection {
  const envFile = readProjectEnvFile(projectPath);
  const persisted = readPersistedConfig(projectPath);
  const rawTarget = readProjectMarker(projectPath)?.serverTarget;
  const marker = typeof rawTarget === 'string' && rawTarget.trim().length > 0 ? rawTarget.trim() : undefined;
  const fileHost = envFile[ENV_HOST];
  const mode = pickMode(normalizeEnv(process.env[ENV_CONNECTION_MODE]))
    ?? pickMode(envFile[ENV_CONNECTION_MODE])
    ?? (fileHost !== undefined && isLocalhostUrl(fileHost) ? 'Custom' : undefined)
    ?? pickMode(persisted.connectionMode)
    ?? (marker !== undefined ? (isLocalhostUrl(marker) ? 'Custom' : 'Cloud') : undefined)
    ?? 'Cloud';
  const persistedCloud = typeof persisted.cloudBaseUrl === 'string' && persisted.cloudBaseUrl.trim() ? persisted.cloudBaseUrl.trim() : undefined;
  const cloudBase = envFile[ENV_CLOUD_URL] ?? persistedCloud ?? (marker !== undefined && !isLocalhostUrl(marker) ? marker : undefined) ?? DEFAULT_CLOUD_BASE_URL;
  const customHost = fileHost !== undefined && isLocalhostUrl(fileHost) ? fileHost : marker !== undefined && isLocalhostUrl(marker) ? marker : undefined;
  return {
    mode,
    cloudUrl: ensureMcpHubPath(cloudBase),
    ...(customHost ? { customHost: customHost.replace(/\/+$/, '') } : {}),
    ...(envFile[ENV_TOKEN] ? { envFileToken: envFile[ENV_TOKEN] } : {}),
  };
}

/** The addon's mode for the project (see {@link readProjectConnection}). */
export function projectConnectionMode(projectPath: string): GodotConnectionMode {
  return readProjectConnection(projectPath).mode;
}

/**
 * Whether this machine (or this project) ever signed in to ai-game.dev: `missing` only when the machine store, the
 * per-project store and the legacy project credential are all absent. `project` = the per-project sign-in exists.
 */
export function signInState(projectPath: string): 'missing' | 'machine' | 'project' {
  if (openProjectStore(projectPath).readState().status !== 'missing' || fs.existsSync(getCredentialsPath(projectPath))) return 'project';
  return openMachineStore().readState().status !== 'missing' ? 'machine' : 'missing';
}

export const SIGNED_IN_BUT_UNUSABLE_MESSAGE = 'This machine is signed in to ai-game.dev, but the sign-in cannot be used right now '
  + '(expired, revoked, unreadable, or the credential store is busy). Run `godot-cli login` to sign in again, or retry. '
  + 'The project stays in Cloud mode: the CLI does not switch it to a local server.';

export const PROJECT_SIGNED_IN_BUT_UNUSABLE_MESSAGE = 'This project is signed in to ai-game.dev, but the sign-in cannot be used right now '
  + '(expired, revoked, unreadable, or the credential store is busy). Run `godot-cli login --project <path>` to sign in '
  + 'again, or retry. The project stays in Cloud mode: the CLI does not switch it to a local server.';

export interface EditorConnection {
  url: string;
  token: string | undefined;
  /** The ai-game.dev cloud hub: only an editor connected there answers; the CLI starts none. */
  cloud: boolean;
  /** Cloud mode on a machine that never signed in: the project's local server is used (in memory only). */
  localFallback: boolean;
  /** Cloud mode with a sign-in that cannot be used: the command refuses (NOT_AUTHENTICATED) with this message. */
  signInRequired?: string;
}

export interface EditorConnectionDeps {
  signInState: (projectPath: string) => 'missing' | 'machine' | 'project';
  /** A usable cloud access token, if any (refreshed through cli-core's provider). */
  readCloudToken: (projectPath: string) => Promise<string | undefined>;
  projectConnection: (projectPath: string) => ProjectConnection;
}

/**
 * The connection of a command that talks to the project's editor (`run-tool`, `run-system-tool`, `exec`).
 * An explicit `--url` or `GODOT_MCP_HOST` (process env) is used as is. Otherwise the project's mode decides:
 * Custom → its local server; Cloud → the cloud hub with the sign-in's (or an explicit) token. Only a machine that
 * NEVER signed in to ai-game.dev falls back to the project's local server. A sign-in that exists but cannot be used
 * is an error: a signed-in machine is never switched to a local server.
 */
export async function resolveEditorConnection(
  projectPath: string,
  options: ConnectionOptions,
  deps: Partial<EditorConnectionDeps> = {},
): Promise<EditorConnection> {
  const d: EditorConnectionDeps = {
    signInState,
    readCloudToken: resolveCloudAccessToken,
    projectConnection: readProjectConnection,
    ...deps,
  };
  const explicitToken = options.token ?? normalizeEnv(process.env[ENV_TOKEN]);
  const explicitUrl = options.url ?? normalizeEnv(process.env[ENV_HOST]);
  if (explicitUrl) return { url: explicitUrl.replace(/\/+$/, ''), token: explicitToken, cloud: false, localFallback: false };

  const project = d.projectConnection(projectPath);
  // GODOT_MCP_CLOUD_URL only moves the hub (as in the addon); it does not turn a Custom project into a Cloud one.
  const envCloud = normalizeEnv(process.env[ENV_CLOUD_URL]);
  if (project.mode === 'Custom') {
    return { url: project.customHost ?? localProjectUrl(projectPath), token: explicitToken ?? project.envFileToken, cloud: false, localFallback: false };
  }

  const cloudUrl = envCloud ? ensureMcpHubPath(envCloud) : project.cloudUrl;
  const token = explicitToken ?? project.envFileToken ?? await d.readCloudToken(projectPath);
  if (token) return { url: cloudUrl, token, cloud: true, localFallback: false };
  const state = d.signInState(projectPath);
  if (state !== 'missing') {
    return {
      url: cloudUrl, token: undefined, cloud: true, localFallback: false,
      signInRequired: state === 'project' ? PROJECT_SIGNED_IN_BUT_UNUSABLE_MESSAGE : SIGNED_IN_BUT_UNUSABLE_MESSAGE,
    };
  }
  const url = localProjectUrl(projectPath);
  verbose(`Cloud mode without an ai-game.dev sign-in on this machine: using the project's local server ${url}`);
  return { url, token: undefined, cloud: false, localFallback: true };
}

/** The warning a command prints when it fell back to the local server. */
export function localFallbackNote(url: string): string {
  return `Not signed in to ai-game.dev on this machine, so the project's local MCP server (${url}) is used; an editor `
    + 'started now serves it (the project stays in Cloud mode). Run `godot-cli login` to use Cloud mode.';
}

/**
 * {@link resolveEditorConnection} for a command: an unusable sign-in fails it (NOT_AUTHENTICATED, exit 6), a local
 * fallback is warned about (stderr / the envelope).
 */
export async function connectionForCommand(output: CommandOutput<string>, projectPath: string, options: ConnectionOptions): Promise<EditorConnection> {
  const conn = await resolveEditorConnection(projectPath, options);
  if (conn.signInRequired) output.fail(ExitCode.CommandFailed, 'NOT_AUTHENTICATED', conn.signInRequired);
  if (conn.localFallback) output.warn(localFallbackNote(conn.url));
  return conn;
}

export const CLOUD_PROJECT_NO_LOCAL_EDITOR_MESSAGE = 'This project is in Cloud mode and this machine is signed in to '
  + 'ai-game.dev, so the CLI does not start an editor on a local server for it. Open the editor with a window '
  + '(`godot-cli open`; it connects to ai-game.dev) and call tools with `godot-cli run-tool` / `godot-cli exec`, which go '
  + 'through ai-game.dev. To use a local server anyway, pass its URL with --url.';

export const NOT_LOCAL_SERVER_MESSAGE = 'A headless editor runs on a local MCP server, but the server to use is not '
  + 'local (--url / GODOT_MCP_HOST). Pass a localhost --url, or call tools on that server with `godot-cli run-tool`.';

/**
 * Why `open --headless` may not start an editor on a local server for this connection (exit 6), or `undefined` when it
 * may. A signed-in machine is never switched to a local server: an unusable sign-in fails (its NOT_AUTHENTICATED
 * message), a working one keeps the project on the cloud. A token without a sign-in on this machine (`--token`,
 * `GODOT_MCP_TOKEN`, the project's `.env`) still gets the local server with that token.
 */
export function localEditorRefusal(conn: EditorConnection, signedIn: () => boolean): string | undefined {
  if (conn.signInRequired) return conn.signInRequired;
  if (conn.cloud) return signedIn() ? CLOUD_PROJECT_NO_LOCAL_EDITOR_MESSAGE : undefined;
  return isLocalhostUrl(conn.url) ? undefined : NOT_LOCAL_SERVER_MESSAGE;
}
