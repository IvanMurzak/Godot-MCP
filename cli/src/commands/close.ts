import { Command, Option } from 'commander';
import * as fs from 'fs';
import * as path from 'path';
import { platform as nodePlatform } from 'os';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { childProcessIds, findGodotProcess } from '../utils/godot-process.js';
import {
  isProcessAlive,
  sendGracefulShutdown,
  sendForceKill,
  waitForExit,
  type SupportedPlatform,
} from '../utils/godot-shutdown.js';
import { resolveSessionConnection } from '../utils/connection.js';
import { createGodotSession } from '../lib/session.js';

export interface CloseOptions {
  path?: string;
  timeout?: string;
  force?: boolean;
  ownedOnly?: boolean;
  url?: string;
  token?: string;
}

const DEFAULT_TIMEOUT_SECONDS = 30;

/** Resolve the project path argument to an absolute, canonical path. */
export function resolveCloseProjectPath(positionalPath: string | undefined, cwd: string): string {
  const explicit = positionalPath ?? cwd;
  let resolved = path.resolve(explicit);
  try {
    resolved = fs.realpathSync(resolved);
  } catch {
    // Path may not exist — let the caller decide what to do.
  }
  return resolved;
}

/** True when `<projectPath>/project.godot` exists. */
export function isGodotProjectRoot(projectPath: string): boolean {
  return fs.existsSync(path.join(projectPath, 'project.godot'));
}

/**
 * Parse a positive-integer `--timeout` value (seconds). Returns the parsed
 * value, or `null` when the input is not a valid positive integer. Exported for
 * unit tests.
 */
export function parseTimeoutSeconds(raw: string | undefined): number | null {
  if (raw === undefined) return DEFAULT_TIMEOUT_SECONDS;
  const parsed = Number(raw);
  if (!Number.isInteger(parsed) || parsed <= 0) return null;
  return parsed;
}

/** Resolve the editor PID for a given project path, or null when not running. */
export function resolveEditorPid(projectPath: string): number | null {
  return resolveEditorProcess(projectPath)?.pid ?? null;
}

/** The editor process of the project, and whether it is the `_console` wrapper (its child is the editor). */
function resolveEditorProcess(projectPath: string): { pid: number; consoleWrapper: boolean } | null {
  const proc = findGodotProcess(projectPath);
  if (!proc) return null;
  verbose(`Godot editor PID ${proc.pid} matched for project`);
  return { pid: proc.pid, consoleWrapper: isConsoleWrapper(proc.commandLine) };
}

/** Whether a command line runs Godot's `_console` build: a windowless wrapper whose child is the editor. */
export function isConsoleWrapper(commandLine: string): boolean {
  return /_console(\.exe)?\b/i.test(commandLine);
}

