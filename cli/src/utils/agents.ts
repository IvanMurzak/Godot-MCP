import chalk from 'chalk';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Agent Definition
// ---------------------------------------------------------------------------

/**
 * An AI-agent MCP-client configurator. Mirrors the Godot addon's
 * `GodotAgentConfigurator` registry (`addons/godot_mcp/Editor/UI/Agents/`), adapted
 * to the CLI's file-writing surface, and brought to parity with the Unity CLI's
 * agent roster (`Unity-MCP/cli/src/utils/agents.ts`). Each agent knows where its
 * MCP-client config lives and what HTTP-transport server entry to write.
 *
 * Godot is a server-less client: the plugin connects out to a server, and an
 * external AI client connects to the same server's `<host>/mcp` streamable-HTTP
 * endpoint. So every configurator here writes an HTTP server entry pointing at
 * the resolved MCP-client URL (NOT the plugin's `<host>/hub/mcp-server` SignalR
 * endpoint). Unlike the Unity CLI, there is NO stdio transport / local
 * server-binary path here — the Godot CLI never spawns a local server process —
 * so the Unity `getStdioProps` / `stdioRemoveKeys` surface is intentionally
 * omitted.
 *
 * `skillsPath` mirrors the Unity definition's per-agent skills directory. The
 * Godot CLI does not yet ship a `setup-skills` command (skills are generated
 * addon-side on plugin boot — see `Godot-MCP/CLAUDE.md` § CLI), but the field is
 * populated now so a follow-up `setup-skills` command can consume it without
 * another registry migration. `null` means the agent has no project-local skills
 * directory.
 */
export interface AgentDefinition {
  id: string;
  name: string;
  /** Per-agent project-relative skills directory, or `null` if the agent has none. */
  skillsPath: string | null;
  configPathDisplay: string;
  /** Config-file serialization format. `toml` is the Codex branch. */
  configFormat: 'json' | 'toml';
  bodyPath: string;
  /** Resolve the absolute config-file path for a given project root. */
  getConfigPath(projectPath: string): string;
  /** Build the HTTP server entry written under `bodyPath[MCP_SERVER_NAME]`. */
  getHttpProps(url: string, token: string, authRequired: boolean): Record<string, unknown>;
  /** Keys to delete from a pre-existing entry before merging new props. */
  httpRemoveKeys: string[];
}

// ---------------------------------------------------------------------------
// Platform helpers
// ---------------------------------------------------------------------------

function appData(): string {
  return process.env['APPDATA'] ?? path.join(os.homedir(), 'AppData', 'Roaming');
}

function home(): string {
  return os.homedir();
}

function isWindows(): boolean {
  return process.platform === 'win32';
}

function isMac(): boolean {
  return process.platform === 'darwin';
}

function authHeaders(token: string, authRequired: boolean): Record<string, string> | undefined {
  if (authRequired && token) {
    return { Authorization: `Bearer ${token}` };
  }
  return undefined;
}

// ---------------------------------------------------------------------------
// Agent Registry
// ---------------------------------------------------------------------------

const MCP_SERVER_NAME = 'ai-game-developer';

/**
 * Note on the Unity-only `unity-ai` agent: the Unity CLI registry includes a
 * `unity-ai` entry that writes `UserSettings/mcp.json` for Unity's built-in AI
 * assistant. That agent is intentionally OMITTED here — it targets a Unity-only
 * surface (`UserSettings/` is a Unity project convention) with no Godot analog.
 */
