import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import { spawn } from 'child_process';
import * as path from 'path';
import {
  executableName,
  findEditorServers,
  findServerDescendants,
  isMcpServerName,
  parseProcessTable,
  stopOrphanedServers,
  type ProcessEntry,
} from '../src/utils/editor-mcp-server.js';
import { isProcessAlive, sendForceKill, waitForExit } from '../src/utils/godot-shutdown.js';

describe('editor-mcp-server — isMcpServerName', () => {
  it('matches the server binary on every OS', () => {
    expect(isMcpServerName('gamedev-mcp-server.exe')).toBe(true);
    expect(isMcpServerName('GameDev-MCP-Server.EXE')).toBe(true);
    expect(isMcpServerName('gamedev-mcp-server')).toBe(true);
    expect(isMcpServerName('/p/.godot/mcp-server/osx-arm64/gamedev-mcp-server')).toBe(true);
    expect(isMcpServerName('gamedev-mcp-ser')).toBe(true); // Linux comm, truncated to 15 chars
  });

  it('rejects other processes', () => {
    expect(isMcpServerName('godot.exe')).toBe(false);
    expect(isMcpServerName('gamedev-mcp')).toBe(false);
    expect(isMcpServerName('gamedev-mcp-server-helper.exe')).toBe(false);
    expect(isMcpServerName('node')).toBe(false);
  });
});

describe('editor-mcp-server — parseProcessTable', () => {
  it('parses Windows rows with .NET ticks and names with spaces', () => {
    const rows = parseProcessTable('10 4 638000000000000000 Godot_v4.4-stable_mono_win64.exe\r\n20 10 0 My Tool.exe\r\nbad line\r\n', true);
    expect(rows).toEqual([
      { pid: 10, ppid: 4, name: 'Godot_v4.4-stable_mono_win64.exe', startedMs: (638000000000000000 - 621355968000000000) / 10000 },
      { pid: 20, ppid: 10, name: 'My Tool.exe', startedMs: -62135596800000 },
    ]);
  });

  it('parses ps rows', () => {
    expect(parseProcessTable('  1     0 /sbin/launchd\n 77    1 gamedev-mcp-ser\n', false)).toEqual([
      { pid: 1, ppid: 0, name: '/sbin/launchd' },
      { pid: 77, ppid: 1, name: 'gamedev-mcp-ser' },
    ]);
  });
});

describe('editor-mcp-server — findServerDescendants', () => {
  const table: ProcessEntry[] = [
    { pid: 100, ppid: 1, name: 'godot_console.exe', startedMs: 1000 },
    { pid: 101, ppid: 100, name: 'godot.exe', startedMs: 1001 },
    { pid: 102, ppid: 101, name: 'gamedev-mcp-server.exe', startedMs: 1005 },
    { pid: 103, ppid: 101, name: 'conhost.exe', startedMs: 1002 },
    // Another project's editor and server: never ours.
    { pid: 200, ppid: 1, name: 'godot.exe', startedMs: 900 },
    { pid: 201, ppid: 200, name: 'gamedev-mcp-server.exe', startedMs: 950 },
    // A server whose dead parent's PID was reused by editor 100: it started before 100, so it is not 100's child.
    { pid: 300, ppid: 100, name: 'gamedev-mcp-server.exe', startedMs: 500 },
  ];

  it('finds the server under the console wrapper and its editor', () => {
    expect(findServerDescendants(table, 100)).toEqual([102]);
    expect(findServerDescendants(table, 101)).toEqual([102]);
  });

  it("never returns another editor's server", () => {
    expect(findServerDescendants(table, 200)).toEqual([201]);
    expect(findServerDescendants(table, 999)).toEqual([]);
  });

  it('follows children when start times are unknown (POSIX)', () => {
    const posix = table.map(({ startedMs: _s, ...rest }) => rest);
    expect(findServerDescendants(posix, 100).sort()).toEqual([102, 300]);
  });

  it('matches by executable, not by a renamed main thread (Linux comm)', () => {
    // Node.js 24+ renames its main thread, so Linux `ps -o comm=` shows `MainThread` for a Node-based process.
    const renamed: ProcessEntry[] = [
      { pid: 100, ppid: 1, name: 'godot' },
      { pid: 102, ppid: 100, name: 'MainThread' },
    ];
    expect(findServerDescendants(renamed, 100)).toEqual([]);
    const exePath = (e: ProcessEntry) => (e.pid === 102 ? '/p/.godot/mcp-server/linux-x64/gamedev-mcp-server' : e.name);
    expect(findServerDescendants(renamed, 100, exePath)).toEqual([102]);
  });
});

