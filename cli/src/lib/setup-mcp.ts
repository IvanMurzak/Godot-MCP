import * as path from 'path';
import { pinUrl } from '@baizor/gamedev-cli-core';
import {
  CLOUD_MCP_URL,
  DEFAULT_CUSTOM_HOST,
  ENV_HOST,
  ENV_TOKEN,
  MCP_HUB_PATH,
} from '../utils/connection.js';
import {
  getAgentById,
  getAgentIds,
  writeJsonAgentConfig,
  writeTomlAgentConfig,
  MCP_SERVER_NAME,
} from '../utils/agents.js';
import { derivePinV2 } from '../utils/project-identity.js';
import { emitProgress } from './progress.js';
import { requireExistingPath } from './validation.js';
import type { SetupMcpOptions, SetupMcpResult } from './types.js';

/**
 * Normalize an env-supplied value (trim + strip a single wrapping double-quote
 * pair), mirroring the addon's `GodotMcpConfig.NormalizeEnv`.
 */
function normalizeEnv(raw: string | undefined): string | undefined {
  if (raw === undefined) return undefined;
  const trimmed = raw.trim();
  if (trimmed.length === 0) return undefined;
  return trimmed.replace(/^"(.*)"$/, '$1');
}

/**
 * Resolve the MCP-client URL an external AI agent should connect to. This is
 * the `<host>/mcp` streamable-HTTP endpoint (NOT the plugin's
 * `<host>/hub/mcp-server` SignalR endpoint), matching the addon's
 * `GodotMcpConfig.ResolveMcpClientUrl`:
 *   1. explicit `--url` override (host) → `<host>/mcp`
 *   2. GODOT_MCP_HOST env (Custom host) → `<host>/mcp`
 *   3. default cloud MCP URL (https://ai-game.dev/mcp)
 */
function resolveMcpClientUrl(optUrl: string | undefined): string {
  const host = optUrl ?? normalizeEnv(process.env[ENV_HOST]);
  if (!host) return CLOUD_MCP_URL;

  const trimmed = host.replace(/\/$/, '');
  if (trimmed.toLowerCase().endsWith(MCP_HUB_PATH)) return trimmed;
  return trimmed + MCP_HUB_PATH;
}

/**
 * Decide whether `setup-mcp` writes a static `Authorization: Bearer` header for a
 * given agent + credential (design decision D11 / auth Flow A & C, mirroring the
 * shared b6 configurators' `SupportsOAuth` policy).
 *
 * OAuth-capable interactive clients (Claude Code, Cursor, Codex, Copilot, …)
 * perform native RFC 9728 discovery + OAuth against the hosted endpoint (Flow A).
 * A static Bearer header BOTH fails there (the hosted AS rejects the token with
 * 401) AND suppresses the client's own OAuth handshake ("OAuth fallback is
 * disabled when headers.Authorization is set"), so it must be omitted — the
 * config stays URL-only `{type,url}`.
 *
 * A header is written ONLY for the Flow C fallback:
 *   - `supportsOAuth === false` — a client that cannot OAuth and needs a static
 *     token against a required-auth (typically self-hosted) endpoint; OR
 *   - an EXPLICIT PAT opt-in — the caller passed the token explicitly (`--token`
 *     / the library `token` arg), a deliberate Flow C request.
 *
 * A token that is only present ambiently (the `GODOT_MCP_TOKEN` env a prior
 * `login` set) is NOT an opt-in: for an OAuth-capable client it is ignored so the
 * native handshake runs. This is the flagship g1 fix — `login` + `setup-mcp
 * claude-code .` must no longer emit a header.
 */
export function shouldWriteAuthHeader(input: {
  hasToken: boolean;
  explicitToken: boolean;
  supportsOAuth: boolean;
}): boolean {
  if (!input.hasToken) return false;
  if (input.supportsOAuth === false) return true;
  return input.explicitToken;
}

/** True when `configPath` resolves inside `projectPath` (a project-scoped, likely VCS-tracked file). */
function isInsideProject(projectPath: string, configPath: string): boolean {
  const rel = path.relative(projectPath, configPath);
  return rel.length > 0 && !rel.startsWith('..') && !path.isAbsolute(rel);
}

/**
 * Write MCP configuration for the given AI agent so it can talk to a Godot-MCP
 * server. Library-safe: no stdout noise, no process.exit, no throws past the
 * public boundary.
 *
 * Writes an HTTP server entry pointing at the resolved MCP-client URL. The
 * Godot config body shape mirrors the addon's `AgentConfigJson` / `AgentConfigPaths`
 * conventions (`.mcp.json` under `mcpServers` for Claude Code, `.vscode/mcp.json`
 * under `servers` for VS Code, etc.).
 */
