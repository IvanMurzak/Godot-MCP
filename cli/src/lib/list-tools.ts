// The editor's AI tools: MCP `tools/list` on the server, cached in `<project>/.ai-game-dev/tools.json` so the list
// (and each tool's input schema) is available while the editor is closed. Library-safe: no console output, no exit.

import * as fs from 'fs';
import * as path from 'path';
import { ensureAiGameDevGitignore, networkFailureReason } from '@baizor/gamedev-cli-core';
import { SKILL_FAMILIES } from '../utils/skills.js';

export interface ToolInfo {
  name: string;
  title?: string;
  description: string;
  inputSchema?: { type?: string; properties?: Record<string, { type?: string | string[]; description?: string }>; required?: string[] };
}

export type ListToolsResult =
  | { kind: 'success'; tools: ToolInfo[] }
  /**
   * `unreachable`: nothing answers (refused, DNS): an editor may be started, or the offline list shown; `dropped`: a
   * server took the request and dropped the connection (exit 7 UNREACHABLE, like run-tool).
   */
  | { kind: 'failure'; reason: 'unreachable' | 'dropped' | 'timeout' | 'protocol'; message: string };

/** The MCP endpoint of a server base URL (`http://localhost:N` → `/mcp`; a cloud hub URL already ends in `/mcp`). */
export function mcpEndpoint(baseUrl: string): string {
  const url = baseUrl.replace(/\/+$/, '');
  return /\/mcp(\/p\/[^/]+)?$/.test(url) ? url : `${url}/mcp`;
}

/** JSON-RPC responses in a Streamable-HTTP body: plain JSON, or SSE `data:` lines. */
export function parseMcpBody(text: string): Array<{ id?: unknown; result?: unknown; error?: { message?: string } }> {
  const trimmed = text.trim();
  if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
    const v = JSON.parse(trimmed) as unknown;
    return (Array.isArray(v) ? v : [v]) as Array<{ id?: unknown; result?: unknown; error?: { message?: string } }>;
  }
  const out: Array<{ id?: unknown; result?: unknown; error?: { message?: string } }> = [];
  for (const line of trimmed.split(/\r?\n/)) {
    if (!line.startsWith('data:')) continue;
    try {
      out.push(JSON.parse(line.slice(5).trim()));
    } catch {
      // keep-alive or partial line
    }
  }
  return out;
}

