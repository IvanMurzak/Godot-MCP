import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { EventEmitter } from 'events';
import type { spawn as SpawnType } from 'child_process';
import { configureAgentViaServer } from '../src/lib/configure-agent.js';
import { getManagedServerDir, getManagedServerExecutablePath } from '../src/lib/install-server.js';

interface SpawnCall {
  bin: string;
  args: string[];
  cwd?: string;
}

/** A fake `spawn` that records the call and emits `stdout` + `close(code)` on next tick. */
function fakeSpawn(exitCode: number, stdout: string, calls: SpawnCall[]): typeof SpawnType {
  return ((bin: string, args: string[], opts: { cwd?: string }) => {
    calls.push({ bin, args, cwd: opts?.cwd });
    const child = new EventEmitter() as EventEmitter & {
      stdout: EventEmitter;
      stderr: EventEmitter;
    };
    child.stdout = new EventEmitter();
    child.stderr = new EventEmitter();
    setImmediate(() => {
      if (stdout) child.stdout.emit('data', Buffer.from(stdout));
      child.emit('close', exitCode);
    });
    return child as unknown as ReturnType<typeof SpawnType>;
  }) as unknown as typeof SpawnType;
}

/** Create the managed server binary so the existsSync gate passes. */
function planetManagedBinary(project: string): string {
  const dir = getManagedServerDir(project);
  fs.mkdirSync(dir, { recursive: true });
  const exe = getManagedServerExecutablePath(project);
  fs.writeFileSync(exe, '#!/bin/sh\necho stub\n');
  return exe;
}

describe('configureAgentViaServer', () => {
  let project: string;
  beforeEach(() => {
    project = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-cfg-'));
  });
  afterEach(() => fs.rmSync(project, { recursive: true, force: true }));

  it('proxies `configure --agent <id>` to the managed binary with cwd=project and forwards exit 0', async () => {
    const exe = planetManagedBinary(project);
    const calls: SpawnCall[] = [];
    const result = await configureAgentViaServer({
      godotProjectPath: project,
      agentId: 'claude-code',
      spawnImpl: fakeSpawn(0, 'wrote .mcp.json\n', calls),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.serverBinaryPath).toBe(exe);
    expect(result.args).toEqual(['configure', '--agent', 'claude-code']);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('wrote .mcp.json');
    expect(calls).toHaveLength(1);
    expect(calls[0].bin).toBe(exe);
    expect(calls[0].args).toEqual(['configure', '--agent', 'claude-code']);
    expect(path.resolve(calls[0].cwd ?? '')).toBe(path.resolve(project));
  });

  it('forwards --url when supplied', async () => {
    planetManagedBinary(project);
    const calls: SpawnCall[] = [];
    await configureAgentViaServer({
      godotProjectPath: project,
      agentId: 'cursor',
      url: 'http://localhost:20001/mcp',
      spawnImpl: fakeSpawn(0, '', calls),
    });
    expect(calls[0].args).toEqual(['configure', '--agent', 'cursor', '--url', 'http://localhost:20001/mcp']);
  });

  it('fails cleanly (not ENOENT) when no managed server binary is present', async () => {
    let spawned = false;
    const spy = (() => {
      spawned = true;
      throw new Error('should not spawn');
    }) as unknown as typeof SpawnType;
    const result = await configureAgentViaServer({ godotProjectPath: project, agentId: 'claude-code', spawnImpl: spy });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/install-plugin --with-server/);
    expect(spawned).toBe(false);
  });

  it('surfaces the server binary’s non-zero exit code', async () => {
    planetManagedBinary(project);
    const calls: SpawnCall[] = [];
    const result = await configureAgentViaServer({
      godotProjectPath: project,
      agentId: 'unknown-agent',
      spawnImpl: fakeSpawn(2, 'error: unknown agent\n', calls),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.exitCode).toBe(2);
      expect(result.error.message).toMatch(/exited with code 2/);
    }
  });

  it('rejects an empty agent id', async () => {
    planetManagedBinary(project);
    const result = await configureAgentViaServer({ godotProjectPath: project, agentId: '   ' });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/agent id/i);
  });
});