describe('editor-mcp-server — executableName', () => {
  it.runIf(process.platform === 'linux')('reads the executable of a live process, not its thread name', () => {
    expect(path.basename(executableName({ pid: process.pid, ppid: 0, name: 'MainThread' }))).toBe(path.basename(process.execPath));
  });

  it('falls back to the table name when the executable cannot be read', () => {
    expect(executableName({ pid: 2147483646, ppid: 0, name: 'gamedev-mcp-ser' })).toBe('gamedev-mcp-ser');
  });
});

const exe = process.platform === 'win32' ? '.exe' : '';
let binDir: string;

/**
 * Node under the editor's and the server's process names. POSIX: a hard link (cheap). Windows: a copy, since a link to
 * the running node.exe could not be deleted afterwards.
 */
function nodeAs(name: string): string {
  const target = path.join(binDir, name + exe);
  try {
    if (process.platform === 'win32') throw new Error('copy');
    fs.linkSync(process.execPath, target);
  } catch {
    fs.copyFileSync(process.execPath, target);
    fs.chmodSync(target, 0o755);
  }
  return target;
}

/** Kill what a test started, unless already seen gone (a dead PID may be reused by now). */
function cleanup(...pids: number[]): void {
  for (const pid of pids) if (isProcessAlive(pid)) sendForceKill(pid);
}

beforeAll(() => {
  binDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-close-'));
  nodeAs('godot-fake-editor');
  nodeAs('gamedev-mcp-server');
});

afterAll(() => {
  try {
    fs.rmSync(binDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  } catch {
    /* best effort: a just-killed process can still hold its image for a moment */
  }
});

/**
 * A fake editor that starts a fake server child and prints its PID. Detached: libuv kills attached children with
 * their parent on Windows, which Godot does not.
 */
async function spawnEditorWithServer(): Promise<{ editorPid: number; serverPid: number }> {
  const server = JSON.stringify(path.join(binDir, 'gamedev-mcp-server' + exe));
  const script = `const c=require('child_process').spawn(${server},['-e','setTimeout(()=>{},60000)'],{stdio:'ignore',detached:true,windowsHide:true});` +
    `console.log(c.pid);setTimeout(()=>{},60000);`;
  const editor = spawn(path.join(binDir, 'godot-fake-editor' + exe), ['-e', script], { stdio: ['ignore', 'pipe', 'ignore'] });
  const serverPid = await new Promise<number>((resolve, reject) => {
    editor.stdout!.once('data', (d: Buffer) => resolve(parseInt(d.toString(), 10)));
    editor.once('error', reject);
  });
  return { editorPid: editor.pid!, serverPid };
}

describe('editor-mcp-server — stopOrphanedServers (live processes)', () => {
  it('stops the server a killed editor left behind, and only after the editor is gone', async () => {
    const { editorPid, serverPid } = await spawnEditorWithServer();
    try {
      const servers = await findEditorServers(editorPid);
      expect(servers.map((s) => s.pid)).toEqual([serverPid]);

      // The editor is alive: its server is not touched.
      expect(await stopOrphanedServers(editorPid, servers)).toEqual([]);
      expect(isProcessAlive(serverPid)).toBe(true);

      // Kill the editor alone (what `close --force` does to a GUI editor): the server survives it...
      sendForceKill(editorPid);
      expect(await waitForExit(editorPid, 10000)).toBe(true);
      expect(isProcessAlive(serverPid)).toBe(true);

      // ...until the sweep.
      expect(await stopOrphanedServers(editorPid, servers)).toEqual([serverPid]);
      expect(isProcessAlive(serverPid)).toBe(false);
    } finally {
      cleanup(editorPid, serverPid);
    }
  }, 60000);

  it('leaves a process alone when its start token no longer matches (PID reused)', async () => {
    const { editorPid, serverPid } = await spawnEditorWithServer();
    try {
      sendForceKill(editorPid);
      expect(await waitForExit(editorPid, 10000)).toBe(true);
      expect(await stopOrphanedServers(editorPid, [{ pid: serverPid, start: 'not-this-process' }])).toEqual([]);
      expect(await stopOrphanedServers(editorPid, [{ pid: serverPid, start: undefined }])).toEqual([]);
      expect(isProcessAlive(serverPid)).toBe(true);
    } finally {
      cleanup(editorPid, serverPid);
    }
  }, 60000);
});
