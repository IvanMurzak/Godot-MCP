import { Command } from 'commander';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { installExtension } from '../lib/install-extension.js';

interface InstallExtensionCliOptions {
  path?: string;
  version?: string;
}

export const installExtensionCommand = new Command('install-extension')
  .description(
    'Install a Godot-MCP extension into a Godot C# project: resolve the extension <id> from the shared catalog, add (or update) its <PackageReference> in the project .csproj, then rebuild to restore it. Idempotent. The project path defaults to the current directory (like install-plugin).',
  )
  .argument('<id>', 'Extension id to install (the package id, or the extension name)')
  .argument('[path]', 'Path to the Godot project (defaults to the current directory)')
  .option('--path <path>', 'Path to the Godot project')
  .option('--version <x.y.z>', 'Override the catalog-pinned version to install')
  .action(async (id: string, positionalPath: string | undefined, options: InstallExtensionCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    ui.heading('Installing Godot-MCP extension');
    verbose(`Extension id: ${id}`);
    verbose(`Project path: ${projectPath}`);
    if (options.version) verbose(`--version: ${options.version}`);

    const spinner = ui.startSpinner('Installing extension...');
    const result = await installExtension({
      godotProjectPath: projectPath,
      extensionId: id,
      version: options.version,
    });

    if (result.kind === 'failure') {
      spinner.error('Failed to install extension');
      ui.error(result.error.message);
      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
      process.exit(1);
    }

    switch (result.outcome) {
      case 'added':
        spinner.success(`Installed ${result.packageId}`);
        break;
      case 'updated':
        spinner.success(`Updated ${result.packageId} to ${result.toVersion}`);
        break;
      case 'already-up-to-date':
        spinner.success(`${result.packageId} is already up to date`);
        break;
      case 'no-project':
        spinner.error('No project .csproj found');
        break;
    }

    ui.label('Status', result.message);
    if (result.csprojPath) ui.label('Project .csproj', result.csprojPath);
    if (result.rebuildRequired) {
      ui.label('Next step', 'Rebuild solutions (e.g. `godot-cli build`) to restore + compile the extension.');
    }

    for (const warning of result.warnings) {
      console.log('');
      ui.warn(warning);
    }
  });
