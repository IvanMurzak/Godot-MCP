import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';

const MINIMAL_PROJECT = '; Engine configuration file.\nconfig_version=5\n\n[application]\n\nconfig/name="Test"\n';

describe('install-extension — CLI smoke (command wiring)', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-ext-cli-'));
    fs.writeFileSync(path.join(tmpDir, 'project.godot'), MINIMAL_PROJECT);
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('is registered and documented in --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('install-extension');
  });

  it('exits 1 with an honest empty-catalog message (no extension published yet)', async () => {
    const { stdout, exitCode } = await runCliAsync(['install-extension', 'anything', tmpDir]);
    // The shipped catalog is empty, so any id is unknown — and the message says so.
    expect(exitCode).toBe(1);
    expect(stdout).toContain('catalog is currently empty');
  });

  it('exits 1 for a non-Godot project directory', async () => {
    const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-ext-cli-empty-'));
    try {
      const { exitCode, stdout } = await runCliAsync(['install-extension', 'anything', empty]);
      expect(exitCode).toBe(1);
      expect(stdout).toContain('project.godot');
    } finally {
      fs.rmSync(empty, { recursive: true, force: true });
    }
  });
});
