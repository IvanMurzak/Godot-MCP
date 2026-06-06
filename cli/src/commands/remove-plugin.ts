import { Command } from 'commander';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { removePlugin } from '../lib/install-plugin.js';

interface RemovePluginCliOptions {
  path?: string;
}

export const removePluginCommand = new Command('remove-plugin')
  .description('Disable the godot_mcp addon in a project (removes res://addons/godot_mcp/plugin.cfg from project.godot [editor_plugins])')
  .argument('[path]', 'Path to the Godot project')
  .option('--path <path>', 'Path to the Godot project')
  .action(async (positionalPath: string | undefined, options: RemovePluginCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    ui.heading('Disabling godot_mcp addon');
    verbose(`Project path: ${projectPath}`);

    const spinner = ui.startSpinner('Updating project.godot...');
    const result = await removePlugin({ godotProjectPath: projectPath });

    if (result.kind === 'failure') {
      spinner.error('Failed to disable plugin');
      ui.error(result.error.message);
      process.exit(1);
    }

    if (result.changed) {
      spinner.success('godot_mcp addon disabled');
    } else {
      spinner.success('godot_mcp addon was not enabled');
    }

    ui.label('project.godot', result.projectGodotPath);
    ui.label('Enabled plugins', result.enabledPlugins.join(', ') || '(none)');
  });
