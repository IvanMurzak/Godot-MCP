// One headless Godot process with a hard timeout: output captured, the whole process tree killed on timeout (test
// runners and imports are known to hang). Library-safe: no console output, no `process.exit`, never throws.

import * as fs from 'fs';
import * as path from 'path';
import { spawn } from 'child_process';
import { killProcessTree } from '@baizor/gamedev-cli-core';
import { editorLaunchEnv } from '../utils/godot-editor.js';
import { GODOT_BANNER_RE, stripAnsi } from './godot-log.js';

export interface GodotRunOptions {
  binary: string;
  args: string[];
  cwd: string;
  timeoutMs: number;
  /** Extra environment (on top of `process.env` and the .NET workaround every editor launch gets). */
  env?: Record<string, string>;
  /**
   * Also pass `--log-file <logFile>`; its content is used when the process printed no Godot banner to stdout
   * (a GUI-subsystem binary on Windows may print nothing to a pipe).
   */
  logFile?: string;
  /** Kill the tree and report `interrupted` on SIGINT / SIGTERM (CLI use; a library host keeps its own handling). */
  killOnSignal?: boolean;
  /** Each output line as it arrives (ANSI stripped). */
  onLine?: (line: string) => void;
  /** Tests. */
  spawnImpl?: typeof spawn;
  killTreeImpl?: (pid: number) => void | Promise<unknown>;
}

export interface GodotRunResult {
  /** Process exit code; null when killed or it could not start. */
  exitCode: number | null;
  timedOut: boolean;
  /** Combined stdout + stderr (or the log file), ANSI stripped. */
  output: string;
  durationMs: number;
  /** The process could not be started. */
  spawnError?: string;
  /** This CLI got SIGINT / SIGTERM while the process ran; its tree was killed. */
  interrupted?: boolean;
}

export function runGodot(options: GodotRunOptions): Promise<GodotRunResult> {
  const started = Date.now();
  // Before the args: anything after a `--` belongs to the script (OS.get_cmdline_user_args()).
  const args = options.logFile ? ['--log-file', options.logFile, ...options.args] : options.args;
  if (options.logFile) {
    try {
      fs.mkdirSync(path.dirname(options.logFile), { recursive: true });
      fs.rmSync(options.logFile, { force: true });
    } catch {
      // the log file is only a fallback
    }
  }

  return new Promise((resolve) => {
    let output = '';
    let pending = '';
    let settled = false;
    let timedOut = false;
    let interrupted = false;
    let detachSignals = (): void => undefined;
    const finish = (exitCode: number | null, spawnError?: string): void => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      detachSignals();
      if (pending) options.onLine?.(stripAnsi(pending));
      let text = stripAnsi(output);
      if (!GODOT_BANNER_RE.test(text) && options.logFile) {
        try {
          const fromFile = stripAnsi(fs.readFileSync(options.logFile, 'utf-8'));
          if (fromFile.trim().length > 0) text = fromFile;
        } catch {
          // no log file either
        }
      }
      resolve({ exitCode, timedOut, output: text, durationMs: Date.now() - started, ...(spawnError ? { spawnError } : {}), ...(interrupted ? { interrupted } : {}) });
    };

    let child: ReturnType<typeof spawn>;
    try {
      child = (options.spawnImpl ?? spawn)(options.binary, args, {
        cwd: options.cwd,
        env: editorLaunchEnv(options.env),
        stdio: ['ignore', 'pipe', 'pipe'],
        windowsHide: true,
        // POSIX: own process group so a timeout kills the whole tree.
        detached: process.platform !== 'win32',
      });
    } catch (err) {
      resolve({ exitCode: null, timedOut: false, output: '', durationMs: Date.now() - started, spawnError: err instanceof Error ? err.message : String(err) });
      return;
    }

    const killTree = (): void => {
      const killed = child.pid !== undefined ? (options.killTreeImpl ?? killProcessTree)(child.pid) : undefined;
      // A killed tree closes its pipes; resolve anyway should a grandchild keep them open — counted from when the
      // kill is done, so the caller never exits while taskkill still runs.
      void Promise.resolve(killed)
        .catch(() => undefined)
        .then(() => setTimeout(() => finish(null), 5_000).unref());
    };
    const timer = setTimeout(() => {
      timedOut = true;
      killTree();
    }, options.timeoutMs);
    // Ctrl+C / SIGTERM: the POSIX child runs in its own process group and never sees the signal, and on Windows
    // Godot's children survive it; kill the tree and let the caller report the interruption.
    const onSignal = (): void => {
      interrupted = true;
      killTree();
    };
    if (options.killOnSignal) {
      process.once('SIGINT', onSignal);
      process.once('SIGTERM', onSignal);
      detachSignals = () => {
        process.removeListener('SIGINT', onSignal);
        process.removeListener('SIGTERM', onSignal);
      };
    }

    const onData = (chunk: Buffer | string): void => {
      const text = chunk.toString();
      output += text;
      if (!options.onLine) return;
      pending += text;
      const parts = pending.split(/\r?\n/);
      pending = parts.pop() ?? '';
      for (const line of parts) options.onLine(stripAnsi(line));
    };
    child.stdout?.on('data', onData);
    child.stderr?.on('data', onData);
    child.on('error', (err) => finish(null, err.message));
    child.on('close', (code) => finish(timedOut || interrupted ? null : code));
    // A grandchild that inherited the pipes (an MSBuild node, the C# compiler server) can keep them open long after
    // Godot exited: give the output 2 s to drain, then finish on the exit itself.
    child.on('exit', (code) => {
      setTimeout(() => finish(timedOut || interrupted ? null : code), 2_000).unref();
    });
  });
}