export async function setupMcp(opts: SetupMcpOptions): Promise<SetupMcpResult> {
  const warnings: string[] = [];

  try {
    if (!opts || typeof opts.agentId !== 'string' || opts.agentId.length === 0) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(`agentId is required. Available agent IDs: ${getAgentIds().join(', ')}`),
      };
    }

    const agent = getAgentById(opts.agentId);
    if (!agent) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(
          `Unknown agent: "${opts.agentId}". Available agent IDs: ${getAgentIds().join(', ')}`,
        ),
      };
    }

    // Resolve project path. If supplied it must exist; otherwise fall back to cwd.
    let projectPath: string;
    if (opts.godotProjectPath) {
      const validated = requireExistingPath(opts.godotProjectPath);
      if (!validated.ok) {
        return { kind: 'failure', success: false, warnings, error: validated.error };
      }
      projectPath = validated.projectPath;
    } else {
      projectPath = path.resolve(process.cwd());
    }

    emitProgress(opts.onProgress, {
      phase: 'start',
      message: `Configuring ${agent.name} for ${projectPath}`,
    });

    // Pin the URL to THIS project's routing segment by DEFAULT (design 02 §T4 / defect B4): the
    // written config points at `<base>/mcp/p/<pin-v2>`, so an agent session launched in this project
    // folder routes strictly to this project's engine instance even when the account has several
    // editors connected. `--no-pin` (opts.noPin) is the escape hatch → the bare `<base>/mcp` URL.
    // The pin is the shared cli-core **v2** pin (`\`→`/` normalization), byte-identical to what the
    // Godot editor's Configure writes — so a CLI-written and an editor-written config route the same
    // (parity with the C# AgentConfigurator; the pin is a ROUTING segment, never the OAuth resource
    // — decision M8).
    const baseClientUrl = resolveMcpClientUrl(opts.url);
    const pinned = opts.noPin !== true;
    const serverUrl = pinned ? pinUrl(baseClientUrl, derivePinV2(projectPath)) : baseClientUrl;
    // A token supplied EXPLICITLY by the caller (`--token` / the library `token`
    // arg) is a deliberate Flow C PAT opt-in; a token that is only present in the
    // ambient `GODOT_MCP_TOKEN` env (e.g. one a prior `login` set) is NOT.
    const explicitToken = typeof opts.token === 'string' && opts.token.length > 0;
    const token = opts.token ?? normalizeEnv(process.env[ENV_TOKEN]) ?? '';
    // OAuth-aware header gate (design D11 / auth Flow A & C): OAuth-capable clients
    // get a credential-free, URL-only config so their native RFC 9728 OAuth runs;
    // a static Authorization header is written only for a non-OAuth client or an
    // explicit PAT opt-in. See `shouldWriteAuthHeader`.
    const authRequired = shouldWriteAuthHeader({
      hasToken: token.length > 0,
      explicitToken,
      supportsOAuth: agent.supportsOAuth,
    });

    const configPath = agent.getConfigPath(projectPath);
    const props = agent.getHttpProps(serverUrl, token, authRequired);

    // Flow C safety: a static PAT written into a project-scoped config file is a
    // VCS leak risk — warn so the user prefers an env var / user-scoped config (or
    // relies on native OAuth by omitting the token). Gate on whether a static
    // header was ACTUALLY emitted into `props`, not merely on `authRequired`:
    // agents whose `getHttpProps` ignore the token (e.g. Codex / Antigravity)
    // write no `headers`, so an explicit `--token` there must NOT raise a "leaked
    // credential" warning about a header that was never written to the file.
    const wroteAuthHeader = Boolean((props as { headers?: unknown }).headers);
    if (wroteAuthHeader && isInsideProject(projectPath, configPath)) {
      warnings.push(
        `Wrote a static Authorization (PAT) header into the project-scoped config ${configPath}. ` +
          `Committing this file would leak the credential — prefer setting ${ENV_TOKEN} in your ` +
          `environment or a user-scoped config, or omit the token to use the client's native OAuth.`,
      );
    }

    if (agent.configFormat === 'toml') {
      writeTomlAgentConfig(configPath, agent.bodyPath, MCP_SERVER_NAME, props, agent.httpRemoveKeys);
    } else {
      writeJsonAgentConfig(configPath, agent.bodyPath, MCP_SERVER_NAME, props, agent.httpRemoveKeys);
    }

    emitProgress(opts.onProgress, {
      phase: 'manifest-patched',
      message: `Wrote ${configPath}`,
      manifestPath: configPath,
    });

    emitProgress(opts.onProgress, { phase: 'done', message: `${agent.name} configured successfully.` });

    return {
      kind: 'success',
      success: true,
      agentId: agent.id,
      configPath,
      serverUrl,
      pinned,
      warnings,
    };
  } catch (err: unknown) {
    return {
      kind: 'failure',
      success: false,
      warnings,
      error: err instanceof Error ? err : new Error(String(err)),
    };
  }
}

/** List every agent id known to `setupMcp`. */
export function listAgentIds(): string[] {
  return getAgentIds();
}

// Re-export the constants so the CLI command can surface the default URL in help.
export { CLOUD_MCP_URL, DEFAULT_CUSTOM_HOST };
