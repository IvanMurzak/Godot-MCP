import { describe, it, expect } from 'vitest';
import { spawn, type ChildProcess } from 'child_process';
import {
  isProcessAlive,
  sendGracefulShutdown,
  sendForceKill,
  waitForExit,
} from '../src/utils/godot-shutdown.js';

/**
 * Spawn a long-lived child process we can probe / signal. On every platform a
 * sleeping node process is the most portable victim. The caller is responsible
 * for force-killing it in a `finally`.
 */
function spawnSleeper(): ChildProcess {
  // A node process that idles for a while; we control its lifecycle in tests.
  return spawn(process.execPath, ['-e', 'setTimeout(() => {}, 60000)'], {
    stdio: 'ignore',
  });
}

async function untilSpawned(child: ChildProcess): Promise<number> {
  if (child.pid) return child.pid;
  await new Promise<void>((resolve, reject) => {
    child.once('spawn', () => resolve());
    child.once('error', reject);
  });
  return child.pid as number;
}

describe('godot-shutdown — isProcessAlive', () => {
  it('returns true for a live process and false after it exits', async () => {
    const child = spawnSleeper();
    try {
      const pid = await untilSpawned(child);
      expect(isProcessAlive(pid)).toBe(true);

      child.kill('SIGKILL');
      const gone = await waitForExit(pid, 5000);
      expect(gone).toBe(true);
      expect(isProcessAlive(pid)).toBe(false);
    } finally {
      try { child.kill('SIGKILL'); } catch { /* noop */ }
    }
  });

  it('returns false for a clearly-dead PID', () => {
    // PID 0 / a very large unused PID should not be alive.
    expect(isProcessAlive(2147483646)).toBe(false);
  });
});

describe('godot-shutdown — sendGracefulShutdown / sendForceKill', () => {
  it('delivers a graceful-shutdown request to a live process', async () => {
    const child = spawnSleeper();
    try {
      const pid = await untilSpawned(child);
      const sent = sendGracefulShutdown(pid);
      if (process.platform === 'win32') {
        // `taskkill` WITHOUT /F posts WM_CLOSE, which a windowless console
        // process (our node sleeper) cannot receive — taskkill then reports a
        // non-zero exit, surfaced as `sent === false`. Either outcome is valid;
        // what matters is the call does not throw and returns a boolean.
        expect(typeof sent).toBe('boolean');
      } else {
        // POSIX SIGTERM reaches any process and ends our node sleeper cleanly.
        expect(sent).toBe(true);
        const exited = await waitForExit(pid, 8000);
        expect(exited).toBe(true);
      }
    } finally {
      try { child.kill('SIGKILL'); } catch { /* noop */ }
    }
  });

  it('force-kills a live process (cross-platform)', async () => {
    const child = spawnSleeper();
    try {
      const pid = await untilSpawned(child);
      const killed = sendForceKill(pid);
      expect(killed).toBe(true);
      const exited = await waitForExit(pid, 5000);
      expect(exited).toBe(true);
      expect(isProcessAlive(pid)).toBe(false);
    } finally {
      try { child.kill('SIGKILL'); } catch { /* noop */ }
    }
  });

  it('returns false when trying to signal a non-existent process (posix)', () => {
    // On win32 taskkill against a missing PID also fails; on posix process.kill
    // throws ESRCH. Both are caught and surface as `false`.
    const result = sendGracefulShutdown(2147483646);
    expect(result).toBe(false);
  });
});

describe('godot-shutdown — waitForExit', () => {
  it('resolves true immediately for an already-dead process', async () => {
    const exited = await waitForExit(2147483646, 1000);
    expect(exited).toBe(true);
  });

  it('resolves false when a live process outlives the timeout', async () => {
    const child = spawnSleeper();
    try {
      const pid = await untilSpawned(child);
      const exited = await waitForExit(pid, 600);
      expect(exited).toBe(false);
    } finally {
      try { child.kill('SIGKILL'); } catch { /* noop */ }
    }
  });
});
