import { execFileSync } from 'child_process';
import { platform } from 'os';

export type SupportedPlatform = 'win32' | 'darwin' | 'linux';

/** True when the process with the given PID is currently alive. */
export function isProcessAlive(pid: number, os: SupportedPlatform = platform() as SupportedPlatform): boolean {
  try {
    if (os === 'win32') {
      const out = execFileSync('tasklist', ['/FI', `PID eq ${pid}`, '/NH'], {
        encoding: 'utf-8',
        timeout: 5000,
      });
      return out.includes(String(pid));
    }
    // POSIX: signal 0 probes existence without delivering a signal.
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

/**
 * Send a graceful-shutdown request. On POSIX this is SIGTERM; on Windows we use
 * `taskkill` WITHOUT `/F` (a polite close request the OS routes to the process).
 * Returns true when the signal was delivered without error.
 */
export function sendGracefulShutdown(
  pid: number,
  os: SupportedPlatform = platform() as SupportedPlatform,
): boolean {
  try {
    if (os === 'win32') {
      execFileSync('taskkill', ['/PID', String(pid)], { encoding: 'utf-8', timeout: 5000, stdio: 'pipe' });
      return true;
    }
    process.kill(pid, 'SIGTERM');
    return true;
  } catch {
    return false;
  }
}

/** Hard-kill the process. SIGKILL on POSIX; `taskkill /F` on Windows. */
export function sendForceKill(
  pid: number,
  os: SupportedPlatform = platform() as SupportedPlatform,
): boolean {
  try {
    if (os === 'win32') {
      execFileSync('taskkill', ['/PID', String(pid), '/F'], { encoding: 'utf-8', timeout: 5000, stdio: 'pipe' });
      return true;
    }
    process.kill(pid, 'SIGKILL');
    return true;
  } catch {
    return false;
  }
}

/**
 * Poll for the process to exit, up to `timeoutMs`. Resolves true when the
 * process is gone, false on timeout.
 */
export async function waitForExit(
  pid: number,
  timeoutMs: number,
  os: SupportedPlatform = platform() as SupportedPlatform,
): Promise<boolean> {
  const start = Date.now();
  const interval = 250;
  while (Date.now() - start < timeoutMs) {
    if (!isProcessAlive(pid, os)) return true;
    await new Promise((resolve) => setTimeout(resolve, interval));
  }
  return !isProcessAlive(pid, os);
}
