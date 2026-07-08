import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  getCredentialsPath,
  readCredentials,
  writeCredentials,
  readCloudToken,
} from '../src/utils/credentials.js';

describe('credentials', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cred-'));
  });
  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('credentials path is project-local .godot-mcp/credentials.json', () => {
    expect(getCredentialsPath(tmpDir)).toBe(path.join(tmpDir, '.godot-mcp', 'credentials.json'));
  });

  it('returns null when no credentials file exists', () => {
    expect(readCredentials(tmpDir)).toBeNull();
  });

  it('round-trips written credentials', () => {
    writeCredentials(tmpDir, { cloudToken: 'tok-1', cloudBaseUrl: 'https://ai-game.dev' });
    expect(readCredentials(tmpDir)).toEqual({ cloudToken: 'tok-1', cloudBaseUrl: 'https://ai-game.dev' });
  });

  it('readCloudToken returns the persisted token', () => {
    writeCredentials(tmpDir, { cloudToken: 'tok-2' });
    expect(readCloudToken(tmpDir)).toBe('tok-2');
  });

  it('readCloudToken returns undefined when absent, empty, or malformed', () => {
    expect(readCloudToken(tmpDir)).toBeUndefined();

    writeCredentials(tmpDir, { cloudToken: '   ' });
    expect(readCloudToken(tmpDir)).toBeUndefined();

    fs.writeFileSync(getCredentialsPath(tmpDir), '{ not json');
    expect(readCloudToken(tmpDir)).toBeUndefined();
  });

  it('readCredentials throws on malformed JSON', () => {
    fs.mkdirSync(path.join(tmpDir, '.godot-mcp'), { recursive: true });
    fs.writeFileSync(getCredentialsPath(tmpDir), '{ not json');
    expect(() => readCredentials(tmpDir)).toThrow(/Malformed JSON/);
  });

  it('writeCredentials git-ignores credentials.json (idempotently, without clobbering)', () => {
    writeCredentials(tmpDir, { cloudToken: 'tok' });
    const gitignorePath = path.join(tmpDir, '.godot-mcp', '.gitignore');
    expect(fs.existsSync(gitignorePath)).toBe(true);
    expect(fs.readFileSync(gitignorePath, 'utf-8')).toContain('credentials.json');

    // A second write must not duplicate the entry.
    writeCredentials(tmpDir, { cloudToken: 'tok2' });
    const occurrences = fs
      .readFileSync(gitignorePath, 'utf-8')
      .split(/\r?\n/)
      .filter((l) => l.trim() === 'credentials.json').length;
    expect(occurrences).toBe(1);
  });

  it('writeCredentials preserves pre-existing .gitignore entries', () => {
    const dir = path.join(tmpDir, '.godot-mcp');
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, '.gitignore'), 'features-local.json\n');
    writeCredentials(tmpDir, { cloudToken: 'tok' });
    const content = fs.readFileSync(path.join(dir, '.gitignore'), 'utf-8');
    expect(content).toContain('features-local.json');
    expect(content).toContain('credentials.json');
  });
});
