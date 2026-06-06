import * as path from 'path';
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
  MCP_SERVER_NAME,
} from '../utils/agents.js';
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

    const serverUrl = resolveMcpClientUrl(opts.url);
    const token = opts.token ?? normalizeEnv(process.env[ENV_TOKEN]) ?? '';
    const authRequired = token.length > 0;

    const configPath = agent.getConfigPath(projectPath);
    const props = agent.getHttpProps(serverUrl, token, authRequired);

    writeJsonAgentConfig(configPath, agent.bodyPath, MCP_SERVER_NAME, props, agent.httpRemoveKeys);

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
