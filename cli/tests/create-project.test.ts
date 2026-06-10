import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  createProject,
  deriveProjectName,
  detectExistingProjectMarker,
  escapeGodotConfigString,
  renderCsproj,
  renderProjectGodot,
  toAssemblyName,
  toRootNamespace,
  validateProjectName,
} from '../src/lib/create-project.js';

describe('deriveProjectName', () => {
  it('uses the explicit name when provided', () => {
    expect(deriveProjectName('/tmp/whatever', 'MyGame')).toBe('MyGame');
  });

  it('trims whitespace from the explicit name', () => {
    expect(deriveProjectName('/tmp/whatever', '  MyGame  ')).toBe('MyGame');
  });

  it('falls back to the folder base name when no name is given', () => {
    expect(deriveProjectName(path.join('/tmp', 'Cool-Project'))).toBe('Cool-Project');
  });

  it('falls back to the folder base name for an empty/whitespace name', () => {
    expect(deriveProjectName(path.join('/tmp', 'Cool-Project'), '   ')).toBe('Cool-Project');
  });
});

describe('toAssemblyName / toRootNamespace', () => {
  it('keeps hyphenated names verbatim for the assembly name', () => {
    expect(toAssemblyName('Godot-Test-Project')).toBe('Godot-Test-Project');
  });

  it('strips non-identifier chars for the root namespace', () => {
    expect(toRootNamespace('Godot-Test-Project')).toBe('GodotTestProject');
  });

  it('prefixes an underscore when the namespace would start with a digit', () => {
    expect(toRootNamespace('3D-Demo')).toBe('_3DDemo');
  });

  it('falls back to GodotProject when nothing usable remains', () => {
    expect(toRootNamespace('---')).toBe('GodotProject');
    expect(toAssemblyName('   ')).toBe('GodotProject');
  });
});

describe('renderProjectGodot', () => {
  it('writes config_version=5, the name, and the icon reference', () => {
    const text = renderProjectGodot('MyGame', false);
    expect(text).toContain('config_version=5');
    expect(text).toContain('config/name="MyGame"');
    expect(text).toContain('config/icon="res://icon.svg"');
    expect(text).toContain('config/features=PackedStringArray("4.3")');
  });

  it('omits the [dotnet] section for a non-dotnet project', () => {
    expect(renderProjectGodot('MyGame', false)).not.toContain('[dotnet]');
  });

  it('adds the C# feature and [dotnet] assembly name for a dotnet project', () => {
    const text = renderProjectGodot('My-Game', true);
    expect(text).toContain('config/features=PackedStringArray("4.3", "C#")');
    expect(text).toContain('[dotnet]');
    expect(text).toContain('project/assembly_name="My-Game"');
  });

  it('escapes quotes and backslashes in the name so the ConfigFile stays valid', () => {
    const text = renderProjectGodot('Ev"il\\Name', true);
    // The raw, unescaped name must never appear verbatim inside the quoted value.
    expect(text).toContain('config/name="Ev\\"il\\\\Name"');
    expect(text).toContain('project/assembly_name="Ev\\"il\\\\Name"');
    expect(text).not.toContain('config/name="Ev"il');
  });
});

describe('escapeGodotConfigString', () => {
  it('escapes double-quotes and backslashes per Godot ConfigFile rules', () => {
    expect(escapeGodotConfigString('a"b')).toBe('a\\"b');
    expect(escapeGodotConfigString('a\\b')).toBe('a\\\\b');
    expect(escapeGodotConfigString('plain')).toBe('plain');
  });
});

describe('validateProjectName', () => {
  it('accepts ordinary and hyphenated names', () => {
    expect(validateProjectName('My-Game')).toBeNull();
    expect(validateProjectName('Godot-Test-Project')).toBeNull();
  });

  it('rejects line breaks', () => {
    expect(validateProjectName('X\nrun/main_scene="res://evil.tscn')).not.toBeNull();
    expect(validateProjectName('a\rb')).not.toBeNull();
  });

  it('rejects path separators and traversal', () => {
    expect(validateProjectName('../../other/proj')).not.toBeNull();
    expect(validateProjectName('sub\\dir')).not.toBeNull();
    expect(validateProjectName('a..b')).not.toBeNull();
  });

  it('rejects Windows-reserved filename characters', () => {
    expect(validateProjectName('na"me')).not.toBeNull();
    expect(validateProjectName('na:me')).not.toBeNull();
    expect(validateProjectName('na*me')).not.toBeNull();
  });
});

