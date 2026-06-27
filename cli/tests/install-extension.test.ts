import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { installExtension } from '../src/lib/install-extension.js';
import { parsePackageReferences } from '../src/utils/extension-install.js';
import type { ExtensionDescriptor } from '../src/utils/extensions-catalog.js';

const MINIMAL_PROJECT = '; Engine configuration file.\nconfig_version=5\n\n[application]\n\nconfig/name="Test"\n';

const SampleCsproj = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.3.1" />
  </ItemGroup>
</Project>`;

// Injected fixture catalog (the shipped EXTENSIONS_CATALOG is empty until a package is published).
const FIXTURE_CATALOG: readonly ExtensionDescriptor[] = [
  {
    name: 'ProBuilder Tools',
    description: 'Mesh-editing MCP tools for Godot.',
    packageId: 'com.IvanMurzak.Godot.MCP.ProBuilder',
    version: '1.2.0',
    gitUrl: null,
    tools: [],
  },
];

describe('installExtension — lib logic (parity with the dock ExtensionInstaller)', () => {
  let tmpDir: string;
  let csprojPath: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-ext-'));
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), MINIMAL_PROJECT);
    csprojPath = path.join(tmpDir, 'Game.csproj');
    fs.writeFileSync(csprojPath, SampleCsproj);
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('adds the PackageReference and requests a rebuild', async () => {
    const result = await installExtension({
      godotProjectPath: tmpDir,
      extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder',
      catalog: FIXTURE_CATALOG,
    });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.outcome).toBe('added');
      expect(result.changed).toBe(true);
      expect(result.rebuildRequired).toBe(true);
      expect(result.packageId).toBe('com.IvanMurzak.Godot.MCP.ProBuilder');
      expect(result.toVersion).toBe('1.2.0');
      expect(result.message).toContain('Rebuild solutions');
    }
    const refs = parsePackageReferences(fs.readFileSync(csprojPath, 'utf-8'));
    expect(refs.get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
    // Pre-existing reference preserved.
    expect(refs.get('com.ivanmurzak.reflectornet')).toBe('5.3.1');
  });

  it('is idempotent — a second install reports already-up-to-date with no write', async () => {
    await installExtension({ godotProjectPath: tmpDir, extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder', catalog: FIXTURE_CATALOG });
    const before = fs.readFileSync(csprojPath, 'utf-8');
    const second = await installExtension({ godotProjectPath: tmpDir, extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder', catalog: FIXTURE_CATALOG });
    expect(second.kind).toBe('success');
    if (second.kind === 'success') {
      expect(second.outcome).toBe('already-up-to-date');
      expect(second.changed).toBe(false);
      expect(second.rebuildRequired).toBe(false);
    }
    expect(fs.readFileSync(csprojPath, 'utf-8')).toBe(before); // byte-identical (no write)
  });

  it('updates when the catalog pins a newer version', async () => {
    const old: readonly ExtensionDescriptor[] = [{ ...FIXTURE_CATALOG[0], version: '1.0.0' }];
    await installExtension({ godotProjectPath: tmpDir, extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder', catalog: old });
    const result = await installExtension({ godotProjectPath: tmpDir, extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder', catalog: FIXTURE_CATALOG });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.outcome).toBe('updated');
      expect(result.fromVersion).toBe('1.0.0');
      expect(result.toVersion).toBe('1.2.0');
    }
    expect(parsePackageReferences(fs.readFileSync(csprojPath, 'utf-8')).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
  });

  it('resolves the extension by name (case-insensitive), not just package id', async () => {
    const result = await installExtension({ godotProjectPath: tmpDir, extensionId: 'probuilder tools', catalog: FIXTURE_CATALOG });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') expect(result.packageId).toBe('com.IvanMurzak.Godot.MCP.ProBuilder');
  });

  it('honors the --version override and warns about overriding the catalog pin', async () => {
    const result = await installExtension({
      godotProjectPath: tmpDir,
      extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder',
      version: '2.5.0',
      catalog: FIXTURE_CATALOG,
    });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.toVersion).toBe('2.5.0');
      expect(result.warnings.some((w) => w.includes('overrides the catalog pin'))).toBe(true);
    }
    expect(parsePackageReferences(fs.readFileSync(csprojPath, 'utf-8')).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('2.5.0');
  });

  it('fails with a clear error for an unknown extension id', async () => {
    const result = await installExtension({ godotProjectPath: tmpDir, extensionId: 'com.does.not.exist', catalog: FIXTURE_CATALOG });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toContain('Unknown extension');
  });

  it('reports the empty catalog clearly when nothing is published', async () => {
    const result = await installExtension({ godotProjectPath: tmpDir, extensionId: 'anything', catalog: [] });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toContain('catalog is currently empty');
  });

  it('returns a no-project outcome (success) when there is no consumer .csproj', async () => {
    fs.rmSync(csprojPath);
    const result = await installExtension({ godotProjectPath: tmpDir, extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder', catalog: FIXTURE_CATALOG });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.outcome).toBe('no-project');
      expect(result.changed).toBe(false);
      // The no-project explanation is surfaced once via result.message (printed as Status),
      // not duplicated into warnings.
      expect(result.message).toContain('.csproj');
      expect(result.warnings.some((w) => w.includes('.csproj'))).toBe(false);
    }
  });

  it('returns a structured failure (never throws) for a non-Godot dir', async () => {
    const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-ext-empty-'));
    try {
      const result = await installExtension({ godotProjectPath: empty, extensionId: 'com.IvanMurzak.Godot.MCP.ProBuilder', catalog: FIXTURE_CATALOG });
      expect(result.kind).toBe('failure');
      if (result.kind === 'failure') expect(result.error.message).toContain('project.godot');
    } finally {
      fs.rmSync(empty, { recursive: true, force: true });
    }
  });
});
