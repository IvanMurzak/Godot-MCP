import * as fs from 'fs';
import * as path from 'path';
import { derivePin, PIN_LENGTH } from './project-identity.js';
import { agentRegistry, MCP_SERVER_NAME } from './agents.js';

/**
 * The tool-neutral, committable **project marker** `<project>/.ai-game-dev/project.json`
 * (design 06 / D15). It is NON-secret — credentials NEVER go here — and records:
 *
 * - `serverTarget` — the hub URL the enrolled plugin should connect to (hosted
 *   `https://ai-game.dev` or a local `http://localhost:<port>`), returned by the
 *   enrollment-redeem endpoint.
 * - `pin` — the D14 routing pin (first 8 hex of the ProjectIdentity SHA256), so
 *   the plugin, the CLIs, and `configure` all agree which project the session
 *   routes to.
 * - `port` — the deterministic local port (SHA256→20000–29999) recorded for a
 *   localhost target so a terminal-written config and the plugin never diverge.
 * - `portOverride` — an explicit user port override (D15) that always wins.
 *
 * ProjectIdentity resolution and every config writer consult it, so an override
 * or target can never silently diverge between the plugin and a terminal-written
 * config.
 */
export const PROJECT_MARKER_RELATIVE_PATH = path.join('.ai-game-dev', 'project.json');

export interface ProjectMarker {
  /** The enrolled server target URL (hosted or local). */
  serverTarget?: string;
  /** The D14 routing pin (first 8 hex of the ProjectIdentity hash). */
  pin?: string;
  /** The deterministic local port (recorded for localhost targets). */
  port?: number;
  /** An explicit user port override (always wins over the derived port). */
  portOverride?: number;
  /** Forward-compatible: unknown keys are preserved on rewrite. */
  [key: string]: unknown;
}

/** Absolute path of the marker file for a project. */
export function getProjectMarkerPath(projectPath: string): string {
  return path.join(projectPath, PROJECT_MARKER_RELATIVE_PATH);
}

/** Read the marker, or null when absent / empty / malformed. Never throws. */
export function readProjectMarker(projectPath: string): ProjectMarker | null {
  const markerPath = getProjectMarkerPath(projectPath);
  if (!fs.existsSync(markerPath)) return null;
  try {
    const raw = fs.readFileSync(markerPath, 'utf-8');
    if (raw.trim().length === 0) return null;
    const parsed = JSON.parse(raw) as ProjectMarker;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
    return parsed;
  } catch {
    return null;
  }
}

/**
 * Write the marker, MERGING over any existing document so a re-enroll never drops
 * a previously-set `portOverride` or forward-compatible key. Creates the
 * `.ai-game-dev/` directory if needed. The supplied `patch` fields overwrite the
 * existing values; every other existing field is preserved.
 */
export function writeProjectMarker(projectPath: string, patch: ProjectMarker): ProjectMarker {
  const markerPath = getProjectMarkerPath(projectPath);
  const dir = path.dirname(markerPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  const existing = readProjectMarker(projectPath) ?? {};
  const merged: ProjectMarker = { ...existing, ...patch };
  fs.writeFileSync(markerPath, JSON.stringify(merged, null, 2) + '\n');
  return merged;
}

/** True when `url`'s host is a loopback address (localhost / 127.0.0.1 / ::1). Never throws. */
export function isLocalhostUrl(url: string): boolean {
  try {
    const host = new URL(url).hostname.toLowerCase();
    return host === 'localhost' || host === '127.0.0.1' || host === '::1' || host === '[::1]';
  } catch {
    return false;
  }
}

/**
 * Append (or replace) the `/p/<pin>` routing segment on an MCP-server URL —
 * the D14 project pin the plugin routes on. A URL already ending in `/p/<hex>`
 * has its pin REPLACED (idempotent re-enroll); otherwise `/p/<pin>` is appended
 * to the existing path. Query/hash are preserved. Returns the input unchanged on
 * a malformed URL. Pure.
 */
export function applyPinToUrl(url: string, pin: string): string {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return url;
  }
  const segments = parsed.pathname.split('/').filter((s) => s.length > 0);
  const hexLen = PIN_LENGTH; // 8
  const isPinHex = (s: string): boolean => new RegExp(`^[0-9a-f]{${hexLen}}$`, 'i').test(s);
  if (segments.length >= 2 && segments[segments.length - 2] === 'p' && isPinHex(segments[segments.length - 1])) {
    segments[segments.length - 1] = pin;
  } else {
    segments.push('p', pin);
  }
  parsed.pathname = '/' + segments.join('/');
  return parsed.toString();
}

