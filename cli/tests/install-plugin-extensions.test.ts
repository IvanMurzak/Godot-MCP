import { describe, it, expect, afterEach } from 'vitest';
import { EventEmitter } from 'events';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { installPlugin } from '../src/lib/install-plugin.js';

const CSG = 'com.IvanMurzak.Godot.MCP.CSG';
const TILEMAP = 'com.IvanMurzak.Godot.MCP.Tilemap';
const dirs: string[] = [];

function tmp(): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-ext-'));
  dirs.push(dir);
  return dir;
}

function writeAddon(dir: string, version: string): void {
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'plugin.cfg'), `[plugin]\nname="x"\nversion="${version}"\nscript="p.cs"\n`);
}

function project(installedVersion: string | null, references: string): string {
  const root = tmp();
  fs.writeFileSync(path.join(root, 'project.godot'), 'config_version=5\n');
  fs.writeFileSync(
    path.join(root, 'Game.csproj'),
    `<Project Sdk="Godot.NET.Sdk/4.3.0">\n  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>\n  <ItemGroup>\n${references}\n  </ItemGroup>\n</Project>\n`,
  );
  if (installedVersion !== null) writeAddon(path.join(root, 'addons', 'godot_mcp'), installedVersion);
  return root;
}

function source(version: string): string {
  const dir = tmp();
  writeAddon(dir, version);
  return dir;
}

/** A `spawn` that records its calls and ends with `code`. */
function fakeSpawn(calls: string[][], code = 0): typeof import('child_process').spawn {
  return ((bin: string, args: string[]) => {
    calls.push([bin, ...args]);
    const child = new EventEmitter();
    setImmediate(() => child.emit('close', code));
    return child;
  }) as unknown as typeof import('child_process').spawn;
}

afterEach(() => {
  while (dirs.length) fs.rmSync(dirs.pop()!, { recursive: true, force: true });
});

describe('installPlugin brings the installed extensions along with an addon update', () => {
  const floating = `<PackageReference Include="${CSG}" Version="*" />`;

  it('force-restores floating extensions when the addon version changes', async () => {
    const calls: string[][] = [];
    const root = project('1.0.0', floating);

    const result = await installPlugin({ godotProjectPath: root, source: source('2.0.0'), spawnImpl: fakeSpawn(calls) });

    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.extensions).toEqual({ refreshed: [CSG], keptPinned: [] });
    expect(calls).toHaveLength(1);
    expect(calls[0]).toEqual(['dotnet', 'restore', path.resolve(root, 'Game.csproj'), '--force', '--no-cache']);
  });

  it('does nothing when the addon version is unchanged', async () => {
    const calls: string[][] = [];
    const result = await installPlugin({ godotProjectPath: project('2.0.0', floating), source: source('2.0.0'), spawnImpl: fakeSpawn(calls) });

    expect(calls).toHaveLength(0);
    expect(result.kind === 'success' && result.extensions.refreshed).toEqual([]);
  });

  it('does nothing on a first install', async () => {
    const calls: string[][] = [];
    await installPlugin({ godotProjectPath: project(null, floating), source: source('2.0.0'), spawnImpl: fakeSpawn(calls) });

    expect(calls).toHaveLength(0);
  });

  it('keeps a concrete pin and reports it', async () => {
    const calls: string[][] = [];
    const root = project('1.0.0', `${floating}\n<PackageReference Include="${TILEMAP}" Version="0.1.0" />`);

    const result = await installPlugin({ godotProjectPath: root, source: source('2.0.0'), spawnImpl: fakeSpawn(calls) });

    if (result.kind !== 'success') throw new Error('expected success');
    expect(result.extensions).toEqual({ refreshed: [CSG], keptPinned: [TILEMAP] });
    expect(result.warnings.join('\n')).toContain(TILEMAP);
    expect(fs.readFileSync(path.join(root, 'Game.csproj'), 'utf-8')).toContain(`Include="${TILEMAP}" Version="0.1.0"`);
  });

  it('a failed restore is a warning, not a failed install', async () => {
    const result = await installPlugin({ godotProjectPath: project('1.0.0', floating), source: source('2.0.0'), spawnImpl: fakeSpawn([], 1) });

    if (result.kind !== 'success') throw new Error('expected success');
    expect(result.extensions.refreshed).toEqual([]);
    expect(result.warnings.join('\n')).toContain('dotnet restore exited with 1');
  });
});
