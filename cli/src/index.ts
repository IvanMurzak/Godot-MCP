import { Command } from 'commander';
import { createRequire } from 'module';
import { buildCommand } from './commands/build.js';
import { openCommand } from './commands/open.js';
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
  closeCommand,
  configureCommand,
  createProjectCommand,
  installPluginCommand,
  installExtensionCommand,
  openCommand,
  removePluginCommand,
  runToolCommand,
  runSystemToolCommand,
  setupMcpCommand,
  setupSkillsCommand,
  statusCommand,
  createUpdateCommand(pkg.version),
  waitForReadyCommand,
];

for (const cmd of subcommands) {
  cmd.option('-v, --verbose', 'Enable verbose diagnostic output');
  configureStyledHelp(cmd);
  program.addCommand(cmd);
}

// Apply styled help to root (after subcommands so banner knows about them)
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
    uiError((err as Error).message || String(err));
    process.exit(1);
  });
