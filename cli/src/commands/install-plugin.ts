import { Command } from 'commander';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { installPlugin } from '../lib/install-plugin.js';

interface InstallPluginCliOptions {
  path?: string;
}

export const installPluginCommand = new Command('install-plugin')
  .description('Enable the godot_mcp addon in a project (adds res://addons/godot_mcp/plugin.cfg to project.godot [editor_plugins])')
  .argument('[path]', 'Path to the Godot project')
  .option('--path <path>', 'Path to the Godot project')
  .action(async (positionalPath: string | undefined, options: InstallPluginCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    ui.heading('Enabling godot_mcp addon');
    verbose(`Project path: ${projectPath}`);

    const spinner = ui.startSpinner('Updating project.godot...');
    const result = await installPlugin({ godotProjectPath: projectPath });

    if (result.kind === 'failure') {
      spinner.error('Failed to enable plugin');
      ui.error(result.error.message);
      process.exit(1);
    }

    if (result.changed) {
      spinner.success('godot_mcp addon enabled');
    } else {
      spinner.success('godot_mcp addon was already enabled');
    }

    ui.label('project.godot', result.projectGodotPath);
    ui.label('Enabled plugins', result.enabledPlugins.join(', ') || '(none)');

    for (const warning of result.warnings) {
      console.log('');
      ui.warn(warning);
    }
  });
