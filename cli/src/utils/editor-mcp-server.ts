import { execFileSync, type ExecFileSyncOptionsWithStringEncoding } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { platform } from 'os';
import { isProcessAlive, killProcessTree, readProcessStart } from '@baizor/gamedev-cli-core';
import { waitForExit } from './godot-shutdown.js';
import { verbose } from './ui.js';

/**
 * The local MCP server is launched by the addon (`GodotMcpServerManager.StartServer`) as a child process of the
 * editor. The plugin stops it when the editor quits cleanly, but a hard-killed editor leaves it running. `close`
 * finds it BEFORE the editor goes away — by process parentage, never by command line — and stops it afterwards.
 * A stopgap for `close` only: a server that watched its parent PID itself would also cover editor crashes.
 */

/** File name of the server binary (`GodotMcpServerView.ExecutableName`), without `.exe`. */
const MCP_SERVER_EXECUTABLE = 'gamedev-mcp-server';

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
 * Every process below `rootPid`: children, grandchildren (the `_console` wrapper runs the editor as its child). A
 * Windows parent PID can name a dead process whose PID was reused, so a child that started before its supposed parent
 * is not its child.
 */
function descendantsOf(table: readonly ProcessEntry[], rootPid: number): ProcessEntry[] {
  const byPid = new Map(table.map((e) => [e.pid, e]));
  const children = new Map<number, ProcessEntry[]>();
  for (const e of table) children.set(e.ppid, [...(children.get(e.ppid) ?? []), e]);
  const found: ProcessEntry[] = [];
  const seen = new Set<number>([rootPid]);
  const queue = [rootPid];
  while (queue.length > 0) {
    const parent = queue.shift()!;
    const parentStarted = byPid.get(parent)?.startedMs;
    for (const entry of children.get(parent) ?? []) {
      if (seen.has(entry.pid)) continue;
      if (parentStarted !== undefined && entry.startedMs !== undefined && entry.startedMs < parentStarted) continue;
      seen.add(entry.pid);
      queue.push(entry.pid);
      found.push(entry);
    }
  }
  return found;
}

/**
 * The PIDs of the processes below `rootPid` that run the server binary. `nameOf` gives a row's executable name (by
 * default its table name).
 */
export function findServerDescendants(
  table: readonly ProcessEntry[],
  rootPid: number,
  nameOf: (entry: ProcessEntry) => string = (e) => e.name,
): number[] {
  return descendantsOf(table, rootPid).filter((e) => isMcpServerName(nameOf(e))).map((e) => e.pid);
}

/**
 * The executable a process runs. On Linux `ps` reports `comm`, the MAIN THREAD's name, which a process may rename
 * (Node.js 24+ names it `MainThread`), so the `/proc/<pid>/exe` link is read instead, falling back to `comm` when it
 * cannot be read (another user's process). Elsewhere the table name already is the executable.
 */
export function executableName(entry: ProcessEntry): string {
  if (platform() !== 'linux') return entry.name;
  try {
    return fs.readlinkSync(`/proc/${entry.pid}/exe`).replace(/ \(deleted\)$/, '');
  } catch {
    return entry.name;
  }
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

const EXEC_OPTIONS: ExecFileSyncOptionsWithStringEncoding = { encoding: 'utf-8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 16 * 1024 * 1024 };

/**
 * The process table (PID, parent PID, name), or empty when it cannot be read. On Windows only Godot and server rows:
 * the addon starts the server straight from the editor, which the `_console` wrapper runs, so no other process sits
 * between them.
 */
export function listProcessTable(): ProcessEntry[] {
  try {
    if (platform() === 'win32') {
      const script =
        `Get-CimInstance Win32_Process -Filter "Name LIKE '%godot%' OR Name LIKE '${MCP_SERVER_EXECUTABLE}%'" | ` +
        'ForEach-Object { "$($_.ProcessId) $($_.ParentProcessId) ' +
        '$(if ($_.CreationDate) { $_.CreationDate.ToUniversalTime().Ticks } else { 0 }) $($_.Name)" }';
      const out = execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { ...EXEC_OPTIONS, timeout: 20000 });
      return parseProcessTable(out, true);
    }
    const out = execFileSync('ps', ['-A', '-o', 'pid=,ppid=,comm='], { ...EXEC_OPTIONS, timeout: 10000 });
    return parseProcessTable(out, false);
  } catch (err) {
    verbose(`Failed to list processes: ${err instanceof Error ? err.message : String(err)}`);
    return [];
  }
}

/** The MCP server processes the editor `editorPid` started (its own descendants only), with their start tokens. */
export async function findEditorServers(editorPid: number): Promise<EditorServer[]> {
  const pids = findServerDescendants(listProcessTable(), editorPid, executableName);
  const servers = await Promise.all(pids.map(async (pid) => ({ pid, start: await readProcessStart(pid) })));
  for (const s of servers) verbose(`Editor PID ${editorPid} runs MCP server PID ${s.pid}`);
  return servers;
}

/**
 * Once the editor `editorPid` is gone, kill each of its servers still running, with the processes it started. A server
 * is skipped while its editor is alive, and when its PID now belongs to another process (start token changed). Returns
 * the PIDs stopped.
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
    // POSIX: the server leads no process group (.NET's Process.Start keeps the editor's), so killProcessTree reaches only
    // the server itself; its children are taken from the table. Windows: `taskkill /T` covers them.
    const children = platform() === 'win32' ? [] : descendantsOf(listProcessTable(), server.pid);
    await killProcessTree(server.pid);
    for (const child of children) {
      try {
        process.kill(child.pid, 'SIGKILL');
      } catch {
        /* already gone */
      }
    }
    if (await waitForExit(server.pid, 5000)) stopped.push(server.pid);
    else verbose(`MCP server PID ${server.pid} is still alive after the kill`);
  }
  return stopped;
}
