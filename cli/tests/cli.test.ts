import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { execFileSync } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CLI_PATH = path.resolve(__dirname, '..', 'bin', 'godot-cli.js');

function runCli(args: string[], options?: { cwd?: string }): { stdout: string; exitCode: number } {
  try {
    const stdout = execFileSync('node', [CLI_PATH, ...args], {
      encoding: 'utf-8',
      timeout: 15000,
      cwd: options?.cwd,
    });
    return { stdout, exitCode: 0 };
  } catch (err: unknown) {
    const error = err as { stdout?: string; stderr?: string; status?: number };
    return {
      stdout: (error.stdout ?? '') + (error.stderr ?? ''),
      exitCode: error.status ?? 1,
    };
  }
}

describe('CLI integration', () => {
  describe('global options', () => {
    it('shows help with --help', () => {
      const { stdout, exitCode } = runCli(['--help']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('godot-cli');
      expect(stdout).toContain('open');
      expect(stdout).toContain('run-tool');
      expect(stdout).toContain('run-system-tool');
      expect(stdout).toContain('status');
      expect(stdout).toContain('wait-for-ready');
      expect(stdout).toContain('setup-mcp');
      expect(stdout).toContain('configure');
      expect(stdout).toContain('close');
      expect(stdout).toContain('install-plugin');
      expect(stdout).toContain('remove-plugin');
      expect(stdout).toContain('update');
      expect(stdout).toContain('create-project');
    });

    it('does NOT register a setup-skills command (scoped out of v1)', () => {
      const { stdout, exitCode } = runCli(['--help']);
      expect(exitCode).toBe(0);
      // The word "skills" may appear in prose, but there is no `setup-skills`
      // command listed in the Commands section.
      expect(stdout).not.toMatch(/^\s*setup-skills\b/m);
    });

    it('shows version with --version', () => {
      const { stdout, exitCode } = runCli(['--version']);
      expect(exitCode).toBe(0);
      expect(stdout.trim()).toMatch(/^\d+\.\d+\.\d+$/);
    });
  });

  describe('open', () => {
    it('shows help with --help including connection options', () => {
      const { stdout, exitCode } = runCli(['open', '--help']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('--path');
      expect(stdout).toContain('--editor-path');
      expect(stdout).toContain('--no-connect');
      expect(stdout).toContain('--url');
      expect(stdout).toContain('--token');
      expect(stdout).toContain('--auth');
      expect(stdout).toContain('--mode');
    });

    it('falls back to cwd and fails when cwd is not a Godot project', () => {
      const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-open-'));
      try {
        const { exitCode, stdout } = runCli(['open'], { cwd: tmp });
        expect(exitCode).toBe(1);
        expect(stdout).toContain('Current directory is not a Godot project');
      } finally {
        fs.rmSync(tmp, { recursive: true, force: true });
      }
    });
  });

  describe('run-tool', () => {
    it('shows help with --help', () => {
      const { stdout, exitCode } = runCli(['run-tool', '--help']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('--url');
      expect(stdout).toContain('--input');
      expect(stdout).toContain('--timeout');
    });
  });

  describe('configure', () => {
    let tmpDir: string;

    beforeEach(() => {
      tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-cfg-'));
    });

    afterEach(() => {
      fs.rmSync(tmpDir, { recursive: true, force: true });
    });

    it('shows help with --help', () => {
      const { stdout, exitCode } = runCli(['configure', '--help']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('--enable-tools');
      expect(stdout).toContain('--disable-tools');
      expect(stdout).toContain('--list');
    });

    it('creates default config and lists it', () => {
      const { stdout, exitCode } = runCli(['configure', '--path', tmpDir, '--list']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('Current configuration');
      // A fresh config writes the project-local features file.
      expect(fs.existsSync(path.join(tmpDir, '.godot-mcp', 'features.json'))).toBe(true);
    });

    it('enables and disables tools', () => {
      runCli(['configure', '--path', tmpDir, '--enable-tools', 'tool-a,tool-b']);

      const { stdout } = runCli(['configure', '--path', tmpDir, '--list']);
      expect(stdout).toContain('[enabled] tool-a');
      expect(stdout).toContain('[enabled] tool-b');

      runCli(['configure', '--path', tmpDir, '--disable-tools', 'tool-a']);
      const { stdout: stdout2 } = runCli(['configure', '--path', tmpDir, '--list']);
      expect(stdout2).toContain('[disabled] tool-a');
      expect(stdout2).toContain('[enabled] tool-b');
    });

    it('fails when project path does not exist', () => {
      const { exitCode, stdout } = runCli([
        'configure',
        '--path',
        path.join(tmpDir, 'does-not-exist-12345'),
        '--list',
      ]);
      expect(exitCode).toBe(1);
      expect(stdout).toContain('does not exist');
    });
  });

  describe('create-project', () => {
    let tmpDir: string;

    beforeEach(() => {
      tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-create-'));
    });

    afterEach(() => {
      fs.rmSync(tmpDir, { recursive: true, force: true });
    });

    it('shows help with --help', () => {
      const { stdout, exitCode } = runCli(['create-project', '--help']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('--path');
      expect(stdout).toContain('--name');
      expect(stdout).toContain('--dotnet');
    });

    it('scaffolds a project into the target directory', () => {
      const dest = path.join(tmpDir, 'NewGame');
      const { exitCode } = runCli(['create-project', '--path', dest, '--name', 'NewGame']);
      expect(exitCode).toBe(0);
      expect(fs.existsSync(path.join(dest, 'project.godot'))).toBe(true);
      expect(fs.existsSync(path.join(dest, 'icon.svg'))).toBe(true);
    });

    it('fails when the target already hosts a Godot project', () => {
      fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
      const { exitCode, stdout } = runCli(['create-project', '--path', tmpDir]);
      expect(exitCode).toBe(1);
      expect(stdout).toContain('Refusing to scaffold');
    });
  });

  describe('install-plugin / remove-plugin', () => {
    let tmpDir: string;

    beforeEach(() => {
      tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-plugin-'));
    });

    afterEach(() => {
      fs.rmSync(tmpDir, { recursive: true, force: true });
    });

    it('install-plugin shows help', () => {
      const { stdout, exitCode } = runCli(['install-plugin', '--help']);
      expect(exitCode).toBe(0);
      expect(stdout).toContain('--path');
    });

    it('fails when project.godot is missing', () => {
      const { exitCode, stdout } = runCli(['install-plugin', '--path', tmpDir]);
      expect(exitCode).toBe(1);
      expect(stdout).toContain('Not a valid Godot project');
    });

    it('enables then disables the addon in project.godot', () => {
      fs.writeFileSync(
        path.join(tmpDir, 'project.godot'),
        '; Engine configuration file.\nconfig_version=5\n\n[application]\n\nconfig/name="Test"\n',
      );

      const install = runCli(['install-plugin', '--path', tmpDir]);
      expect(install.exitCode).toBe(0);
      let text = fs.readFileSync(path.join(tmpDir, 'project.godot'), 'utf-8');
      expect(text).toContain('[editor_plugins]');
      expect(text).toContain('res://addons/godot_mcp/plugin.cfg');

      const remove = runCli(['remove-plugin', '--path', tmpDir]);
      expect(remove.exitCode).toBe(0);
      text = fs.readFileSync(path.join(tmpDir, 'project.godot'), 'utf-8');
      expect(text).not.toContain('res://addons/godot_mcp/plugin.cfg');
    });
  });
});
