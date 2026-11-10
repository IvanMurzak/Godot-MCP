import { Command } from 'commander';
import { classifyCliError, writeFailureEnvelope } from '@baizor/gamedev-cli-core';
import { createRequire } from 'module';
import { buildCommand } from './commands/build.js';
import { loginCommand } from './commands/login.js';
import { openCommand } from './commands/open.js';
import { rqlCommand } from './commands/rql.js';
import { runToolCommand } from './commands/run-tool.js';
import { runSystemToolCommand } from './commands/run-system-tool.js';
import { statusCommand } from './commands/status.js';
import { waitForReadyCommand } from './commands/wait-for-ready.js';
import { setupMcpCommand } from './commands/setup-mcp.js';
import { setupSkillsCommand } from './commands/setup-skills.js';
import { configureCommand } from './commands/configure.js';
import { createProjectCommand } from './commands/create-project.js';
import { closeCommand } from './commands/close.js';
import { createUpdateCommand } from './commands/update.js';
import { installPluginCommand } from './commands/install-plugin.js';
import { installExtensionCommand } from './commands/install-extension.js';
import { removePluginCommand } from './commands/remove-plugin.js';
import { execCommand } from './commands/exec.js';
import { checkCommand } from './commands/check.js';
import { createCommandsCommand } from './commands/commands.js';
import { exportCommand } from './commands/export.js';
import { importCommand } from './commands/import.js';
import { runScriptCommand } from './commands/run-script.js';
import { testCommand } from './commands/test.js';
import { toolsCommand } from './commands/tools.js';
import { ExitCode } from './utils/exit-codes.js';
import { CommandExit, failActive } from './utils/output.js';
import { configureStyledHelp, error as uiError, setVerbose } from './utils/ui.js';
import { checkForUpdate, isRunningViaNpx, printUpdateNotification } from './utils/update-check.js';

const require = createRequire(import.meta.url);
const pkg = require('../package.json') as { version: string };

const program = new Command();

program
  .name('godot-cli')
  .description('Cross-platform CLI tool for Godot-MCP operations')
  .version(pkg.version)
  .option('-v, --verbose', 'Enable verbose diagnostic output');

// Register all subcommands
const subcommands = [
  buildCommand,
  checkCommand,
  closeCommand,
  createCommandsCommand(() => program, pkg.version),
  configureCommand,
  createProjectCommand,
  execCommand,
  exportCommand,
  importCommand,
  installPluginCommand,
  installExtensionCommand,
  loginCommand,
  openCommand,
  removePluginCommand,
  rqlCommand,
  runScriptCommand,
  runToolCommand,
  runSystemToolCommand,
  setupMcpCommand,
  setupSkillsCommand,
  statusCommand,
  testCommand,
  toolsCommand,
  createUpdateCommand(pkg.version),
  waitForReadyCommand,
];

for (const cmd of subcommands) {
  cmd.option('-v, --verbose', 'Enable verbose diagnostic output');
  configureStyledHelp(cmd);
  // Parse errors throw a CommanderError (handled below) instead of exiting 1.
  cmd.exitOverride();
  program.addCommand(cmd);
}

program.exitOverride();

// Apply styled help to root (after the subcommands: the root help names them)
configureStyledHelp(program, pkg.version);

// Wire verbose flag before any command executes
program.hook('preAction', (_thisCommand, actionCommand) => {
  const opts = actionCommand.optsWithGlobals() as { verbose?: boolean };
  if (opts.verbose) {
    setVerbose(true);
  }
});

// Show help when no command provided
program.action(() => {
  program.outputHelp();
});

// Start update check in background (non-blocking, parallel with command execution)
const updateCheckPromise = isRunningViaNpx() ? Promise.resolve(null) : checkForUpdate(pkg.version);

program.parseAsync()
  .then(async () => {
    const update = await updateCheckPromise;
    if (update) {
      printUpdateNotification(update.current, update.latest);
    }
  })
  .catch((err) => {
    // A command finished through its output: the exit is scheduled once stdout has flushed.
    if (err instanceof CommandExit) return;
    const failure = classifyCliError(err);
    // --help / --version are not errors.
    if (failure.informational) process.exit(ExitCode.Success);
    if (failure.code === 'USAGE') {
      // Commander already printed its parse errors to stderr; machine formats also get the envelope on stdout.
      if (!failure.printed) uiError(failure.message);
      writeFailureEnvelope(program, failure);
      process.exit(failure.exitCode);
    }
    try {
      if (failActive(err)) return;
    } catch (exit) {
      if (exit instanceof CommandExit) return;
      throw exit;
    }
    uiError(failure.message);
    process.exit(failure.exitCode);
  });
