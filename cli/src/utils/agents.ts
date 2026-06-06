import chalk from 'chalk';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Agent Definition
// ---------------------------------------------------------------------------

/**
 * An AI-agent MCP-client configurator. Mirrors the Godot addon's
 * `GodotAgentConfigurator` registry (`addons/godot_mcp/UI/Agents/`), adapted
 * to the CLI's file-writing surface. Each agent knows where its MCP-client
 * config lives and what HTTP-transport server entry to write.
 *
 * Godot is a server-less client: the plugin connects out to a server, and an
 * external AI client connects to the same server's `<host>/mcp` streamable-HTTP
 * endpoint. So every configurator here writes an HTTP server entry pointing at
 * the resolved MCP-client URL (NOT the plugin's `<host>/hub/mcp-server` SignalR
 * endpoint).
 */
export interface AgentDefinition {
  id: string;
  name: string;
  configPathDisplay: string;
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

export const agentRegistry: readonly AgentDefinition[] = [
  // ── Claude Code ──────────────────────────────────────────────
  {
    id: 'claude-code',
    name: 'Claude Code',
    configPathDisplay: '.mcp.json',
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
    configPathDisplay: '~/Claude/claude_desktop_config.json',
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
    configPathDisplay: '.cursor/mcp.json',
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
    configPathDisplay: '.vscode/mcp.json',
    bodyPath: 'servers',
    getConfigPath: (p) => path.join(p, '.vscode', 'mcp.json'),
    getHttpProps: (url, token, authRequired) => ({
      type: 'http',
      url,
      ...(authHeaders(token, authRequired) ? { headers: authHeaders(token, authRequired) } : {}),
    }),
    httpRemoveKeys: ['command', 'args'],
  },

  // ── Custom (generic mcpServers entry written to a caller path) ─
  {
    id: 'custom',
    name: 'Custom (generic MCP client)',
    configPathDisplay: 'mcp.json',
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

export { MCP_SERVER_NAME };