describe('renderCsproj', () => {
  it('matches the Godot-Test-Project reference shape', () => {
    const text = renderCsproj('Godot-Test-Project');
    expect(text).toContain('<Project Sdk="Godot.NET.Sdk/4.3.0">');
    expect(text).toContain('<TargetFramework>net8.0</TargetFramework>');
    expect(text).toContain('<Nullable>enable</Nullable>');
    expect(text).toContain('<RootNamespace>GodotTestProject</RootNamespace>');
  });

  it('does NOT include the addon-specific NuGet PackageReferences', () => {
    const text = renderCsproj('Demo');
    expect(text).not.toContain('com.IvanMurzak.McpPlugin');
    expect(text).not.toContain('com.IvanMurzak.ReflectorNet');
  });
});

describe('detectExistingProjectMarker', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-marker-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('returns null for an empty directory', () => {
    expect(detectExistingProjectMarker(tmpDir)).toBeNull();
  });

  it('returns null for a non-existent directory', () => {
    expect(detectExistingProjectMarker(path.join(tmpDir, 'nope'))).toBeNull();
  });

  it('detects a Godot project.godot', () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    expect(detectExistingProjectMarker(tmpDir)?.engine).toBe('godot');
  });

  it('detects a Unity project', () => {
    fs.mkdirSync(path.join(tmpDir, 'ProjectSettings'));
    fs.writeFileSync(path.join(tmpDir, 'ProjectSettings', 'ProjectVersion.txt'), 'm_EditorVersion: 6000\n');
    expect(detectExistingProjectMarker(tmpDir)?.engine).toBe('unity');
  });

  it('detects an Unreal project', () => {
    fs.writeFileSync(path.join(tmpDir, 'MyGame.uproject'), '{}');
    expect(detectExistingProjectMarker(tmpDir)?.engine).toBe('unreal');
  });
});

