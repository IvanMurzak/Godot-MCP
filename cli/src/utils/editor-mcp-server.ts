import { execFileSync } from 'child_process';
import * as path from 'path';
import { platform } from 'os';
import { killProcessTree, readProcessStart } from '@baizor/gamedev-cli-core';
import { isProcessAlive, waitForExit } from './godot-shutdown.js';
import { verbose } from './ui.js';

/**
 * The local MCP server is launched by the addon (`GodotMcpServerManager.StartServer`) as a child process of the
 * editor. The plugin stops it when the editor quits cleanly, but a hard-killed editor leaves it running. `close`
 * finds it BEFORE the editor goes away — by process parentage, never by command line — and stops it afterwards.
 */

/** File name of the server binary (`GodotMcpServerView.ExecutableName`), without `.exe`. */
export const MCP_SERVER_EXECUTABLE = 'gamedev-mcp-server';

/** One row of the OS process table. `startedMs` is known only on Windows (where parent PIDs go stale). */
export interface ProcessEntry {
  pid: number;
  ppid: number;
  name: string;
  startedMs?: number;
}

/** A server process found under an editor, with the start-time token that proves it is still the same process. */
export interface EditorServer {
  pid: number;
  start: string | undefined;
}

/** True for the server binary's process name: `gamedev-mcp-server[.exe]`, a full path, or Linux's 15-char `comm`. */
export function isMcpServerName(name: string): boolean {
  const base = path.basename(name.trim().replace(/\\/g, '/')).toLowerCase().replace(/\.exe$/, '');
  return base === MCP_SERVER_EXECUTABLE || (base.length === 15 && MCP_SERVER_EXECUTABLE.startsWith(base));
}

/**
 * The PIDs of every process below `rootPid` (children, grandchildren — the `_console` wrapper runs the editor as its
 * child) that `isServer` accepts. A Windows parent PID can name a dead process whose PID was reused, so a child that
 * started before its supposed parent is not its child.
 */
export function findServerDescendants(
  table: readonly ProcessEntry[],
  rootPid: number,
  isServer: (entry: ProcessEntry) => boolean = (e) => isMcpServerName(e.name),
): number[] {
  const byPid = new Map(table.map((e) => [e.pid, e]));
  const found: number[] = [];
  const seen = new Set<number>([rootPid]);
  const queue = [rootPid];
  while (queue.length > 0) {
    const parent = queue.shift()!;
    const parentStarted = byPid.get(parent)?.startedMs;
    for (const entry of table) {
      if (entry.ppid !== parent || seen.has(entry.pid)) continue;
      if (parentStarted !== undefined && entry.startedMs !== undefined && entry.startedMs < parentStarted) continue;
      seen.add(entry.pid);
      queue.push(entry.pid);
      if (isServer(entry)) found.push(entry.pid);
    }
  }
  return found;
}

/** Parse `pid ppid [startedTicks] name` lines (Windows rows carry UTC .NET ticks). */
export function parseProcessTable(output: string, withStart: boolean): ProcessEntry[] {
  const rows: ProcessEntry[] = [];
  const pattern = withStart ? /^\s*(\d+)\s+(\d+)\s+(\d+)\s+(.+?)\s*$/ : /^\s*(\d+)\s+(\d+)\s+(.+?)\s*$/;
  for (const line of output.split(/\r?\n/)) {
    const m = line.match(pattern);
    if (!m) continue;
    const entry: ProcessEntry = { pid: Number(m[1]), ppid: Number(m[2]), name: withStart ? m[4] : m[3] };
    // .NET ticks (100 ns since 0001-01-01) to epoch ms; only ever compared with each other.
    if (withStart) entry.startedMs = Number((BigInt(m[3]) - 621355968000000000n) / 10000n);
    rows.push(entry);
  }
  return rows;
}

/** The whole process table (PID, parent PID, name), or empty when it cannot be read. */
export function listProcessTable(): ProcessEntry[] {
  try {
    if (platform() === 'win32') {
      const script =
        'Get-CimInstance Win32_Process | ForEach-Object { "$($_.ProcessId) $($_.ParentProcessId) ' +
        '$(if ($_.CreationDate) { $_.CreationDate.ToUniversalTime().Ticks } else { 0 }) $($_.Name)" }';
      const out = execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], {
        encoding: 'utf-8',
        timeout: 20000,
        stdio: ['ignore', 'pipe', 'pipe'],
        maxBuffer: 16 * 1024 * 1024,
      });
      return parseProcessTable(out, true);
    }
    const out = execFileSync('ps', ['-A', '-o', 'pid=,ppid=,comm='], {
      encoding: 'utf-8',
      timeout: 10000,
      stdio: ['ignore', 'pipe', 'pipe'],
      maxBuffer: 16 * 1024 * 1024,
    });
    return parseProcessTable(out, false);
  } catch (err) {
    verbose(`Failed to list processes: ${err instanceof Error ? err.message : String(err)}`);
    return [];
  }
}

/** The MCP server processes the editor `editorPid` started (its own descendants only), with their start tokens. */
export async function findEditorServers(
  editorPid: number,
  isServer?: (entry: ProcessEntry) => boolean,
): Promise<EditorServer[]> {
  const pids = findServerDescendants(listProcessTable(), editorPid, isServer);
  const servers = await Promise.all(pids.map(async (pid) => ({ pid, start: await readProcessStart(pid) })));
  for (const s of servers) verbose(`Editor PID ${editorPid} runs MCP server PID ${s.pid}`);
  return servers;
}

/**
 * Once the editor `editorPid` is gone, kill (whole tree) each of its servers still running. A server is skipped while
 * its editor is alive, and when its PID now belongs to another process (start token changed). Returns the PIDs stopped.
 */
export async function stopOrphanedServers(editorPid: number, servers: readonly EditorServer[]): Promise<number[]> {
  if (servers.length === 0 || isProcessAlive(editorPid)) return [];
  const stopped: number[] = [];
  for (const server of servers) {
    if (!isProcessAlive(server.pid)) continue;
    if (server.start === undefined || (await readProcessStart(server.pid)) !== server.start) {
      verbose(`MCP server PID ${server.pid} cannot be proven to be the same process; left alone`);
      continue;
    }
    await killProcessTree(server.pid);
    if (await waitForExit(server.pid, 5000)) stopped.push(server.pid);
    else verbose(`MCP server PID ${server.pid} is still alive after the kill`);
  }
  return stopped;
}