/** `initialize` → `notifications/initialized` → `tools/list` (all pages) → session DELETE. */
export async function listToolsLive(
  baseUrl: string,
  token: string | undefined,
  timeoutMs = 15_000,
  fetchImpl: typeof fetch = globalThis.fetch,
): Promise<ListToolsResult> {
  const endpoint = mcpEndpoint(baseUrl);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  let session: string | undefined;
  const headers = (): Record<string, string> => ({
    'Content-Type': 'application/json',
    Accept: 'application/json, text/event-stream',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(session ? { 'Mcp-Session-Id': session } : {}),
  });
  const rpc = async (id: number | undefined, method: string, params?: unknown) => {
    const res = await fetchImpl(endpoint, {
      method: 'POST',
      headers: headers(),
      body: JSON.stringify({ jsonrpc: '2.0', ...(id !== undefined ? { id } : {}), method, ...(params !== undefined ? { params } : {}) }),
      signal: controller.signal,
    });
    session = res.headers.get('mcp-session-id') ?? session;
    const text = await res.text();
    if (!res.ok) throw new Error(`HTTP ${res.status} from ${endpoint}${text ? `: ${text.slice(0, 200)}` : ''}`);
    if (id === undefined) return undefined;
    const reply = parseMcpBody(text).find((m) => m.id === id);
    if (!reply) throw new Error(`no reply to ${method} from ${endpoint}`);
    if (reply.error) throw new Error(`${method} failed: ${reply.error.message ?? 'error'}`);
    return reply.result as Record<string, unknown>;
  };
  try {
    await rpc(1, 'initialize', { protocolVersion: '2025-03-26', capabilities: {}, clientInfo: { name: 'godot-cli', version: '1' } });
    await rpc(undefined, 'notifications/initialized');
    const tools: ToolInfo[] = [];
    let cursor: string | undefined;
    for (let page = 0, id = 2; page < 50; page++, id++) {
      const result = await rpc(id, 'tools/list', cursor ? { cursor } : {});
      for (const t of (result?.tools as ToolInfo[] | undefined) ?? []) tools.push(t);
      cursor = typeof result?.nextCursor === 'string' ? result.nextCursor : undefined;
      if (!cursor) break;
    }
    return { kind: 'success', tools: tools.sort((a, b) => a.name.localeCompare(b.name)) };
  } catch (err) {
    const e = err as Error;
    if (e.name === 'AbortError') return { kind: 'failure', reason: 'timeout', message: `No answer from ${endpoint} within ${timeoutMs} ms.` };
    // The shared transport classification, as in run-tool: a dropped connection is not "nothing answers".
    const why = networkFailureReason(err);
    if (why === 'connection-reset') {
      return { kind: 'failure', reason: 'dropped', message: `${endpoint} dropped the connection before the tool list arrived.` };
    }
    if (why !== undefined) return { kind: 'failure', reason: 'unreachable', message: `Nothing answers at ${endpoint}.` };
    return { kind: 'failure', reason: 'protocol', message: e.message };
  } finally {
    clearTimeout(timer);
    if (session) void fetchImpl(endpoint, { method: 'DELETE', headers: headers() }).catch(() => undefined);
  }
}

export interface ToolsCache {
  fetchedAt: string;
  url: string;
  tools: ToolInfo[];
}

export function toolsCachePath(projectPath: string): string {
  return path.join(projectPath, '.ai-game-dev', 'tools.json');
}

export function readToolsCache(projectPath: string): ToolsCache | undefined {
  try {
    const cache = JSON.parse(fs.readFileSync(toolsCachePath(projectPath), 'utf-8')) as ToolsCache;
    return Array.isArray(cache.tools) ? cache : undefined;
  } catch {
    return undefined;
  }
}

/** Write the cache and keep it out of version control (`.ai-game-dev/.gitignore`). Best effort. */
export function writeToolsCache(projectPath: string, url: string, tools: ToolInfo[]): void {
  try {
    const dir = path.dirname(toolsCachePath(projectPath));
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(toolsCachePath(projectPath), JSON.stringify({ fetchedAt: new Date().toISOString(), url, tools }, null, 2));
    ensureAiGameDevGitignore(projectPath, ['tools.json']);
  } catch {
    // the cache is a convenience
  }
}

/** The tool list the CLI knows without an editor (the addon's tool families; no input schemas). */
export function offlineTools(): ToolInfo[] {
  return SKILL_FAMILIES.flatMap((f) => f.tools.map((t) => ({ name: t.name, description: t.description }))).sort((a, b) => a.name.localeCompare(b.name));
}

function placeholder(type: string | string[] | undefined, name: string): unknown {
  const t = Array.isArray(type) ? type.find((x) => x !== 'null') : type;
  if (t === 'integer' || t === 'number') return 0;
  if (t === 'boolean') return false;
  if (t === 'array') return [];
  if (t === 'object') return {};
  return `<${name}>`;
}

/** `godot-cli run-tool <name> --input '{…required args…}'` from the tool's input schema. */
export function exampleCall(tool: ToolInfo): string {
  const props = tool.inputSchema?.properties ?? {};
  const required = tool.inputSchema?.required ?? [];
  const input: Record<string, unknown> = {};
  for (const key of required) input[key] = placeholder(props[key]?.type, key);
  return `godot-cli run-tool ${tool.name} --input '${JSON.stringify(input)}'`;
}