describe('createProject', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-create-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('scaffolds a minimal project.godot + icon.svg (non-dotnet)', async () => {
    const dest = path.join(tmpDir, 'NewGame');
    const result = await createProject({ projectPath: dest, name: 'NewGame' });

    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.dotnet).toBe(false);
    expect(result.projectName).toBe('NewGame');

    const projectGodot = fs.readFileSync(path.join(dest, 'project.godot'), 'utf-8');
    expect(projectGodot).toContain('config_version=5');
    expect(projectGodot).toContain('config/name="NewGame"');
    expect(projectGodot).not.toContain('[dotnet]');

    expect(fs.existsSync(path.join(dest, 'icon.svg'))).toBe(true);
    // No csproj for a non-dotnet scaffold.
    expect(fs.readdirSync(dest).some((f) => f.endsWith('.csproj'))).toBe(false);
  });

  it('derives the project name from the folder when no name is given', async () => {
    const dest = path.join(tmpDir, 'Derived-Name');
    const result = await createProject({ projectPath: dest });

    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.projectName).toBe('Derived-Name');
    const projectGodot = fs.readFileSync(path.join(dest, 'project.godot'), 'utf-8');
    expect(projectGodot).toContain('config/name="Derived-Name"');
  });

  it('scaffolds a Godot.NET.Sdk csproj for the --dotnet variant', async () => {
    const dest = path.join(tmpDir, 'CsharpGame');
    const result = await createProject({ projectPath: dest, name: 'CsharpGame', dotnet: true });

    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.dotnet).toBe(true);

    const csprojPath = path.join(dest, 'CsharpGame.csproj');
    expect(fs.existsSync(csprojPath)).toBe(true);
    const csproj = fs.readFileSync(csprojPath, 'utf-8');
    expect(csproj).toContain('<Project Sdk="Godot.NET.Sdk/4.3.0">');
    expect(csproj).toContain('<RootNamespace>CsharpGame</RootNamespace>');

    const projectGodot = fs.readFileSync(path.join(dest, 'project.godot'), 'utf-8');
    expect(projectGodot).toContain('[dotnet]');
    expect(projectGodot).toContain('project/assembly_name="CsharpGame"');
  });

  it('refuses to scaffold over an existing Godot project (structured error, no writes)', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    const before = fs.readFileSync(path.join(tmpDir, 'project.godot'), 'utf-8');

    const result = await createProject({ projectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;
    expect(result.errorMessage).toContain('godot project marker');

    // The pre-existing file was left untouched and no icon was written.
    expect(fs.readFileSync(path.join(tmpDir, 'project.godot'), 'utf-8')).toBe(before);
    expect(fs.existsSync(path.join(tmpDir, 'icon.svg'))).toBe(false);
  });

  it('refuses to scaffold over an existing Unity project', async () => {
    fs.mkdirSync(path.join(tmpDir, 'ProjectSettings'));
    fs.writeFileSync(path.join(tmpDir, 'ProjectSettings', 'ProjectVersion.txt'), 'm_EditorVersion: 6000\n');
    const result = await createProject({ projectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;
    expect(result.errorMessage).toContain('unity project marker');
  });

  it('returns a structured failure (never throws) on a filesystem error', async () => {
    // Make the target path an existing FILE so the scaffold's mkdir fails.
    const filePath = path.join(tmpDir, 'iam-a-file');
    fs.writeFileSync(filePath, 'not a directory');

    const result = await createProject({ projectPath: filePath });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;
    expect(result.error).toBeInstanceOf(Error);
    expect(result.errorMessage.length).toBeGreaterThan(0);
  });

  it('rolls back already-written files when a LATER write fails', async () => {
    // Pre-create dest with a DIRECTORY where the csproj will be written. With
    // --dotnet the write order is icon.svg -> <name>.csproj -> project.godot, so
    // icon.svg is written and recorded first, then writeFileSync to a directory
    // path throws — exercising the catch-block rollback after a recorded write.
    // (A `project.godot` directory can't be used here: detectExistingProjectMarker
    // would treat it as an existing marker and refuse before any write.)
    const dest = path.join(tmpDir, 'PartialGame');
    fs.mkdirSync(dest, { recursive: true });
    fs.mkdirSync(path.join(dest, 'PartialGame.csproj'));

    const result = await createProject({ projectPath: dest, name: 'PartialGame', dotnet: true });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;

    // icon.svg was written then rolled back; cleanup succeeded, so the
    // CreateProjectFailure.filesWritten contract reports an empty list.
    expect(fs.existsSync(path.join(dest, 'icon.svg'))).toBe(false);
    expect(result.filesWritten).toEqual([]);
    // The caller-provided directory itself is left in place (we did not create it).
    expect(fs.existsSync(dest)).toBe(true);
  });

  it('refuses (structured failure, no writes) a name with a newline / injected key', async () => {
    const dest = path.join(tmpDir, 'Injected');
    const result = await createProject({
      projectPath: dest,
      name: 'X"\nrun/main_scene="res://evil.tscn',
    });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;
    expect(result.errorMessage).toContain('Invalid project name');
    // Nothing was scaffolded — the injection never reached project.godot.
    expect(fs.existsSync(path.join(dest, 'project.godot'))).toBe(false);
    expect(result.filesWritten).toEqual([]);
  });

  it('refuses (structured failure, no writes) a name with path separators / traversal', async () => {
    const dest = path.join(tmpDir, 'Traversal');
    const result = await createProject({ projectPath: dest, name: '../../evil', dotnet: true });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;
    expect(result.errorMessage).toContain('Invalid project name');
    // No csproj was written outside (or inside) the target directory.
    expect(fs.existsSync(dest)).toBe(false);
    expect(result.filesWritten).toEqual([]);
  });

  it('refuses a name containing a double-quote (reserved char)', async () => {
    // `"` is rejected at the createProject boundary; the renderProjectGodot
    // escaping (unit-tested separately) is the defense-in-depth second layer.
    const dest = path.join(tmpDir, 'EscapeMe');
    const result = await createProject({ projectPath: dest, name: 'Quote"Name' });
    expect(result.kind).toBe('failure');
    if (result.kind !== 'failure') return;
    expect(result.errorMessage).toContain('Invalid project name');
  });

  it('warns when it overwrites a pre-existing non-marker file', async () => {
    // A stray icon.svg in a marker-free directory must be reported, not silently clobbered.
    fs.mkdirSync(tmpDir, { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'icon.svg'), '<svg/>');
    const result = await createProject({ projectPath: tmpDir, name: 'Reuse' });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.warnings.some((w) => w.includes('icon.svg'))).toBe(true);
  });
});
