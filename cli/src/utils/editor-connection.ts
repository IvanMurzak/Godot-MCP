// The connection mode the project's addon runs in, and the connection a command that talks to the editor uses.
//
// Owner rule: a machine signed in to ai-game.dev is never switched to a local server. Only a machine that NEVER signed
// in (machine credential store `missing`) falls back to the project's local server, in memory, with a warning.

import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { verbose } from './ui.js';
import { openMachineStore } from './machine-store.js';
import {
  CLOUD_MCP_URL,
  ENV_CONNECTION_MODE,
  ENV_TOKEN,
  localProjectUrl,
  normalizeEnv,
  resolveCloudAccessToken,
  resolveTargetUrl,
  type ConnectionOptions,
} from './connection.js';

export type GodotConnectionMode = 'Cloud' | 'Custom';

/** `[application] config/name` and the custom user-dir settings of project.godot. */
function readProjectUserDirSettings(projectPath: string): { name: string; customDir: string | undefined } {
  let text = '';
  try {
    text = fs.readFileSync(path.join(projectPath, 'project.godot'), 'utf-8');
  } catch {
    // no project.godot: the defaults below
  }
  const value = (key: string): string | undefined => {
    const m = new RegExp(`^${key.replace(/\//g, '\\/')}\\s*=\\s*(.+)$`, 'm').exec(text);
    return m ? m[1].trim().replace(/^"(.*)"$/, '$1') : undefined;
  };
  const customName = value('config/custom_user_dir_name');
  const useCustom = value('config/use_custom_user_dir') === 'true';
  return { name: value('config/name') ?? '[unnamed project]', customDir: useCustom && customName ? customName : undefined };
}

/** Godot's OS::get_safe_dir_name: path separators and reserved characters become `-`. */
function safeDirName(name: string): string {
  return name.replace(/[\\/:*?"<>|]/g, '-').trim();
}

/**
 * The project's `user://` folder, where the addon persists `godot-mcp-config.json`: `app_userdata/<name>` under
 * Godot's data folder, or the custom user dir the project names.
 */
export function godotUserDataDir(projectPath: string, platform: NodeJS.Platform = process.platform): string {
  const home = os.homedir();
  const dataRoot = platform === 'win32'
    ? process.env.APPDATA ?? path.join(home, 'AppData', 'Roaming')
    : platform === 'darwin'
      ? path.join(home, 'Library', 'Application Support')
      : process.env.XDG_DATA_HOME ?? path.join(home, '.local', 'share');
  const { name, customDir } = readProjectUserDirSettings(projectPath);
  if (customDir) return path.join(dataRoot, safeDirName(customDir));
  return path.join(dataRoot, platform === 'linux' ? 'godot' : 'Godot', 'app_userdata', safeDirName(name));
}

/** `GODOT_MCP_CONNECTION_MODE` in the project's `.env` file. */
function envFileMode(projectPath: string): string | undefined {
  try {
    const text = fs.readFileSync(path.join(projectPath, '.env'), 'utf-8');
    const m = new RegExp(`^\\s*${ENV_CONNECTION_MODE}\\s*=\\s*(.*)$`, 'm').exec(text);
    return m ? normalizeEnv(m[1].replace(/\s+#.*$/, '')) : undefined;
  } catch {
    return undefined;
  }
}

/**
 * The connection mode the project's addon runs in, by the addon's own precedence: the process env, the project's
 * `.env`, the persisted `user://godot-mcp-config.json`, then Cloud (the addon's default).
 */
export function projectConnectionMode(projectPath: string): GodotConnectionMode {
  const pick = (raw: string | undefined): GodotConnectionMode | undefined => {
    const lower = raw?.toLowerCase();
    return lower === 'cloud' ? 'Cloud' : lower === 'custom' ? 'Custom' : undefined;
  };
  const fromEnv = pick(normalizeEnv(process.env[ENV_CONNECTION_MODE])) ?? pick(envFileMode(projectPath));
  if (fromEnv) return fromEnv;
  try {
    const file = path.join(godotUserDataDir(projectPath), 'godot-mcp-config.json');
    const config = JSON.parse(fs.readFileSync(file, 'utf-8')) as { connectionMode?: unknown };
    const persisted = pick(typeof config.connectionMode === 'string' ? config.connectionMode : undefined);
    if (persisted) return persisted;
  } catch {
    // never opened with the addon, or unreadable: the addon's default
  }
  return 'Cloud';
}

export const SIGNED_IN_BUT_UNUSABLE_MESSAGE = 'This machine is signed in to ai-game.dev, but the sign-in cannot be used right now '
  + '(expired, revoked, unreadable, or the credential store is busy). Run `godot-cli login` to sign in again, or retry. '
  + 'The project stays in Cloud mode: the CLI does not switch it to a local server.';

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
  /** The machine credential store's state: `missing` = never signed in on this machine. */
  credentialState: () => 'ok' | 'missing' | 'unreadable';
  /** A usable cloud access token, if any (refreshed through cli-core's provider). */
  readCloudToken: (projectPath: string) => Promise<string | undefined>;
  connectionMode: (projectPath: string) => GodotConnectionMode;
}

/**
 * The connection of a command that talks to the project's editor (`run-tool`, `run-system-tool`, `exec`).
 * An explicit `--url`, `GODOT_MCP_HOST` or a local marker target is used as is. Otherwise the project's mode decides:
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
    credentialState: () => openMachineStore().readState().status,
    readCloudToken: resolveCloudAccessToken,
    connectionMode: projectConnectionMode,
    ...deps,
  };
  const target = resolveTargetUrl(projectPath, options);
  const explicitToken = options.token ?? normalizeEnv(process.env[ENV_TOKEN]);
  let cloudUrl: string;
  if (target.isCloud) cloudUrl = target.url;
  else if (target.derived && d.connectionMode(projectPath) === 'Cloud') cloudUrl = CLOUD_MCP_URL;
  else return { url: target.url, token: explicitToken, cloud: false, localFallback: false };

  const token = explicitToken ?? await d.readCloudToken(projectPath);
  if (token) return { url: cloudUrl, token, cloud: true, localFallback: false };
  if (d.credentialState() !== 'missing') {
    return { url: cloudUrl, token: undefined, cloud: true, localFallback: false, signInRequired: SIGNED_IN_BUT_UNUSABLE_MESSAGE };
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
