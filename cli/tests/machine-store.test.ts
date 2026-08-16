import { describe, it, expect } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import {
  MACHINE_STORE_DIR_ENV,
  resolveMachineStoreDir,
  openMachineStore,
  getMachineCredentialsPath,
  resolveProjectStoreDir,
  ensureProjectStoreGitignored,
  hasUsableFamily,
} from '../src/utils/machine-store.js';

/**
 * The CLI ships NO store implementation of its own any more (unified-machine-auth 06 D7:
 * "CLI-local store copies … deleted; cli-core is the only TS implementation") — this file covers
 * only the thin resolution shim around `@baizor/gamedev-cli-core`.
 */
describe('machine-store shim', () => {
  it('defaults the store to ~/.ai-game-dev', () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    delete process.env[MACHINE_STORE_DIR_ENV];
    try {
      expect(resolveMachineStoreDir()).toBe(path.join(os.homedir(), '.ai-game-dev'));
      expect(getMachineCredentialsPath()).toBe(path.join(os.homedir(), '.ai-game-dev', 'credentials.json'));
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });

  it('honors the AI_GAME_DEV_CREDENTIALS_DIR env override', () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    process.env[MACHINE_STORE_DIR_ENV] = path.join('some', 'override', 'dir');
    try {
      expect(resolveMachineStoreDir()).toBe(path.join('some', 'override', 'dir'));
      expect(openMachineStore().baseDirectory).toBe(path.join('some', 'override', 'dir'));
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });

  it('an explicit base-dir override wins over the env override', () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    process.env[MACHINE_STORE_DIR_ENV] = path.join('env', 'dir');
    try {
      expect(resolveMachineStoreDir(path.join('explicit', 'dir'))).toBe(path.join('explicit', 'dir'));
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });

  it('the per-project store lives at <project>/.ai-game-dev', () => {
    expect(resolveProjectStoreDir(path.join('proj', 'root'))).toBe(path.join('proj', 'root', '.ai-game-dev'));
  });

  it('ensureProjectStoreGitignored covers the credential + lock files, idempotently', () => {
    const projectDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-store-gi-'));
    try {
      ensureProjectStoreGitignored(projectDir);
      ensureProjectStoreGitignored(projectDir); // idempotent — no duplicate entries
      const gitignorePath = path.join(projectDir, '.ai-game-dev', '.gitignore');
      const lines = fs
        .readFileSync(gitignorePath, 'utf-8')
        .split(/\r?\n/)
        .filter((l) => l.trim().length > 0);
      expect(lines).toEqual(['credentials.json', 'credentials.lock', 'credentials.lock.takeover']);
    } finally {
      fs.rmSync(projectDir, { recursive: true, force: true });
    }
  });

  it('hasUsableFamily sees v1 documents (families.legacy view) and rejects empty ones', () => {
    expect(hasUsableFamily(null)).toBe(false);
    expect(hasUsableFamily({})).toBe(false);
    expect(hasUsableFamily({ accessToken: '   ' })).toBe(false);
    expect(hasUsableFamily({ accessToken: 'v1-tok' })).toBe(true); // v1 doc reads as families.legacy
    expect(hasUsableFamily({ version: 2, families: { plugin: { accessToken: 'v2-tok' } } })).toBe(true);
    expect(hasUsableFamily({ version: 2, families: { plugin: { refreshToken: 'only-refresh' } } })).toBe(false);
  });
});
