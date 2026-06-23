import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { EventEmitter } from 'events';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { buildProject, findProjectCsproj } from '../src/lib/build.js';
import type { ProgressEvent } from '../src/lib/types.js';

/**
 * Build a fake `child_process.spawn` whose returned child emits the supplied
 * stdout/stderr and then `close`s with `exitCode`. Records every spawn call so a
 * test can assert the command + cwd shape. `errorOnSpawn` makes the spawn itself
 * throw synchronously (mirrors `dotnet` missing from PATH on some platforms).
 */
function makeSpawnMock(opts: {
  exitCode?: number | null;
  stdout?: string;
  stderr?: string;
  emitError?: string;
  throwOnSpawn?: string;
}): { spawnImpl: typeof import('child_process').spawn; calls: { bin: string; args: string[]; cwd?: string }[] } {
  const calls: { bin: string; args: string[]; cwd?: string }[] = [];
  const spawnImpl = ((bin: string, args: string[], options?: { cwd?: string }) => {
    calls.push({ bin, args, cwd: options?.cwd });
    if (opts.throwOnSpawn) {
      throw new Error(opts.throwOnSpawn);
    }
    const child = new EventEmitter() as EventEmitter & {
      stdout: EventEmitter;
      stderr: EventEmitter;
    };
    child.stdout = new EventEmitter();
    child.stderr = new EventEmitter();
    // Emit async so listeners are attached first.
    setImmediate(() => {
      if (opts.stdout) child.stdout.emit('data', Buffer.from(opts.stdout));
      if (opts.stderr) child.stderr.emit('data', Buffer.from(opts.stderr));
      if (opts.emitError) {
        child.emit('error', new Error(opts.emitError));
        return;
      }
      child.emit('close', opts.exitCode ?? 0);
    });
    return child;
  }) as unknown as typeof import('child_process').spawn;
  return { spawnImpl, calls };
}

function writeGodotProject(dir: string, opts: { csproj?: boolean | string[] } = {}): void {
  fs.writeFileSync(path.join(dir, 'project.godot'), 'config_version=5\n');
  if (opts.csproj === true) {
    fs.writeFileSync(path.join(dir, 'MyProject.csproj'), '<Project />');
  } else if (Array.isArray(opts.csproj)) {
    for (const name of opts.csproj) fs.writeFileSync(path.join(dir, name), '<Project />');
  }
}

describe('findProjectCsproj', () => {
  let tmpDir: string;
  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-build-csproj-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('returns null when no csproj is present (GDScript-only)', () => {
    writeGodotProject(tmpDir);
    expect(findProjectCsproj(tmpDir, [])).toBeNull();
  });

  it('returns the single root csproj', () => {
    writeGodotProject(tmpDir, { csproj: true });
    expect(findProjectCsproj(tmpDir, [])).toBe(path.join(tmpDir, 'MyProject.csproj'));
  });

  it('picks the first alphabetically and warns when several csprojs exist', () => {
    writeGodotProject(tmpDir, { csproj: ['Zeta.csproj', 'Alpha.csproj'] });
    const warnings: string[] = [];
    expect(findProjectCsproj(tmpDir, warnings)).toBe(path.join(tmpDir, 'Alpha.csproj'));
    expect(warnings).toHaveLength(1);
    expect(warnings[0]).toContain('Multiple .csproj');
  });

  it('returns null for a nonexistent directory', () => {
    expect(findProjectCsproj(path.join(tmpDir, 'nope'), [])).toBeNull();
  });
});

describe('buildProject', () => {
  let tmpDir: string;
  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-build-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('fails when the project path does not exist', async () => {
    const result = await buildProject({ projectPath: path.join(tmpDir, 'missing') });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('does not exist');
    }
  });

  it('fails for a non-Godot directory', async () => {
    const result = await buildProject({ projectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('Not a Godot project');
    }
  });

  it('skips (success) a GDScript-only project with no csproj — never spawns dotnet', async () => {
    writeGodotProject(tmpDir);
    const { spawnImpl, calls } = makeSpawnMock({ exitCode: 0 });
    const events: ProgressEvent[] = [];
    const result = await buildProject({
      projectPath: tmpDir,
      spawnImpl,
      onProgress: (e) => events.push(e),
    });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.skipped).toBe(true);
      expect(result.skipReason).toBe('no-csproj');
    }
    expect(calls).toHaveLength(0);
    expect(events.some((e) => e.phase === 'build-skipped')).toBe(true);
  });

  it('runs "dotnet build <csproj> --configuration Debug" in the project cwd for a C# project', async () => {
    writeGodotProject(tmpDir, { csproj: true });
    const { spawnImpl, calls } = makeSpawnMock({ exitCode: 0, stdout: 'Build succeeded.' });
    const result = await buildProject({ projectPath: tmpDir, spawnImpl });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.skipped).toBe(false);
      expect(result.csprojPath).toBe(path.join(tmpDir, 'MyProject.csproj'));
      expect(result.configuration).toBe('Debug');
      expect(result.output).toContain('Build succeeded.');
    }
    expect(calls).toHaveLength(1);
    expect(calls[0].bin).toBe('dotnet');
    expect(calls[0].args).toEqual([
      'build',
      path.join(tmpDir, 'MyProject.csproj'),
      '--configuration',
      'Debug',
    ]);
    expect(calls[0].cwd).toBe(path.resolve(tmpDir));
  });

  it('honors a custom configuration and dotnet path', async () => {
    writeGodotProject(tmpDir, { csproj: true });
    const { spawnImpl, calls } = makeSpawnMock({ exitCode: 0 });
    const result = await buildProject({
      projectPath: tmpDir,
      configuration: 'Release',
      dotnetPath: '/opt/dotnet/dotnet',
      spawnImpl,
    });
    expect(result.kind).toBe('success');
    expect(calls[0].bin).toBe('/opt/dotnet/dotnet');
    expect(calls[0].args).toContain('Release');
  });

  it('fails (structured) and surfaces output when dotnet build exits non-zero', async () => {
    writeGodotProject(tmpDir, { csproj: true });
    const { spawnImpl } = makeSpawnMock({ exitCode: 1, stderr: 'error CS0103: missing' });
    const result = await buildProject({ projectPath: tmpDir, spawnImpl });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('dotnet build failed');
      expect(result.errorMessage).toContain('error CS0103');
    }
  });

  it('fails (structured) when the spawn itself errors (dotnet missing)', async () => {
    writeGodotProject(tmpDir, { csproj: true });
    const { spawnImpl } = makeSpawnMock({ emitError: 'spawn dotnet ENOENT' });
    const result = await buildProject({ projectPath: tmpDir, spawnImpl });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('dotnet build failed');
    }
  });

  it('fails (structured) when spawn throws synchronously', async () => {
    writeGodotProject(tmpDir, { csproj: true });
    const { spawnImpl } = makeSpawnMock({ throwOnSpawn: 'EACCES' });
    const result = await buildProject({ projectPath: tmpDir, spawnImpl });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.errorMessage).toContain('dotnet build failed');
    }
  });
});
