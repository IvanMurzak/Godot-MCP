import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  fetchLatestVersion,
  formatUpdateAvailable,
  isRunningViaNpx,
} from '../src/utils/update-check.js';

describe('update-check — fetchLatestVersion', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns the version field from a 200 registry response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ version: '9.9.9' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    await expect(fetchLatestVersion()).resolves.toBe('9.9.9');
  });

  it('throws when the registry returns a non-OK status', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('', { status: 503 }));
    await expect(fetchLatestVersion()).rejects.toThrow(/503/);
  });

  it('throws when the response body has no version field', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({}), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    await expect(fetchLatestVersion()).rejects.toThrow(/No version field/);
  });
});

describe('update-check — formatUpdateAvailable', () => {
  it('mentions both the current and latest versions', () => {
    const msg = formatUpdateAvailable('1.0.0', '2.0.0');
    expect(msg).toContain('1.0.0');
    expect(msg).toContain('2.0.0');
    expect(msg).toContain('Update available');
  });
});

describe('update-check — isRunningViaNpx', () => {
  const ORIG_EXECPATH = process.env['npm_execpath'];
  const ORIG_COMMAND = process.env['npm_command'];

  afterEach(() => {
    if (ORIG_EXECPATH === undefined) delete process.env['npm_execpath'];
    else process.env['npm_execpath'] = ORIG_EXECPATH;
    if (ORIG_COMMAND === undefined) delete process.env['npm_command'];
    else process.env['npm_command'] = ORIG_COMMAND;
  });

  it('is true when npm_command is "exec"', () => {
    delete process.env['npm_execpath'];
    process.env['npm_command'] = 'exec';
    expect(isRunningViaNpx()).toBe(true);
  });

  it('is true when npm_execpath references npx', () => {
    process.env['npm_execpath'] = '/usr/local/lib/node_modules/npm/bin/npx-cli.js';
    delete process.env['npm_command'];
    expect(isRunningViaNpx()).toBe(true);
  });

  it('is false for a normal global install invocation', () => {
    process.env['npm_execpath'] = '/usr/local/lib/node_modules/npm/bin/npm-cli.js';
    process.env['npm_command'] = 'run-script';
    expect(isRunningViaNpx()).toBe(false);
  });

  it('is false when neither env var is set', () => {
    delete process.env['npm_execpath'];
    delete process.env['npm_command'];
    expect(isRunningViaNpx()).toBe(false);
  });
});