export const closeCommand = new Command('close')
  .description('Gracefully terminate the Godot editor instance running for a given project path')
  .argument('[path]', 'Path to the Godot project (defaults to current directory)')
  .option('--path <path>', 'Path to the Godot project (same as [path])')
  .option('--timeout <seconds>', `Polite-quit timeout in seconds (default: ${DEFAULT_TIMEOUT_SECONDS})`, String(DEFAULT_TIMEOUT_SECONDS))
  .option('--force', 'Hard-kill the editor if it does not exit within --timeout')
  .option('--owned-only', 'Close only an editor started by godot-cli (exec / open --headless); never a user editor')
  // Hidden fallback: a lease written by godot-cli 0.30.0 (cli-core 0.7.0) does not record the editor's server.
  .addOption(new Option('--url <url>', 'With --owned-only: the local MCP server of that editor, for a lease without one').hideHelp())
  .option('--token <token>', 'With --owned-only: bearer token (default: GODOT_MCP_TOKEN)')
  .action(async (positionalArg: string | undefined, options: CloseOptions) => {
    const positionalPath = positionalArg ?? options.path;
    const projectPath = resolveCloseProjectPath(positionalPath, process.cwd());
    verbose(`close invoked for project: ${projectPath}`);

    if (!fs.existsSync(projectPath)) {
      ui.error(`Project path does not exist: ${projectPath}`);
      process.exit(1);
    }

    if (!isGodotProjectRoot(projectPath)) {
      ui.error(`Not a Godot project root: ${projectPath}`);
      process.exit(1);
    }

    const timeoutSeconds = parseTimeoutSeconds(options.timeout);
    if (timeoutSeconds === null) {
      ui.error(`Invalid --timeout value: "${options.timeout}". Must be a positive integer (seconds).`);
      process.exit(1);
    }

    // The local port is derived from the path as typed (exec / open --headless do not resolve links either).
    const leasePath = path.resolve(positionalPath ?? process.cwd());
    // An editor godot-cli started (exec, run-tool, tools, open --headless) is windowless: ask its plugin to save and
    // quit through the lease instead of a window close it cannot receive. --owned-only stops there.
    const closedOwned = await closeOwned(leasePath, timeoutSeconds, options, options.ownedOnly === true);
    if (closedOwned || options.ownedOnly) return;

    const platform = nodePlatform() as SupportedPlatform;
    const editor = resolveEditorProcess(projectPath);
    const pid = editor?.pid ?? null;

    if (pid === null) {
      ui.success(`no running editor for project at ${projectPath}`);
      process.exit(0);
    }

    ui.heading('Closing Godot Editor');
    ui.label('Project', projectPath);
    ui.label('PID', String(pid));
    ui.label('Timeout', `${timeoutSeconds}s`);
    ui.label('Force', options.force ? 'yes' : 'no');
    ui.divider();

    const spinner = ui.startSpinner(`Sending polite-quit to PID ${pid}...`);
    // A console-subsystem editor (the `_console` build) or a windowless one cannot be asked politely on Windows.
    let sent = sendGracefulShutdown(pid, platform);
    // The `_console` wrapper has no window, but the editor it runs has: ask that one (the wrapper exits with it).
    if (!sent && editor!.consoleWrapper) {
      for (const child of childProcessIds(pid)) {
        const ok = sendGracefulShutdown(child, platform);
        verbose(`Polite-quit to editor child PID ${child}: ${ok ? 'sent' : 'failed'}`);
        if (ok) sent = true;
      }
    }
    if (!sent && !options.force) {
      spinner.error(`Could not ask PID ${pid} to quit (no window to close); re-run with --force to stop it.`);
      process.exit(1);
    }

    if (sent) spinner.text = `Waiting up to ${timeoutSeconds}s for PID ${pid} to exit...`;
    const exited = sent ? await waitForExit(pid, timeoutSeconds * 1000, platform) : false;

    if (exited) {
      spinner.success(`Godot editor (PID ${pid}) exited cleanly`);
      process.exit(0);
    }

    if (!options.force) {
      spinner.error(
        `Godot editor (PID ${pid}) did not exit within ${timeoutSeconds}s. ` +
          `Re-run with --force to hard-kill, or close it manually.`,
      );
      process.exit(1);
    }

    spinner.text = `Force-killing PID ${pid}...`;
    // Not /T for a user's GUI editor: its children may be a running game or an external script editor.
    const killed = sendForceKill(pid, platform, editor!.consoleWrapper);
    if (!killed) {
      spinner.error(`Failed to force-kill PID ${pid}`);
      process.exit(1);
    }

    const reaped = await waitForExit(pid, 5000, platform);
    if (reaped || !isProcessAlive(pid, platform)) {
      spinner.success(`Godot editor (PID ${pid}) force-killed`);
      process.exit(0);
    }

    spinner.error(`Godot editor (PID ${pid}) is still alive after force-kill`);
    process.exit(1);
  });

/**
 * `close --owned-only`: ask the editor named by our lease (`.ai-game-dev/session.json`) to save and quit through
 * its plugin (a windowless editor gets no WM_CLOSE). Anything else — no lease, a stale lease, a user's editor — is
 * left alone.
 */
async function closeOwned(projectPath: string, timeoutSeconds: number, options: CloseOptions, report: boolean): Promise<boolean> {
  let session: ReturnType<typeof createGodotSession>['session'];
  try {
    session = createGodotSession({ projectPath, ...resolveSessionConnection(options) }).session;
  } catch (err) {
    if (!report) {
      verbose(`No usable session lease: ${err instanceof Error ? err.message : String(err)}`);
      return false;
    }
    ui.error(err instanceof Error ? err.message : String(err));
    process.exit(1);
  }
  const result = await session.closeOwned({ timeoutMs: timeoutSeconds * 1000, force: options.force === true });
  switch (result.kind) {
    case 'closed':
      ui.success(`Godot editor (PID ${result.pid}) ${result.forced ? 'force-killed' : 'closed'}`);
      return true;
    case 'not-owned':
      if (report) ui.success(`No editor started by godot-cli is running for ${projectPath}${result.staleLeaseRemoved ? ' (removed a stale lease)' : ''}`);
      return false;
    case 'still-running':
      ui.error(`Godot editor (PID ${result.pid}) is still running after ${timeoutSeconds}s` + (options.force ? '' : '; re-run with --force'));
      process.exit(1);
  }
}
