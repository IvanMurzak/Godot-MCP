import * as fs from 'fs';
import * as path from 'path';
import { verbose } from './ui.js';
import * as ui from './ui.js';

// --- Godot MCP connection constants (mirror addons/godot_mcp GodotMcpConfig). ---

/** Default hosted cloud base URL (GodotMcpConfig.DefaultCloudBaseUrl). */
export const DEFAULT_CLOUD_BASE_URL = 'https://ai-game.dev';

/** The MCP hub path appended to a base host (GodotMcpConfig.CloudHubPath). */
export const MCP_HUB_PATH = '/mcp';

/** Resolved cloud MCP-client URL (base + /mcp). */
export const CLOUD_MCP_URL = `${DEFAULT_CLOUD_BASE_URL}${MCP_HUB_PATH}`;

/** Default custom-mode host (GodotMcpConfig.DefaultCustomHost). */
export const DEFAULT_CUSTOM_HOST = 'http://localhost:8080';

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
 * Resolve the MCP server base URL + auth token for direct-tool-call requests
 * (the `<base>/api/tools/<name>` HTTP API exposed by a Godot-MCP server).
 *
 * URL priority:
 *   1. --url flag (explicit override)
 *   2. GODOT_MCP_HOST env (Custom-mode host)
 *   3. GODOT_MCP_CLOUD_URL env / Cloud mode → cloud base (https://ai-game.dev)
 *   4. default custom host (http://localhost:8080)
 *
 * Token priority:
 *   1. --token flag (explicit override)
 *   2. GODOT_MCP_TOKEN env
 *
 * Unlike the Unity CLI, there is NO deterministic project-path→port hash:
 * the Godot plugin is a server-less client whose persisted config lives in
 * `user://` (outside the project tree), so the CLI cannot read a project-local
 * config file. Resolution is therefore env-var + cloud-default based, and
 * `--url` is the authoritative override for a local/self-hosted server.
 */
export function resolveConnection(
  _projectPath: string,
  options: ConnectionOptions,
): { url: string; token: string | undefined } {
  let url: string;
  if (options.url) {
    url = options.url.replace(/\/$/, '');
    verbose(`Using explicit --url: ${url}`);
  } else {
    const envHost = normalizeEnv(process.env[ENV_HOST]);
    const envCloud = normalizeEnv(process.env[ENV_CLOUD_URL]);
    if (envHost) {
      url = envHost.replace(/\/$/, '');
      verbose(`Using ${ENV_HOST}: ${url}`);
    } else if (envCloud) {
      url = envCloud.replace(new RegExp(`${MCP_HUB_PATH}$`), '').replace(/\/$/, '');
      verbose(`Using ${ENV_CLOUD_URL} base: ${url}`);
    } else if (isCloudMode()) {
      url = DEFAULT_CLOUD_BASE_URL;
      verbose(`Using default cloud base URL: ${url}`);
    } else {
      url = DEFAULT_CUSTOM_HOST;
      verbose(`Using default custom host: ${url}`);
    }
  }

  const token = options.token ?? normalizeEnv(process.env[ENV_TOKEN]);
  if (options.token) {
    verbose('Using explicit --token');
  } else if (token) {
    verbose(`Using ${ENV_TOKEN}`);
  }

  return { url, token };
}
