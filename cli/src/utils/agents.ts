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
  /**
   * Whether this MCP client performs native RFC 9728 OAuth against the hosted
   * endpoint (auth Flow A). Default policy is `true`, mirroring the shared b6
   * configurators / decision D11: OAuth-capable clients get a credential-free,
   * URL-only `{type,url}` config so their own OAuth handshake runs, and
   * `setup-mcp` OMITS the static `Authorization` header for them — a static
   * header both fails against the hosted AS (401) and suppresses the client's
   * OAuth fallback ("OAuth fallback is disabled when headers.Authorization is
   * set"). Set `false` only for a client that cannot OAuth and needs a static
   * token against a required-auth (typically self-hosted) endpoint (Flow C).
   */
  supportsOAuth: boolean;
  /** Resolve the absolute (primary) config-file path for a given project root. */
  getConfigPath(projectPath: string): string;
  /**
   * Every config file the entry lives in, when the agent has more than one (Antigravity reads either
   * `~/.gemini/config/mcp_config.json` or `~/.gemini/antigravity/mcp_config.json`, unpredictably per
   * install, so both are written). Absent ⇒ just {@link getConfigPath}. Use {@link getAgentConfigPaths}.
   */
  getConfigPaths?(projectPath: string): string[];
  /** Build the HTTP server entry written under `bodyPath[MCP_SERVER_NAME]`. */
  getHttpProps(url: string, token: string, authRequired: boolean): Record<string, unknown>;
  /** Keys to delete from a pre-existing entry before merging new props. */
  httpRemoveKeys: string[];
  /**
   * The entry key holding static http headers, when it is not `headers` (Codex: `http_headers`). Used to
   * detect a written `Authorization` header and to clear a stale one from a URL-only Cloud config.
   */
  httpHeadersKey?: string;
}

/** Every config file `agent`'s entry is written to / read from / removed from, primary first. */
export function getAgentConfigPaths(agent: AgentDefinition, projectPath: string): string[] {
  return agent.getConfigPaths?.(projectPath) ?? [agent.getConfigPath(projectPath)];
}

