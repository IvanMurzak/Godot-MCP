import * as path from 'path';
import {
  createProjectKeyResolver,
  godotAdapter,
  ProjectKeyStore,
  isCloudUrl,
  pinUrl,
  toAuthServerRoot,
  type ProjectKeyResult,
} from '@baizor/gamedev-cli-core';
import {
  CLOUD_MCP_URL,
  ENV_HOST,
  ENV_TOKEN,
  MCP_HUB_PATH,
} from '../utils/connection.js';
import {
  getAgentById,
  getAgentConfigPaths,
  getAgentIds,
  httpHeadersKeyOf,
  rewriteProjectKeyInAgentConfigs,
  writeJsonAgentConfig,
  writeTomlAgentConfig,
  MCP_SERVER_NAME,
} from '../utils/agents.js';
import { derivePinV2 } from '../utils/project-identity.js';
import { emitProgress } from './progress.js';
import { requireExistingPath } from './validation.js';
import type { SetupMcpCredential, SetupMcpOptions, SetupMcpResult } from './types.js';

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

/**
 * Write MCP configuration for the given AI agent so it can talk to a Godot-MCP
 * server. Library-safe: no stdout noise, no process.exit, no throws past the
 * public boundary.
 *
 * **Credential policy (project-keys contract §7, owner rulings 2026-09-23)** — mirrors cli-core's
 * `setupMcp`:
 *   - an explicit `token` always wins (the Flow C PAT opt-in, see `shouldWriteAuthHeader`);
 *   - otherwise a **Cloud** config (a non-loopback hub URL) carries `Authorization: Bearer agd_pk_…` —
 *     a non-expiring project key strictly bound to this project's pin, reused from
 *     `~/.ai-game-dev/project-keys.json` or minted with the machine credential — for EVERY agent,
 *     through each agent's own static-header key (`headers`, Codex `http_headers`);
 *   - `oauth` opts out (URL-only, any previous header removed);
 *   - `regenerateKey` mints a fresh key, rewrites the config, then revokes the previous key;
 *   - no machine login / a failed mint ⇒ URL-only with a warning (the write itself never fails);
 *   - a local-server (loopback) config is unchanged.
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
    const pin = derivePinV2(projectPath);
    const serverUrl = pinned ? pinUrl(baseClientUrl, pin) : baseClientUrl;
    // A token supplied EXPLICITLY by the caller (`--token` / the library `token`
    // arg) is a deliberate Flow C PAT opt-in; a token that is only present in the
    // ambient `GODOT_MCP_TOKEN` env (e.g. one a prior `login` set) is NOT.
    const explicitToken = typeof opts.token === 'string' && opts.token.length > 0;
    const cloud = isCloudUrl(baseClientUrl);
    if (opts.regenerateKey && (opts.oauth || explicitToken || !cloud)) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(
          '--regenerate-key applies only to a Cloud config without --oauth / --token ' +
            '(project keys are not used for a local server).',
        ),
      };
    }

    // Cloud default (contract §7): resolve the project key unless the caller opted out.
    let key: Extract<ProjectKeyResult, { kind: 'ok' }> | undefined;
    // Regenerate: the key being replaced, read BEFORE the resolver overwrites the cache entry, so the
    // project's other agent configs still holding it can be carried over to the new key (see below).
    let previousKey: string | undefined;
    if (cloud && !explicitToken && !opts.oauth) {
      const issuer = toAuthServerRoot(baseClientUrl);
      if (opts.regenerateKey) {
        const lookup = opts.previousProjectKey ?? ((i: string, p: string) => new ProjectKeyStore().get(i, p)?.key);
        previousKey = lookup(issuer, pin);
      }
      const resolver = opts.projectKeyResolver ?? createProjectKeyResolver(godotAdapter);
      const outcome = await resolver({
        issuer,
        pin,
        engine: 'godot',
        label: projectPath,
        machineName: opts.machineName,
        regenerate: opts.regenerateKey === true,
      });
      if (outcome.kind === 'ok') {
        key = outcome;
        warnings.push(...outcome.warnings);
      } else if (opts.regenerateKey) {
        return {
          kind: 'failure',
          success: false,
          warnings,
          error: new Error(`Could not regenerate the project key: ${outcome.reason}`),
        };
      } else {
        warnings.push(
          `No project key written (${outcome.reason}) — the config is URL-only and the agent must sign in with its own OAuth. ` +
            'Sign in on this machine (`godot-cli login`) and run setup-mcp again to write a project key.',
        );
      }
    }

    // OAuth-aware header gate (design D11 / auth Flow A & C): without a project key, OAuth-capable
    // clients get a credential-free, URL-only config so their native RFC 9728 OAuth runs; a static
    // Authorization header is written only for a non-OAuth client or an explicit PAT opt-in. A
    // project key is written for every agent.
    const token = key?.key ?? opts.token ?? normalizeEnv(process.env[ENV_TOKEN]) ?? '';
    const authRequired =
      key !== undefined ||
      shouldWriteAuthHeader({
        hasToken: token.length > 0,
        explicitToken,
        supportsOAuth: agent.supportsOAuth,
      });

    const configPaths = getAgentConfigPaths(agent, projectPath);
    const props = agent.getHttpProps(serverUrl, token, authRequired);
    // Gate on whether a static header was ACTUALLY emitted into `props` (an agent whose format lacks
    // one writes none).
    const headersKey = httpHeadersKeyOf(agent);
    const wroteAuthHeader = Boolean((props as Record<string, unknown>)[headersKey]);
    const credential: SetupMcpCredential = !wroteAuthHeader ? 'none' : key ? 'project-key' : 'token';
    // A config written without a header must not keep a stale one: in Cloud (--oauth, no login, failed
    // mint) it would suppress the client's native OAuth, and re-pointing an entry at a local server must
    // not carry the Cloud project key along.
    const removeKeys = wroteAuthHeader ? agent.httpRemoveKeys : [...agent.httpRemoveKeys, headersKey];

    // Write EVERY config file of the agent (Antigravity has two). One failing must not pass silently,
    // and must not stop the others: each is attempted, then the failures are reported by path.
    const write = agent.configFormat === 'toml' ? writeTomlAgentConfig : writeJsonAgentConfig;
    const written: string[] = [];
    const failedWrites: string[] = [];
    for (const configPath of configPaths) {
      try {
        write(configPath, agent.bodyPath, MCP_SERVER_NAME, props, removeKeys);
        written.push(configPath);
        emitProgress(opts.onProgress, {
          phase: 'manifest-patched',
          message: `Wrote ${configPath}`,
          manifestPath: configPath,
        });
      } catch (err) {
        failedWrites.push(`${configPath} (${err instanceof Error ? err.message : String(err)})`);
      }
    }
    if (failedWrites.length > 0) {
      // The previous key (on a regenerate) is NOT revoked: a config still holds it.
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(
          `Could not write ${agent.name} config: ${failedWrites.join('; ')}.` +
            (written.length > 0 ? ` Written: ${written.join(', ')}.` : '') +
            // The new key is already cached, so a later --regenerate-key replaces IT, never this one.
            (key?.revokePrevious
              ? ' The previous project key was NOT revoked; once the config is fixed, re-run setup-mcp ' +
                'without --regenerate-key and revoke the previous key from your account page.'
              : ''),
        ),
      };
    }

    // Regenerate: the other agent configs that still carry the previous key (pinned or `--no-pin` URL —
    // the key is this project's, whatever URL it sits next to) would break the moment it is revoked (only
    // a regenerate that replaced this account's cached key revokes — `revokePrevious`) — carry the new key into them first. Revoke
    // only when every one of them was rewritten; otherwise keep the old key alive and say which failed.
    const rewrite =
      key?.revokePrevious && previousKey && previousKey !== key.key
        ? rewriteProjectKeyInAgentConfigs({
            projectPath,
            oldKey: previousKey,
            newKey: key.key,
            skipPaths: written,
          })
        : undefined;
    const revokeBlocked = (rewrite?.failed.length ?? 0) > 0;
    if (rewrite && revokeBlocked) {
      // Not "run --regenerate-key again": the new key is already cached, so a second regenerate would
      // treat IT as the previous key and never revoke (or rewrite configs still holding) this one.
      warnings.push(
        'These agent configs may still carry the previous project key and could not be rewritten, so the previous key ' +
          'was NOT revoked (fix them, re-run setup-mcp for their agent to write the new key, then revoke the ' +
          `previous key from your account page): ${rewrite.failed.map((f) => `${f.path} (${f.reason})`).join('; ')}.`,
      );
    }

    // Regenerate (§7): only once the new key is cached AND the configs rewritten, revoke the old one.
    // A revoke failure is reported, never fatal.
    if (key?.revokePrevious && !revokeBlocked) {
      try {
        const revokeWarning = await key.revokePrevious();
        if (revokeWarning) warnings.push(revokeWarning);
      } catch (err) {
        warnings.push(
          `Revoking the previous project key failed (${err instanceof Error ? err.message : String(err)}).`,
        );
      }
    }

    emitProgress(opts.onProgress, { phase: 'done', message: `${agent.name} configured successfully.` });

    return {
      kind: 'success',
      success: true,
      agentId: agent.id,
      configPath: written[0],
      configPaths: written,
      rewrittenConfigPaths: rewrite?.rewritten ?? [],
      serverUrl,
      pinned,
      credential,
      projectKeyId: key?.keyId,
      projectKeySource: key?.source,
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

// Re-export the constant so the CLI command can surface the default URL in help.
export { CLOUD_MCP_URL };
