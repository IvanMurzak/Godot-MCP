import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';

describe('create-project — CLI smoke (--dotnet + name derivation)', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-create-cli-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('scaffolds a C# (.NET) project with a csproj when --dotnet is given', async () => {
    const dest = path.join(tmpDir, 'CsGame');
    const { stdout, exitCode } = await runCliAsync([
      'create-project', dest, '--name', 'CsGame', '--dotnet',
    ]);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('C# (.NET)');
    expect(fs.existsSync(path.join(dest, 'project.godot'))).toBe(true);
    expect(fs.existsSync(path.join(dest, 'CsGame.csproj'))).toBe(true);
    expect(fs.existsSync(path.join(dest, 'icon.svg'))).toBe(true);
  });

  it('derives the project name from the target folder when --name is omitted', async () => {
    const dest = path.join(tmpDir, 'DerivedName');
    const { exitCode } = await runCliAsync(['create-project', dest]);
    expect(exitCode).toBe(0);
    const projectGodot = fs.readFileSync(path.join(dest, 'project.godot'), 'utf-8');
    expect(projectGodot).toContain('DerivedName');
  });

  it('refuses to scaffold over an existing Unity project (structured failure)', async () => {
    fs.mkdirSync(path.join(tmpDir, 'ProjectSettings'), { recursive: true });
    fs.writeFileSync(path.join(tmpDir, 'ProjectSettings', 'ProjectVersion.txt'), 'm_EditorVersion: 6000.0.0f1\n');
    const { stdout, exitCode } = await runCliAsync(['create-project', tmpDir]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Refusing to scaffold');
  });
});