export const agentRegistry: readonly AgentDefinition[] = [
  // ── Claude Code ──────────────────────────────────────────────
  {
    id: 'claude-code',
    name: 'Claude Code',
    skillsPath: '.claude/skills',
    configPathDisplay: '.mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Claude Desktop ───────────────────────────────────────────
  {
    id: 'claude-desktop',
    name: 'Claude Desktop',
    skillsPath: null,
    configPathDisplay: '~/Claude/claude_desktop_config.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: () => {
      if (isWindows()) {
        return path.join(appData(), 'Claude', 'claude_desktop_config.json');
      }
      if (isMac()) {
        return path.join(home(), 'Library', 'Application Support', 'Claude', 'claude_desktop_config.json');
      }
      return path.join(home(), '.config', 'Claude', 'claude_desktop_config.json');
    },
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Cursor ───────────────────────────────────────────────────
  {
    id: 'cursor',
    name: 'Cursor',
    skillsPath: '.cursor/skills',
    configPathDisplay: '.cursor/mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.cursor', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── VS Code (Copilot) ────────────────────────────────────────
  {
    id: 'vscode',
    name: 'Visual Studio Code (Copilot)',
    skillsPath: '.github/skills',
    configPathDisplay: '.vscode/mcp.json',
    configFormat: 'json',
    bodyPath: 'servers',
    getConfigPath: (p) => path.join(p, '.vscode', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Visual Studio (Copilot) ──────────────────────────────────
  {
    id: 'vs-copilot',
    name: 'Visual Studio (Copilot)',
    skillsPath: '.github/skills',
    configPathDisplay: '.vs/mcp.json',
    configFormat: 'json',
    bodyPath: 'servers',
    getConfigPath: (p) => path.join(p, '.vs', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Rider (Junie) ───────────────────────────────────────────
  {
    id: 'rider-junie',
    name: 'Rider (Junie)',
    skillsPath: '.junie/skills',
    configPathDisplay: '.junie/mcp/mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.junie', 'mcp', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      enabled: true,
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['disabled', 'command', 'args'],
  },

  // ── GitHub Copilot CLI ──────────────────────────────────────
  {
    id: 'github-copilot-cli',
    name: 'GitHub Copilot CLI',
    skillsPath: '.github/skills',
    configPathDisplay: '~/.copilot/mcp-config.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: () => path.join(home(), '.copilot', 'mcp-config.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      tools: ['*'],
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Gemini ──────────────────────────────────────────────────
  {
    id: 'gemini',
    name: 'Gemini',
    skillsPath: '.gemini/skills',
    configPathDisplay: '.gemini/settings.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.gemini', 'settings.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Antigravity ─────────────────────────────────────────────
  {
    id: 'antigravity',
    name: 'Antigravity',
    skillsPath: '.agent/skills',
    configPathDisplay: '~/.gemini/config/mcp_config.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: () => path.join(home(), '.gemini', 'config', 'mcp_config.json'),
    // Antigravity uses a `serverUrl` key (not `url`) and a `disabled` flag.
    getHttpProps: (url, _token, _authRequired) => ({
      disabled: false,
      serverUrl: url,
    }),
    httpRemoveKeys: ['command', 'args', 'url', 'type'],
  },

  // ── Cline ───────────────────────────────────────────────────
  {
    id: 'cline',
    name: 'Cline',
    skillsPath: '.cline/skills',
    configPathDisplay: '~/Code/globalStorage/.../cline_mcp_settings.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: () => {
      if (isWindows()) {
        return path.join(
          appData(),
          'Code',
          'User',
          'globalStorage',
          'saoudrizwan.claude-dev',
          'settings',
          'cline_mcp_settings.json',
        );
      }
      const base = isMac()
        ? path.join(home(), 'Library', 'Application Support', 'Code', 'User', 'globalStorage')
        : path.join(home(), '.config', 'Code', 'User', 'globalStorage');
      return path.join(base, 'saoudrizwan.claude-dev', 'settings', 'cline_mcp_settings.json');
    },
    getHttpProps: (url, token, authRequired) => ({
      type: 'streamableHttp',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Open Code ───────────────────────────────────────────────
  {
    id: 'open-code',
    name: 'Open Code',
    skillsPath: '.opencode/skills',
    configPathDisplay: 'opencode.json',
    configFormat: 'json',
    bodyPath: 'mcp',
    getConfigPath: (p) => path.join(p, 'opencode.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'remote',
      enabled: true,
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Codex (TOML config) ─────────────────────────────────────
  {
    id: 'codex',
    name: 'Codex',
    skillsPath: '.agents/skills',
    configPathDisplay: '.codex/config.toml',
    configFormat: 'toml',
    bodyPath: 'mcp_servers',
    getConfigPath: (p) => path.join(p, '.codex', 'config.toml'),
    getHttpProps: (url, _token, _authRequired) => ({
      enabled: true,
      url,
      tool_timeout_sec: 300,
      startup_timeout_sec: 30,
    }),
    httpRemoveKeys: ['command', 'args', 'type'],
  },

  // ── Kilo Code ───────────────────────────────────────────────
  {
    id: 'kilo-code',
    name: 'Kilo Code',
    skillsPath: '.kilocode/skills',
    configPathDisplay: '.kilocode/mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.kilocode', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'streamable-http',
      disabled: false,
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Custom (generic mcpServers entry written to a caller path) ─
  {
    id: 'custom',
    name: 'Custom (generic MCP client)',
    skillsPath: null,
    configPathDisplay: 'mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },
] as const;

// ---------------------------------------------------------------------------
// Lookup helpers
// ---------------------------------------------------------------------------

export function getAgentById(id: string): AgentDefinition | undefined {
  return agentRegistry.find((a) => a.id === id);
}

export function getAgentIds(): string[] {
  return agentRegistry.map((a) => a.id);
}

export function listAgentTable(
  heading: string,
  locationLabel: string,
  locationFn: (agent: AgentDefinition) => string,
): void {
  const sorted = [...agentRegistry].sort((a, b) => a.id.localeCompare(b.id));

  const colId = 'ID';
  const colLoc = locationLabel;

  const wId = Math.max(colId.length, ...sorted.map((a) => a.id.length));
  const wLoc = Math.max(colLoc.length, ...sorted.map((a) => locationFn(a).length));

  const sep = chalk.dim;
  const hBar = (w: number) => '─'.repeat(w);

  console.log(`\n${chalk.bold.cyan(heading)}\n`);

  // Header
  console.log(sep('  ┌─') + sep(hBar(wId)) + sep('─┬─') + sep(hBar(wLoc)) + sep('─┐'));
  console.log(
    sep('  │ ') + chalk.bold.white(colId.padEnd(wId)) + sep(' │ ') + chalk.bold.white(colLoc.padEnd(wLoc)) + sep(' │'),
  );
  console.log(sep('  ├─') + sep(hBar(wId)) + sep('─┼─') + sep(hBar(wLoc)) + sep('─┤'));

  // Rows
  for (const agent of sorted) {
    const loc = locationFn(agent);
    console.log(
      sep('  │ ') + chalk.yellow(agent.id.padEnd(wId)) + sep(' │ ') + chalk.green(loc.padEnd(wLoc)) + sep(' │'),
    );
  }

  // Footer
  console.log(sep('  └─') + sep(hBar(wId)) + sep('─┴─') + sep(hBar(wLoc)) + sep('─┘'));
  console.log('');
}

// ---------------------------------------------------------------------------
// Config file writing — JSON
// ---------------------------------------------------------------------------

export function writeJsonAgentConfig(
  configPath: string,
  bodyPath: string,
  serverName: string,
  props: Record<string, unknown>,
  removeKeys: string[],
): void {
  const dir = path.dirname(configPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  let root: Record<string, unknown> = {};
  if (fs.existsSync(configPath)) {
    try {
      root = JSON.parse(fs.readFileSync(configPath, 'utf-8')) as Record<string, unknown>;
    } catch {
      // If the file is malformed, start fresh
      root = {};
    }
    if (!root || typeof root !== 'object' || Array.isArray(root)) {
      root = {};
    }
  }

  // Navigate/create bodyPath
  let body = root[bodyPath] as Record<string, unknown> | undefined;
  if (!body || typeof body !== 'object' || Array.isArray(body)) {
    body = {};
    root[bodyPath] = body;
  }

  // Remove deprecated "Godot-MCP" entries
  delete body['Godot-MCP'];

  // Get or create the server entry
  let entry = body[serverName] as Record<string, unknown> | undefined;
  if (!entry || typeof entry !== 'object' || Array.isArray(entry)) {
    entry = {};
  }

  // Remove stale keys
  for (const key of removeKeys) {
    delete entry[key];
  }

  // Merge new properties
  for (const [key, value] of Object.entries(props)) {
    entry[key] = value;
  }

  body[serverName] = entry;
  root[bodyPath] = body;

  fs.writeFileSync(configPath, JSON.stringify(root, null, 2) + '\n');
}

// ---------------------------------------------------------------------------
// Config file writing — TOML (Codex only)
// ---------------------------------------------------------------------------

/**
 * Write/merge a single `[bodyPath.serverName]` TOML section into `configPath`,
 * mirroring the Unity CLI's Codex TOML branch. Existing sections under other
 * headers are preserved; the target section is replaced wholesale (so a re-run
 * is idempotent and stale keys are dropped). Keys in `removeKeys` are never
 * written. This is a deliberately minimal TOML emitter — Codex's config schema
 * here is flat (string/number/bool/array scalars only), so a full TOML library
 * dependency is unwarranted.
 */
export function writeTomlAgentConfig(
  configPath: string,
  bodyPath: string,
  serverName: string,
  props: Record<string, unknown>,
  removeKeys: string[],
): void {
  const dir = path.dirname(configPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  // Read existing content or start fresh
  let lines: string[] = [];
  if (fs.existsSync(configPath)) {
    lines = fs.readFileSync(configPath, 'utf-8').split('\n');
  }

  const sectionHeader = `[${bodyPath}.${serverName}]`;

  // Find existing section boundaries
  const sectionIdx = lines.findIndex((l) => l.trim() === sectionHeader);

  // Build TOML key-value pairs for the section
  const tomlLines = [sectionHeader];
  for (const [key, value] of Object.entries(props)) {
    if (removeKeys.includes(key)) continue;
    tomlLines.push(`${key} = ${tomlValue(value)}`);
  }

  if (sectionIdx >= 0) {
    // Find end of section (next [...] header or EOF). The leading-`[` test is
    // safe for the Codex schema, which emits only flat scalars (no inline-array
    // value lines that would also start with `[`); revisit if that changes.
    let endIdx = sectionIdx + 1;
    while (endIdx < lines.length && !lines[endIdx].trim().startsWith('[')) {
      endIdx++;
    }
    // Replace section
    lines.splice(sectionIdx, endIdx - sectionIdx, ...tomlLines);
  } else {
    // Append section
    if (lines.length > 0 && lines[lines.length - 1].trim() !== '') {
      lines.push('');
    }
    lines.push(...tomlLines);
  }

  fs.writeFileSync(configPath, lines.join('\n') + '\n');
}

function tomlValue(v: unknown): string {
  if (typeof v === 'string') return `"${v.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
  if (typeof v === 'boolean') return String(v);
  if (typeof v === 'number') {
    // TOML spells non-finite floats `nan` / `inf` / `-inf`, not JS's NaN/Infinity.
    if (Number.isNaN(v)) return 'nan';
    if (v === Infinity) return 'inf';
    if (v === -Infinity) return '-inf';
    return String(v);
  }
  if (Array.isArray(v)) {
    return `[${v.map(tomlValue).join(', ')}]`;
  }
  // null/undefined/object have no valid TOML scalar form here; emit a quoted
  // string so we never produce an invalid bare token (the Codex schema only
  // feeds string/number/bool/array scalars, so this is a defensive fallback).
  return `"${String(v).replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
}

export { MCP_SERVER_NAME };
