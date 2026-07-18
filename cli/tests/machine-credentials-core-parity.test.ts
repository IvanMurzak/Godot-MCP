import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { writeMachineCredentials, readMachineCredentials } from '../src/utils/machine-credentials.js';
import { MachineCredentialStore } from '@baizor/gamedev-cli-core';

/**
 * The machine credential store is the shared CROSS-TOOL on-disk contract (`~/.ai-game-dev/
 * credentials.json`): `godot-cli login` writes it and the Godot addon (the C# `MachineCredentialStore`)
 * reads it — and vice-versa. This parity gate proves the CLI's store and the cli-core
 * `MachineCredentialStore` are byte-compatible — the single-source-of-truth guarantee — by
 * round-tripping a credential ACROSS the two implementations in both directions (POSIX plaintext /
 * Windows DPAPI, both `CurrentUser` scope, so the blobs are cross-readable on either OS).
 */
describe('machine-credentials — cli-core interop parity', () => {
  let dir: string;
  beforeEach(() => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-mc-parity-'));
  });
  afterEach(() => {
    fs.rmSync(dir, { recursive: true, force: true });
  });

  const CREDS = {
    accessToken: 'acc.jwt',
    refreshToken: 'refresh.jwt',
    expiresAt: '2030-01-01T00:00:00.000Z',
    serverTarget: 'https://ai-game.dev',
    subject: 'user-42',
  } as const;

  it('a credential the CLI store writes is read back identically by the cli-core store', () => {
    writeMachineCredentials({ ...CREDS }, dir);
    const viaCore = new MachineCredentialStore(dir).read();
    expect(viaCore).toMatchObject({ version: 1, ...CREDS });
  });

  it('a credential the cli-core store writes is read back identically by the CLI store', () => {
    new MachineCredentialStore(dir).write({ ...CREDS });
    const viaCli = readMachineCredentials(dir);
    expect(viaCli).toMatchObject({ version: 1, ...CREDS });
  });
});
