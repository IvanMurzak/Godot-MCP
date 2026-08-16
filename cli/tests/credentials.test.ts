import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { getCredentialsPath, readCredentials, readCloudToken } from '../src/utils/credentials.js';

/**
 * The legacy `<project>/.godot-mcp/credentials.json` sink is READ-FALLBACK ONLY now
 * (unified-machine-auth 06 D7): the CLI never writes it — `login` commits to the cli-core
 * machine / per-project stores instead — so these tests seed the file with raw `fs` writes,
 * exactly the way a pre-existing legacy install left it on disk.
 */
function seedLegacyCredentials(projectPath: string, credentials: Record<string, unknown>): void {
  const credentialsPath = getCredentialsPath(projectPath);
  fs.mkdirSync(path.dirname(credentialsPath), { recursive: true });
  fs.writeFileSync(credentialsPath, JSON.stringify(credentials, null, 2) + '\n');
}

describe('credentials (legacy read-fallback)', () => {
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

  it('reads a pre-existing legacy credentials file', () => {
    seedLegacyCredentials(tmpDir, { cloudToken: 'tok-1', cloudBaseUrl: 'https://ai-game.dev' });
    expect(readCredentials(tmpDir)).toEqual({ cloudToken: 'tok-1', cloudBaseUrl: 'https://ai-game.dev' });
  });

  it('readCloudToken returns the persisted token', () => {
    seedLegacyCredentials(tmpDir, { cloudToken: 'tok-2' });
    expect(readCloudToken(tmpDir)).toBe('tok-2');
  });

  it('readCloudToken returns undefined when absent, empty, or malformed', () => {
    expect(readCloudToken(tmpDir)).toBeUndefined();

    seedLegacyCredentials(tmpDir, { cloudToken: '   ' });
    expect(readCloudToken(tmpDir)).toBeUndefined();

    fs.writeFileSync(getCredentialsPath(tmpDir), '{ not json');
    expect(readCloudToken(tmpDir)).toBeUndefined();
  });

  it('readCredentials throws on malformed JSON', () => {
    fs.mkdirSync(path.join(tmpDir, '.godot-mcp'), { recursive: true });
    fs.writeFileSync(getCredentialsPath(tmpDir), '{ not json');
    expect(() => readCredentials(tmpDir)).toThrow(/Malformed JSON/);
  });

  it('exposes NO write surface any more (a v2-era legacy-sink write must never ship — 06 D7)', async () => {
    const credentialsModule = await import('../src/utils/credentials.js');
    expect('writeCredentials' in credentialsModule).toBe(false);
  });
});
