import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  parseTimeoutSeconds,
  isGodotProjectRoot,
  resolveCloseProjectPath,
  resolveEditorPid,
} from '../src/commands/close.js';
import { runCliAsync } from './helpers/cli.js';

describe('close — parseTimeoutSeconds', () => {
  it('defaults to 30 when undefined', () => {
    expect(parseTimeoutSeconds(undefined)).toBe(30);
  });

  it('parses a positive integer string', () => {
    expect(parseTimeoutSeconds('45')).toBe(45);
  });

  it('rejects zero', () => {
    expect(parseTimeoutSeconds('0')).toBeNull();
  });

  it('rejects negatives', () => {
    expect(parseTimeoutSeconds('-5')).toBeNull();
  });

  it('rejects non-integers', () => {
    expect(parseTimeoutSeconds('3.5')).toBeNull();
    expect(parseTimeoutSeconds('abc')).toBeNull();
    expect(parseTimeoutSeconds('')).toBeNull();
  });
});

describe('close — isGodotProjectRoot', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-close-unit-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('is true when project.godot exists', () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    expect(isGodotProjectRoot(tmpDir)).toBe(true);
  });

  it('is false when project.godot is absent', () => {
    expect(isGodotProjectRoot(tmpDir)).toBe(false);
  });
});

describe('close — resolveCloseProjectPath', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-close-resolve-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('resolves an explicit path to its canonical absolute form', () => {
    const resolved = resolveCloseProjectPath(tmpDir, process.cwd());
    expect(path.isAbsolute(resolved)).toBe(true);
    expect(fs.realpathSync(resolved)).toBe(fs.realpathSync(tmpDir));
  });

  it('falls back to cwd when no positional path is given', () => {
    const resolved = resolveCloseProjectPath(undefined, tmpDir);
    expect(fs.realpathSync(resolved)).toBe(fs.realpathSync(tmpDir));
  });

  it('returns a resolved path even when the target does not exist', () => {
    const ghost = path.join(tmpDir, 'does-not-exist-xyz');
    const resolved = resolveCloseProjectPath(ghost, process.cwd());
    expect(resolved).toBe(path.resolve(ghost));
  });
});

describe('close — resolveEditorPid', () => {
  it('returns null when no Godot editor is running for the project', () => {
    // A random temp dir is never the --path of a live Godot process.
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-close-pid-'));
    try {
      expect(resolveEditorPid(tmp)).toBeNull();
    } finally {
      fs.rmSync(tmp, { recursive: true, force: true });
    }
  });
});

describe('close — CLI smoke', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-close-smoke-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['close', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('--timeout');
    expect(stdout).toContain('--force');
  });

  it('exits 1 when the path does not exist', async () => {
    const ghost = path.join(tmpDir, 'nope');
    const { stdout, exitCode } = await runCliAsync(['close', ghost]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Project path does not exist');
  });

  it('exits 1 when the path is not a Godot project root', async () => {
    const { stdout, exitCode } = await runCliAsync(['close', tmpDir]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Not a Godot project root');
  });

  it('exits 1 for an invalid --timeout value', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    const { stdout, exitCode } = await runCliAsync(['close', tmpDir, '--timeout', 'abc']);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Invalid --timeout value');
  });

  it('exits 0 with a friendly message when no editor is running for the project', async () => {
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), 'config_version=5\n');
    const { stdout, exitCode } = await runCliAsync(['close', tmpDir]);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('no running editor for project');
  });
});
