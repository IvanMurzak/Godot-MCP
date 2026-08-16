import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { MachineCredentialStore } from '@baizor/gamedev-cli-core';
import { MACHINE_STORE_DIR_ENV } from '../src/utils/machine-store.js';

/**
 * Command-level regression for review f2 B1: an AGENT-ONLY store (the F1 `partial` state —
 * exchange failed after the agent family committed) must never make `godot-cli login` report
 * "Already authenticated". It must finish the derivation leg (no second device flow) — or fail
 * with an actionable message — because every command consumes the PLUGIN plane, so the old
 * `hasUsableFamily` gate wedged the CLI: `login` short-circuited while `run-tool`/`open` raised
 * `login required`, with an unnamed `--force` as the only exit.
 *
 * The login surface is mocked at its seams (`completePartialLogin` / `runCloudLogin`) so no
 * network or browser is touched; the REAL `classifyLoginState` gate decides the path.
 */
vi.mock('../src/utils/cloud-login.js', async (importOriginal) => {
  const original = await importOriginal<typeof import('../src/utils/cloud-login.js')>();
  return {
    ...original,
    completePartialLogin: vi.fn(async () => 'repaired-plugin-tok'),
    runCloudLogin: vi.fn(async () => 'fresh-plugin-tok'),
  };
});

import { completePartialLogin, runCloudLogin } from '../src/utils/cloud-login.js';
import { loginCommand } from '../src/commands/login.js';

const BASE = 'https://ai-game.dev'; // the command's default base URL

describe('login command — agent-only (partial) store is repaired, never "Already authenticated" (B1)', () => {
  let storeDir: string;
  let prevEnv: string | undefined;
  let logSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    storeDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-login-cmd-'));
    prevEnv = process.env[MACHINE_STORE_DIR_ENV];
    process.env[MACHINE_STORE_DIR_ENV] = storeDir;
    logSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    vi.mocked(completePartialLogin).mockClear();
    vi.mocked(runCloudLogin).mockClear();
  });

  afterEach(() => {
    if (prevEnv === undefined) delete process.env[MACHINE_STORE_DIR_ENV];
    else process.env[MACHINE_STORE_DIR_ENV] = prevEnv;
    fs.rmSync(storeDir, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  function loggedOutput(): string {
    return logSpy.mock.calls.map((call) => call.join(' ')).join('\n');
  }

  it('runs the derivation-repair leg for an agent-only store — no device flow, no "Already authenticated"', async () => {
    new MachineCredentialStore(storeDir).write({
      version: 2,
      serverTarget: BASE,
      subject: 'user-a',
      families: {
        agent: {
          accessToken: 'agent-tok',
          refreshToken: 'agent-refresh',
          clientId: 'godot-cli',
          scope: 'mcp:agent',
        },
      },
    });

    await loginCommand.parseAsync([], { from: 'user' });

    expect(completePartialLogin).toHaveBeenCalledTimes(1); // the repair leg ran…
    expect(runCloudLogin).not.toHaveBeenCalled(); // …not a second device flow
    expect(loggedOutput()).not.toContain('Already authenticated');
    expect(loggedOutput()).toContain('Authentication complete');
  });

  it('still short-circuits "Already authenticated" for a real plugin-plane credential', async () => {
    new MachineCredentialStore(storeDir).write({
      version: 2,
      serverTarget: BASE,
      families: {
        plugin: { accessToken: 'plugin-tok', clientId: 'godot-cli', scope: 'mcp:plugin' },
      },
      accessToken: 'plugin-tok',
    });

    await loginCommand.parseAsync([], { from: 'user' });

    expect(loggedOutput()).toContain('Already authenticated');
    expect(completePartialLogin).not.toHaveBeenCalled();
    expect(runCloudLogin).not.toHaveBeenCalled();
  });
});
