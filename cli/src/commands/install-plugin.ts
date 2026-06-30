import { Command } from 'commander';
import { createRequire } from 'module';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { installPlugin } from '../lib/install-plugin.js';

interface InstallPluginCliOptions {
  path?: string;
  source?: string;
  version?: string;
}

/** The CLI's own version — the default addon release version to download. */
function cliVersion(): string {
  try {
    const require = createRequire(import.meta.url);
    const pkg = require('../../package.json') as { version: string };
    return pkg.version;
  } catch {
    return '';
  }
}

export const installPluginCommand = new Command('install-plugin')
  .description(
    'Install the godot_mcp addon into a Godot C# project: materialize res://addons/godot_mcp/ (download the matching GitHub release, or --source a local copy), add the required NuGet PackageReferences and the extension-catalog <EmbeddedResource> to the project .csproj, and enable the plugin in project.godot.',
  )
  .argument('[path]', 'Path to the Godot project')
  .option('--path <path>', 'Path to the Godot project')
  .option(
    '--source <path>',
    'Install the addon from a local directory (offline / dev / CI) instead of downloading it',
  )
  .option(
    '--version <x.y.z>',
    'Addon release version to download (defaults to this CLI version). Ignored with --source.',
  )
  .action(async (positionalPath: string | undefined, options: InstallPluginCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    ui.heading('Installing godot_mcp addon');
    verbose(`Project path: ${projectPath}`);
    if (options.source) verbose(`--source: ${options.source}`);

    const spinner = ui.startSpinner('Installing addon...');
    const result = await installPlugin({
      godotProjectPath: projectPath,
      source: options.source,
      version: options.version ?? cliVersion(),
    });

    if (result.kind === 'failure') {
      spinner.error('Failed to install plugin');
      ui.error(result.error.message);
      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
      process.exit(1);
    }

    if (result.changed) {
      spinner.success('godot_mcp addon installed and enabled');
    } else {
      spinner.success('godot_mcp addon was already enabled');
    }

    // Addon files outcome.
    if (result.materialize.source === 'download') {
      ui.label('Addon files', `downloaded → ${result.materialize.addonDir}`);
    } else if (result.materialize.source === 'local') {
      ui.label('Addon files', `copied from ${result.materialize.sourceDir} → ${result.materialize.addonDir}`);
    }

    // csproj outcome.
    if (result.csproj.csprojPath) {
      const summary = result.csproj.packages
        .map((p) => `${p.id}@${p.version} (${p.action})`)
        .join(', ');
      ui.label('NuGet packages', summary || '(none)');
      const embedSummary = result.csproj.embeds
        .map((e) => `${e.logicalName} (${e.action})`)
        .join(', ');
      ui.label('Embedded resources', embedSummary || '(none)');
    }

    ui.label('project.godot', result.projectGodotPath);
    ui.label('Enabled plugins', result.enabledPlugins.join(', ') || '(none)');

    for (const warning of result.warnings) {
      console.log('');
      ui.warn(warning);
    }
  });
