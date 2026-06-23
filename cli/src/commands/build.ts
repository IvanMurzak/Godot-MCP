import { Command } from 'commander';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { buildProject } from '../lib/build.js';

interface BuildCliOptions {
  path?: string;
  configuration?: string;
  dotnetPath?: string;
}

export const buildCommand = new Command('build')
  .description(
    'Build the Godot project\'s C# assembly (dotnet build) so the godot_mcp addon loads on the next editor open. GDScript-only projects (no .csproj) are a no-op. This is the same build `open` runs before launching the editor.',
  )
  .argument('[path]', 'Path to the Godot project (defaults to current directory)')
  .option('--path <path>', 'Path to the Godot project (defaults to current directory)')
  .option('--configuration <cfg>', 'MSBuild configuration to compile (default: Debug)')
  .option('--dotnet-path <path>', 'Explicit dotnet executable path (default: dotnet on PATH)')
  .action(async (positionalPath: string | undefined, options: BuildCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    ui.heading('Building Godot C# project');
    verbose(`Project path: ${projectPath}`);

    const spinner = ui.startSpinner('Building C# assembly...');
    const result = await buildProject({
      projectPath,
      configuration: options.configuration,
      dotnetPath: options.dotnetPath,
      onProgress: (event) => {
        switch (event.phase) {
          case 'build-running':
            spinner.text = `Building ${path.basename(event.csprojPath)} ...`;
            verbose(`Build command: ${event.command}`);
            break;
          case 'build-skipped':
            verbose('No .csproj at project root — skipping build (GDScript-only).');
            break;
          default:
            break;
        }
      },
    });

    if (result.kind === 'failure') {
      spinner.error('Build failed');
      ui.error(result.errorMessage);
      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
      process.exit(1);
    }

    if (result.skipped) {
      spinner.success('No C# to build (GDScript-only project)');
      ui.info('This project has no .csproj at its root — nothing to compile.');
    } else {
      spinner.success('C# assembly built');
      ui.label('Project', result.projectPath);
      if (result.csprojPath) ui.label('Built', result.csprojPath);
      if (result.configuration) ui.label('Configuration', result.configuration);
      // Surface the captured `dotnet build` output in verbose mode — otherwise
      // a slow non-incremental first build shows only a static spinner.
      if (result.output) verbose(result.output.trim());
    }

    for (const warning of result.warnings) {
      console.log('');
      ui.warn(warning);
    }
  });