/** True when `configPath` resolves INSIDE `projectPath` (a project-local, not user-global, config). */
function isProjectLocalConfig(configPath: string, projectPath: string): boolean {
  const root = path.resolve(projectPath) + path.sep;
  return path.resolve(configPath).startsWith(root);
}

/**
 * Upsert the D14 pin into the URL of the `ai-game-developer` entry of every
 * EXISTING project-local agent config (design 06 — "upserts the D14 pin into any
 * existing project-local agent config entry"), so a hosted/local server the user
 * added manually before enrolling becomes pinned. Best-effort: user-global
 * configs (Claude Desktop, Cline, …) are skipped; a config with no
 * `ai-game-developer` entry or no URL is left untouched; a malformed/unreadable
 * file is skipped without throwing. Returns the absolute paths that were
 * rewritten. JSON configs rewrite the `url`/`serverUrl` value; the Codex TOML
 * config rewrites its `url = "…"` line.
 */
export function upsertPinIntoAgentConfigs(projectPath: string, pin: string): string[] {
  const updated: string[] = [];
  for (const agent of agentRegistry) {
    let configPath: string;
    try {
      configPath = agent.getConfigPath(projectPath);
    } catch {
      continue;
    }
    if (!isProjectLocalConfig(configPath, projectPath)) continue;
    if (!fs.existsSync(configPath)) continue;

    try {
      if (agent.configFormat === 'toml') {
        if (upsertPinInTomlConfig(configPath, pin)) updated.push(configPath);
      } else {
        if (upsertPinInJsonConfig(configPath, agent.bodyPath, pin)) updated.push(configPath);
      }
    } catch {
      // Best-effort: a single unreadable/malformed config never fails enrollment.
    }
  }
  return updated;
}

/** Rewrite the `url`/`serverUrl` of `bodyPath.ai-game-developer` in a JSON config. Returns true when changed. */
function upsertPinInJsonConfig(configPath: string, bodyPath: string, pin: string): boolean {
  const root = JSON.parse(fs.readFileSync(configPath, 'utf-8')) as Record<string, unknown>;
  if (!root || typeof root !== 'object' || Array.isArray(root)) return false;
  const body = root[bodyPath] as Record<string, unknown> | undefined;
  if (!body || typeof body !== 'object' || Array.isArray(body)) return false;
  const entry = body[MCP_SERVER_NAME] as Record<string, unknown> | undefined;
  if (!entry || typeof entry !== 'object' || Array.isArray(entry)) return false;

  // The registry uses `url` for most agents and `serverUrl` for Antigravity.
  const urlKey = typeof entry['url'] === 'string' ? 'url' : typeof entry['serverUrl'] === 'string' ? 'serverUrl' : null;
  if (!urlKey) return false;

  const current = entry[urlKey] as string;
  const pinned = applyPinToUrl(current, pin);
  if (pinned === current) return false;

  entry[urlKey] = pinned;
  fs.writeFileSync(configPath, JSON.stringify(root, null, 2) + '\n');
  return true;
}

/** Rewrite the `url = "…"` line under `[mcp_servers.ai-game-developer]` in a Codex TOML config. Returns true when changed. */
function upsertPinInTomlConfig(configPath: string, pin: string): boolean {
  const lines = fs.readFileSync(configPath, 'utf-8').split('\n');
  const header = `[mcp_servers.${MCP_SERVER_NAME}]`;
  const sectionIdx = lines.findIndex((l) => l.trim() === header);
  if (sectionIdx < 0) return false;

  for (let i = sectionIdx + 1; i < lines.length; i++) {
    const trimmed = lines[i].trim();
    if (trimmed.startsWith('[')) break; // next section
    const m = trimmed.match(/^url\s*=\s*"([^"]*)"\s*$/);
    if (m) {
      const pinned = applyPinToUrl(m[1], pin);
      if (pinned === m[1]) return false;
      lines[i] = `url = "${pinned}"`;
      fs.writeFileSync(configPath, lines.join('\n'));
      return true;
    }
  }
  return false;
}

/** Convenience: derive the pin for a project root (re-exported for callers that only need the pin). */
export function projectPin(projectPath: string): string {
  return derivePin(projectPath);
}