/** The entry key an agent's static http headers live under (`headers` unless the agent overrides it). */
export function httpHeadersKeyOf(agent: AgentDefinition): string {
  return agent.httpHeadersKey ?? 'headers';
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

/**
 * Antigravity's global MCP config lives in ONE of two places and which one is not predictable (it
 * differs per machine/install, not per OS), so the entry is written to both. Primary first.
 */
function antigravityConfigPaths(): string[] {
  return [
    path.join(home(), '.gemini', 'config', 'mcp_config.json'),
    path.join(home(), '.gemini', 'antigravity', 'mcp_config.json'),
  ];
}

/**
 * Build the optional `headers` object for an HTTP server entry. Emits a Bearer
 * `Authorization` header ONLY when `authRequired` is set AND a non-empty token is
 * present. `authRequired` is the OAuth-aware decision made per agent in
 * `lib/setup-mcp.ts` (see `shouldWriteAuthHeader`): OAuth-capable clients
 * (`supportsOAuth: true`, the default) are handed `authRequired = false`, so this
 * stays `undefined` and the client runs its own RFC 9728 OAuth (Flow A). A header
 * is produced only for the Flow C fallback (a non-OAuth client, or an explicit
 * PAT opt-in). Returns `undefined` otherwise.
 */
function authHeaders(token: string, authRequired: boolean): Record<string, string> | undefined {
  if (authRequired && token) {
    return { Authorization: `Bearer ${token}` };
  }
  return undefined;
}

/** `{ [key]: <Bearer header> }` when {@link authHeaders} emits one, else `{}` — for spreading into props. */
function headersProp(key: string, token: string, authRequired: boolean): Record<string, unknown> {
  const headers = authHeaders(token, authRequired);
  return headers ? { [key]: headers } : {};
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
    supportsOAuth: true,
    skillsPath: '.claude/skills',
    configPathDisplay: '.mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Claude Desktop ───────────────────────────────────────────
  {
    id: 'claude-desktop',
    name: 'Claude Desktop',
    supportsOAuth: true,
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
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Cursor ───────────────────────────────────────────────────
  {
    id: 'cursor',
    name: 'Cursor',
    supportsOAuth: true,
    skillsPath: '.cursor/skills',
    configPathDisplay: '.cursor/mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.cursor', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── VS Code (Copilot) ────────────────────────────────────────
  {
    id: 'vscode-copilot',
    name: 'Visual Studio Code (Copilot)',
    supportsOAuth: true,
    skillsPath: '.github/skills',
    configPathDisplay: '.vscode/mcp.json',
    configFormat: 'json',
    bodyPath: 'servers',
    getConfigPath: (p) => path.join(p, '.vscode', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Visual Studio (Copilot) ──────────────────────────────────
  {
    id: 'vs-copilot',
    name: 'Visual Studio (Copilot)',
    supportsOAuth: true,
    skillsPath: '.github/skills',
    configPathDisplay: '.vs/mcp.json',
    configFormat: 'json',
    bodyPath: 'servers',
    getConfigPath: (p) => path.join(p, '.vs', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Rider (Junie) ───────────────────────────────────────────
  {
    id: 'rider-junie',
    name: 'Rider (Junie)',
    supportsOAuth: true,
    skillsPath: '.junie/skills',
    configPathDisplay: '.junie/mcp/mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.junie', 'mcp', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      enabled: true,
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['disabled', 'command', 'args'],
  },

  // ── GitHub Copilot CLI ──────────────────────────────────────
  {
    id: 'github-copilot-cli',
    name: 'GitHub Copilot CLI',
    supportsOAuth: true,
    skillsPath: '.github/skills',
    configPathDisplay: '~/.copilot/mcp-config.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: () => path.join(home(), '.copilot', 'mcp-config.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      tools: ['*'],
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Gemini ──────────────────────────────────────────────────
  {
    id: 'gemini',
    name: 'Gemini',
    supportsOAuth: true,
    skillsPath: '.gemini/skills',
    configPathDisplay: '.gemini/settings.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.gemini', 'settings.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Antigravity ─────────────────────────────────────────────
  {
    id: 'antigravity',
    name: 'Antigravity',
    supportsOAuth: true,
    skillsPath: '.agent/skills',
    configPathDisplay: '~/.gemini/config/mcp_config.json + ~/.gemini/antigravity/mcp_config.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: () => antigravityConfigPaths()[0],
    getConfigPaths: () => antigravityConfigPaths(),
    // Antigravity uses a `serverUrl` key (not `url`), a `disabled` flag, and static `headers`.
    getHttpProps: (url, token, authRequired) => ({
      disabled: false,
      serverUrl: url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args', 'url', 'type'],
  },

  // ── Cline ───────────────────────────────────────────────────
  {
    id: 'cline',
    name: 'Cline',
    supportsOAuth: true,
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
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Open Code ───────────────────────────────────────────────
  {
    id: 'open-code',
    name: 'Open Code',
    supportsOAuth: true,
    skillsPath: '.opencode/skills',
    configPathDisplay: 'opencode.json',
    configFormat: 'json',
    bodyPath: 'mcp',
    getConfigPath: (p) => path.join(p, 'opencode.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'remote',
      enabled: true,
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Codex (TOML config) ─────────────────────────────────────
  {
    id: 'codex',
    name: 'Codex',
    supportsOAuth: true,
    skillsPath: '.agents/skills',
    configPathDisplay: '.codex/config.toml',
    configFormat: 'toml',
    bodyPath: 'mcp_servers',
    getConfigPath: (p) => path.join(p, '.codex', 'config.toml'),
    // Codex takes static http headers from the `http_headers` inline table (project-keys contract §7);
    // the section is rewritten wholesale, so a legacy `bearer_token_env_var` never coexists with it.
    getHttpProps: (url, token, authRequired) => ({
      enabled: true,
      url,
      tool_timeout_sec: 300,
      startup_timeout_sec: 30,
      ...headersProp('http_headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args', 'type'],
    httpHeadersKey: 'http_headers',
  },

  // ── Kilo Code ───────────────────────────────────────────────
  {
    id: 'kilo-code',
    name: 'Kilo Code',
    supportsOAuth: true,
    skillsPath: '.kilocode/skills',
    configPathDisplay: '.kilocode/mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, '.kilocode', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'streamable-http',
      disabled: false,
      url,
      ...headersProp('headers', token, authRequired),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Custom (generic mcpServers entry written to a caller path) ─
  {
    id: 'custom',
    name: 'Custom (generic MCP client)',
    supportsOAuth: true,
    skillsPath: null,
    configPathDisplay: 'mcp.json',
    configFormat: 'json',
    bodyPath: 'mcpServers',
    getConfigPath: (p) => path.join(p, 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...headersProp('headers', token, authRequired),
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
 * here is flat (string/number/bool/array scalars plus the `http_headers` inline table), so a full TOML library
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
  // The entry is owned wholesale, so drop its sub-tables too (e.g. a hand-written
  // `[mcp_servers.<name>.http_headers]`): leaving one beside the inline
  // `http_headers = {…}` is a duplicate key (invalid TOML), and on a URL-only
  // write it would keep a stale Authorization header.
  lines = removeTomlSubTables(lines, `[${bodyPath}.${serverName}.`);

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

/** Remove every `[<prefix>…]` table (header line through the line before the next header). */
function removeTomlSubTables(lines: string[], headerPrefix: string): string[] {
  const out: string[] = [];
  let skipping = false;
  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('[')) skipping = trimmed.startsWith(headerPrefix);
    if (!skipping) out.push(line);
  }
  return out;
}

/** A TOML key: bare when it is a valid bare key, else a quoted string. */
function tomlKey(k: string): string {
  return /^[A-Za-z0-9_-]+$/.test(k) ? k : tomlValue(k);
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
  if (v && typeof v === 'object') {
    // An inline table of string values (Codex `http_headers`).
    const pairs = Object.entries(v as Record<string, unknown>).map(([k, val]) => `${tomlKey(k)} = ${tomlValue(val)}`);
    return `{ ${pairs.join(', ')} }`;
  }
  // null/undefined have no valid TOML scalar form here; emit a quoted
  // string so we never produce an invalid bare token (the Codex schema only
  // feeds string/number/bool/array scalars and string tables, so this is a defensive fallback).
  return `"${String(v).replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
}

// ---------------------------------------------------------------------------
// Regenerate: carry a new project key into the project's other agent configs
// ---------------------------------------------------------------------------

/** The outcome of {@link rewriteProjectKeyInAgentConfigs}. */
export interface ProjectKeyRewriteReport {
  /** Configs whose `Authorization: Bearer <old key>` now carries the new key. */
  rewritten: string[];
  /** Configs that carry (or may carry) the old key but could not be rewritten, with the reason. */
  failed: { path: string; reason: string }[];
}

/**
 * `setup-mcp <agent> --regenerate-key` rewrites ONE agent's config and then revokes the previous key —
 * which would break every OTHER agent config of this project still holding it. This carries the new key
 * into each of them first: every EXISTING config of every registered agent (all of an agent's
 * {@link getAgentConfigPaths}) whose `ai-game-developer` entry carries a static `Authorization` header of
 * exactly `Bearer <oldKey>` gets that header value replaced — whatever URL the entry points at (pinned, or
 * unpinned via `--no-pin`): the key is this project's secret, so any entry holding it breaks on revoke.
 * The other entries and fields are kept (a JSON file is re-serialized with 2-space indentation).
 * `skipPaths` (the configs the regenerate just wrote) are left alone. A config that holds the old key but
 * cannot be read, parsed or written — or still holds it outside that header (another entry, a renamed
 * entry, a hand-written TOML sub-table) — is reported in `failed`, so the caller can keep the old key
 * alive instead of revoking it. Never throws.
 */
export function rewriteProjectKeyInAgentConfigs(opts: {
  projectPath: string;
  oldKey: string;
  newKey: string;
  skipPaths: readonly string[];
}): ProjectKeyRewriteReport {
  const report: ProjectKeyRewriteReport = { rewritten: [], failed: [] };
  const seen = new Set(opts.skipPaths.map((p) => path.resolve(p)));
  for (const agent of agentRegistry) {
    let paths: string[];
    try {
      paths = getAgentConfigPaths(agent, opts.projectPath);
    } catch {
      continue;
    }
    for (const configPath of paths) {
      const resolved = path.resolve(configPath);
      if (seen.has(resolved)) continue;
      seen.add(resolved);
      try {
        let text: string;
        try {
          text = fs.readFileSync(resolved, 'utf-8');
        } catch (err) {
          if ((err as NodeJS.ErrnoException).code === 'ENOENT') continue;
          throw err;
        }
        if (!text.includes(opts.oldKey)) continue;
        const next =
          agent.configFormat === 'toml'
            ? rewriteKeyInToml(text, agent.bodyPath, opts)
            : rewriteKeyInJson(text, agent.bodyPath, opts);
        if (next !== null) {
          fs.writeFileSync(resolved, next);
          report.rewritten.push(resolved);
        }
        // The old key is still in the file outside the rewritten header — revoking it would break that use.
        if ((next ?? text).includes(opts.oldKey)) {
          report.failed.push({ path: resolved, reason: `holds the previous key outside its ${MCP_SERVER_NAME} Authorization header` });
        }
      } catch (err) {
        report.failed.push({ path: resolved, reason: err instanceof Error ? err.message : String(err) });
      }
    }
  }
  return report;
}

/** The rewritten JSON text, or null when the entry does not carry the old key in its header. Throws on bad JSON. */
function rewriteKeyInJson(
  text: string,
  bodyPath: string,
  opts: { oldKey: string; newKey: string },
): string | null {
  // A UTF-8 BOM (Visual Studio writes one into `.vs/mcp.json`) is not JSON — strip it before parsing.
  const root = JSON.parse(text.replace(/^\uFEFF/, '')) as Record<string, unknown>;
  const entry = asRecord(asRecord(root)?.[bodyPath])?.[MCP_SERVER_NAME];
  const record = asRecord(entry);
  if (!record) return null;
  // JSON agents all keep static headers under `headers` (only the TOML Codex entry uses `http_headers`).
  const headers = asRecord(record['headers']);
  if (!headers || headers['Authorization'] !== `Bearer ${opts.oldKey}`) return null;
  headers['Authorization'] = `Bearer ${opts.newKey}`;
  return JSON.stringify(root, null, 2) + '\n';
}

/** The rewritten Codex TOML text, or null when the section does not carry the old key in its header. */
function rewriteKeyInToml(
  text: string,
  bodyPath: string,
  opts: { oldKey: string; newKey: string },
): string | null {
  const lines = text.split('\n');
  const start = lines.findIndex((l) => l.trim() === `[${bodyPath}.${MCP_SERVER_NAME}]`);
  if (start < 0) return null;
  let end = start + 1;
  while (end < lines.length && !lines[end].trim().startsWith('[')) end++;
  const section = lines.slice(start + 1, end);
  const oldValue = `"Bearer ${opts.oldKey}"`;
  const headerIdx = section.findIndex((l) => /^\s*http_headers\s*=/.test(l) && l.includes(oldValue));
  if (headerIdx < 0) return null;
  // A replacer FUNCTION, not a string: a replacement string would expand `$&`/`$'`/`$$` inside the key.
  lines[start + 1 + headerIdx] = section[headerIdx].replace(oldValue, () => `"Bearer ${opts.newKey}"`);
  return lines.join('\n');
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : undefined;
}

export { MCP_SERVER_NAME };
