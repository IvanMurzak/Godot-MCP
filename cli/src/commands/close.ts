import { Command } from 'commander';
import * as fs from 'fs';
import * as path from 'path';
import { platform as nodePlatform } from 'os';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { findGodotProcess } from '../utils/godot-process.js';
import {
  isProcessAlive,
  sendGracefulShutdown,
  sendForceKill,
  waitForExit,
  type SupportedPlatform,
} from '../utils/godot-shutdown.js';

export interface CloseOptions {
  timeout?: string;
  force?: boolean;
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
  const proc = findGodotProcess(projectPath);
  if (proc) {
    verbose(`Godot editor PID ${proc.pid} matched for project`);
    return proc.pid;
  }
  return null;
}

export const closeCommand = new Command('close')
  .description('Gracefully terminate the Godot editor instance running for a given project path')
  .argument('[path]', 'Path to the Godot project (defaults to current directory)')
  .option('--timeout <seconds>', `Polite-quit timeout in seconds (default: ${DEFAULT_TIMEOUT_SECONDS})`, String(DEFAULT_TIMEOUT_SECONDS))
  .option('--force', 'Hard-kill the editor if it does not exit within --timeout')
  .action(async (positionalPath: string | undefined, options: CloseOptions) => {
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

    const platform = nodePlatform() as SupportedPlatform;
    const pid = resolveEditorPid(projectPath);

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
    const sent = sendGracefulShutdown(pid, platform);
    if (!sent) {
      spinner.error(`Failed to deliver polite-quit signal to PID ${pid}`);
      process.exit(1);
    }

    spinner.text = `Waiting up to ${timeoutSeconds}s for PID ${pid} to exit...`;
    const exited = await waitForExit(pid, timeoutSeconds * 1000, platform);

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
    const killed = sendForceKill(pid, platform);
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
