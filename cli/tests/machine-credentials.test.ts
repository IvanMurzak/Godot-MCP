import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
  MACHINE_STORE_DIR_NAME,
  MACHINE_STORE_DIR_ENV,
  getMachineStoreDir,
  getMachineCredentialsPath,
  machineCredentialsExist,
  readMachineCredentials,
  writeMachineCredentials,
  readMachineAccessToken,
  deleteMachineCredentials,
} from '../src/utils/machine-credentials.js';

const isWindows = process.platform === 'win32';

describe('machine credential store — location', () => {
  it('defaults to ~/.ai-game-dev/credentials.json', () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    delete process.env[MACHINE_STORE_DIR_ENV];
    try {
      expect(getMachineStoreDir()).toBe(path.join(os.homedir(), MACHINE_STORE_DIR_NAME));
      expect(getMachineCredentialsPath()).toBe(
        path.join(os.homedir(), MACHINE_STORE_DIR_NAME, 'credentials.json'),
      );
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });

  it('honors the AI_GAME_DEV_CREDENTIALS_DIR env override', () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    process.env[MACHINE_STORE_DIR_ENV] = path.join('some', 'override', 'dir');
    try {
      expect(getMachineStoreDir()).toBe(path.join('some', 'override', 'dir'));
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });

  it('an explicit baseDir argument overrides both env and default', () => {
    const prev = process.env[MACHINE_STORE_DIR_ENV];
    process.env[MACHINE_STORE_DIR_ENV] = path.join('env', 'dir');
    try {
      expect(getMachineStoreDir('/explicit/base')).toBe('/explicit/base');
      expect(getMachineCredentialsPath('/explicit/base')).toBe(path.join('/explicit/base', 'credentials.json'));
    } finally {
      if (prev === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
      else process.env[MACHINE_STORE_DIR_ENV] = prev;
    }
  });
});

describe('machine credential store — read/write', () => {
  let storeDir: string;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-machine-'));
  });
  afterEach(() => {
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it('returns null when no credential file exists', () => {
    expect(readMachineCredentials(storeDir)).toBeNull();
    expect(machineCredentialsExist(storeDir)).toBe(false);
    expect(readMachineAccessToken(storeDir)).toBeUndefined();
  });

  it('round-trips written credentials (DPAPI on Windows / plaintext on POSIX)', () => {
    writeMachineCredentials(
      { accessToken: 'acc-123', serverTarget: 'https://ai-game.dev', subject: 'user-1' },
      storeDir,
    );
    expect(machineCredentialsExist(storeDir)).toBe(true);
    const creds = readMachineCredentials(storeDir);
    expect(creds).toEqual({
      version: 1,
      accessToken: 'acc-123',
      serverTarget: 'https://ai-game.dev',
      subject: 'user-1',
    });
    expect(readMachineAccessToken(storeDir)).toBe('acc-123');
  });

  it('always stamps version=1 and omits absent optional fields', () => {
    writeMachineCredentials({ accessToken: 'only-token' }, storeDir);
    const creds = readMachineCredentials(storeDir);
    expect(creds).toEqual({ version: 1, accessToken: 'only-token' });
    expect(creds).not.toHaveProperty('refreshToken');
    expect(creds).not.toHaveProperty('expiresAt');
    expect(creds).not.toHaveProperty('serverTarget');
  });

  it('overwrites an existing credential', () => {
    writeMachineCredentials({ accessToken: 'first', serverTarget: 'https://a.test' }, storeDir);
    writeMachineCredentials({ accessToken: 'second', serverTarget: 'https://b.test' }, storeDir);
    const creds = readMachineCredentials(storeDir);
    expect(creds?.accessToken).toBe('second');
    expect(creds?.serverTarget).toBe('https://b.test');
  });

  it('readMachineAccessToken ignores an empty/whitespace token', () => {
    writeMachineCredentials({ accessToken: '   ' }, storeDir);
    expect(readMachineAccessToken(storeDir)).toBeUndefined();
  });

  it('deletes the credential (sign-out); delete is a no-op when absent', () => {
    writeMachineCredentials({ accessToken: 'acc' }, storeDir);
    expect(machineCredentialsExist(storeDir)).toBe(true);
    deleteMachineCredentials(storeDir);
    expect(machineCredentialsExist(storeDir)).toBe(false);
    // No throw on a second delete.
    expect(() => deleteMachineCredentials(storeDir)).not.toThrow();
  });
});

describe('machine credential store — at-rest protection', () => {
  let storeDir: string;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-machine-atrest-'));
  });
  afterEach(() => {
    fs.rmSync(storeDir, { recursive: true, force: true });
  });

  it.skipIf(isWindows)('POSIX: writes plaintext JSON with 0600 file perms inside a 0700 dir', () => {
    writeMachineCredentials({ accessToken: 'plain-secret', serverTarget: 'https://ai-game.dev' }, storeDir);
    const credPath = getMachineCredentialsPath(storeDir);

    const onDisk = fs.readFileSync(credPath, 'utf8');
    expect(onDisk).toContain('plain-secret');
    expect(JSON.parse(onDisk)).toEqual({
      version: 1,
      accessToken: 'plain-secret',
      serverTarget: 'https://ai-game.dev',
    });

    expect(fs.statSync(credPath).mode & 0o777).toBe(0o600);
    expect(fs.statSync(storeDir).mode & 0o777).toBe(0o700);
  });

  it.runIf(isWindows)('Windows: on-disk bytes are DPAPI-encrypted (never plaintext), still decryptable', () => {
    writeMachineCredentials({ accessToken: 'dpapi-secret', serverTarget: 'https://ai-game.dev' }, storeDir);
    const credPath = getMachineCredentialsPath(storeDir);

    const raw = fs.readFileSync(credPath);
    // The plaintext token must NOT appear anywhere in the encrypted blob.
    expect(raw.toString('utf8')).not.toContain('dpapi-secret');
    expect(raw.toString('latin1')).not.toContain('dpapi-secret');
    // And the bytes are not parseable as the JSON document.
    expect(() => JSON.parse(raw.toString('utf8'))).toThrow();

    // But decryption via the store recovers the exact document.
    expect(readMachineCredentials(storeDir)).toEqual({
      version: 1,
      accessToken: 'dpapi-secret',
      serverTarget: 'https://ai-game.dev',
    });
  });
});
