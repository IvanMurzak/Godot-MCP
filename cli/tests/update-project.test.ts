import { describe, it, expect, afterEach, vi } from 'vitest';
import { EventEmitter } from 'events';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { findGodotProjectRoot, updateProject } from '../src/lib/update-project.js';

const CSG = 'com.IvanMurzak.Godot.MCP.CSG';
const TILEMAP = 'com.IvanMurzak.Godot.MCP.Tilemap';
const dirs: string[] = [];

function tmp(): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-update-'));
  dirs.push(dir);
  return dir;
}

function writeAddon(dir: string, version: string): void {
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'plugin.cfg'), `[plugin]\nname="x"\nversion="${version}"\nscript="p.cs"\n`);
}

function project(addonVersion: string | null, references: string): string {
  const root = tmp();
  fs.writeFileSync(path.join(root, 'project.godot'), 'config_version=5\n');
  fs.writeFileSync(
    path.join(root, 'Game.csproj'),
    `<Project Sdk="Godot.NET.Sdk/4.3.0">\n  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>\n  <ItemGroup>\n${references}\n  </ItemGroup>\n</Project>\n`,
  );
  if (addonVersion !== null) writeAddon(path.join(root, 'addons', 'godot_mcp'), addonVersion);
  return root;
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
  vi.unstubAllGlobals();
  while (dirs.length) fs.rmSync(dirs.pop()!, { recursive: true, force: true });
});

const floating = `<PackageReference Include="${CSG}" Version="*" />`;

describe('updateProject', () => {
  it('force-restores floating extensions even when the addon is current', async () => {
    const calls: string[][] = [];
    const root = project('2.0.0', floating);

    const result = await updateProject({ godotProjectPath: root, version: '2.0.0', spawnImpl: fakeSpawn(calls) });

    if (result.kind !== 'success') throw result.error;
    expect(result.updated.map((item) => item.packageId)).toEqual([CSG]);
    expect(calls).toEqual([['dotnet', 'restore', path.resolve(root, 'Game.csproj'), '--force', '--no-cache']]);
  });

  it('looks the latest addon up on npm when no version is given', async () => {
    const root = project('2.0.0', '');
    const fetchMock = vi.fn(async () => ({ ok: true, status: 200, json: async () => ({ version: '2.0.0' }) }));
    vi.stubGlobal('fetch', fetchMock);

    const result = await updateProject({ godotProjectPath: root, dryRun: true });

    if (result.kind !== 'success') throw result.error;
    expect(result.items[0]).toMatchObject({ kind: 'plugin', from: '2.0.0', to: '2.0.0', status: 'current' });
    expect(String(fetchMock.mock.calls[0]?.[0 as never])).toContain('registry.npmjs.org/godot-cli');
  });

  it('dryRun reports and neither downloads nor restores', async () => {
    const calls: string[][] = [];
    const root = project('1.0.0', floating);

    const result = await updateProject({ godotProjectPath: root, version: '2.0.0', dryRun: true, spawnImpl: fakeSpawn(calls) });

    if (result.kind !== 'success') throw result.error;
    expect(result.items.map((item) => item.status)).toEqual(['outdated', 'floating']);
    expect(result.updated).toEqual([]);
    expect(calls).toEqual([]);
  });

  it('keeps a concrete pin and reports it', async () => {
    const calls: string[][] = [];
    const root = project('2.0.0', `${floating}\n<PackageReference Include="${TILEMAP}" Version="0.1.0" />`);

    const result = await updateProject({ godotProjectPath: root, version: '2.0.0', spawnImpl: fakeSpawn(calls) });

    if (result.kind !== 'success') throw result.error;
    expect(result.warnings.join('\n')).toContain(TILEMAP);
    expect(fs.readFileSync(path.join(root, 'Game.csproj'), 'utf-8')).toContain(`Include="${TILEMAP}" Version="0.1.0"`);
    expect(result.updated.map((item) => item.packageId)).toEqual([CSG]);
  });

  it('a failed restore is a warning, not a failure', async () => {
    const result = await updateProject({ godotProjectPath: project('2.0.0', floating), version: '2.0.0', spawnImpl: fakeSpawn([], 1) });

    if (result.kind !== 'success') throw result.error;
    expect(result.updated).toEqual([]);
    expect(result.warnings.join('\n')).toContain('dotnet restore exited with 1');
  });

  it('leaves a linked addon alone', async () => {
    const root = project(null, '');
    const checkout = tmp();
    writeAddon(checkout, '1.0.0');
    fs.mkdirSync(path.join(root, 'addons'));
    fs.symlinkSync(checkout, path.join(root, 'addons', 'godot_mcp'));

    const result = await updateProject({ godotProjectPath: root, version: '2.0.0' });

    if (result.kind !== 'success') throw result.error;
    expect(result.items[0]?.status).toBe('local');
    expect(result.updated).toEqual([]);
  });

  it('noExtensions restores nothing', async () => {
    const calls: string[][] = [];
    const result = await updateProject({ godotProjectPath: project('2.0.0', floating), version: '2.0.0', noExtensions: true, spawnImpl: fakeSpawn(calls) });

    if (result.kind !== 'success') throw result.error;
    expect(calls).toEqual([]);
  });

  it('fails without throwing outside a Godot project', async () => {
    expect((await updateProject({ godotProjectPath: tmp() })).kind).toBe('failure');
  });
});

describe('findGodotProjectRoot', () => {
  it('finds the project from a subfolder, and null outside one', () => {
    const root = project(null, '');
    const nested = path.join(root, 'scenes', 'levels');
    fs.mkdirSync(nested, { recursive: true });

    expect(findGodotProjectRoot(nested)).toBe(root);
    expect(findGodotProjectRoot(os.tmpdir())).toBeNull();
  });
});
