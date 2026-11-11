import { describe, it, expect } from 'vitest';
import { spawn } from 'child_process';
import * as path from 'path';
import {
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
});

/** A node "editor" that starts a node "server" child and prints its PID (detached: libuv kills attached children with it on Windows, unlike Godot). */
async function spawnEditorWithServer(): Promise<{ editorPid: number; serverPid: number }> {
  const script = `const c=require('child_process').spawn(process.execPath,['-e','setTimeout(()=>{},60000)'],{stdio:'ignore',detached:true,windowsHide:true});` +
    `console.log(c.pid);setTimeout(()=>{},60000);`;
  const editor = spawn(process.execPath, ['-e', script], { stdio: ['ignore', 'pipe', 'ignore'] });
  const serverPid = await new Promise<number>((resolve, reject) => {
    editor.stdout!.once('data', (d: Buffer) => resolve(parseInt(d.toString(), 10)));
    editor.once('error', reject);
  });
  return { editorPid: editor.pid!, serverPid };
}

const isNode = (e: ProcessEntry) => path.basename(e.name).toLowerCase().replace(/\.exe$/, '') === 'node';

describe('editor-mcp-server — stopOrphanedServers (live processes)', () => {
  it('stops the server a killed editor left behind, and only after the editor is gone', async () => {
    const { editorPid, serverPid } = await spawnEditorWithServer();
    try {
      const servers = await findEditorServers(editorPid, isNode);
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
      sendForceKill(editorPid, undefined, true);
      sendForceKill(serverPid);
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
      sendForceKill(editorPid, undefined, true);
      sendForceKill(serverPid);
    }
  }, 60000);
});
