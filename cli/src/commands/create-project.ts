import { Command } from 'commander';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { createProject } from '../lib/create-project.js';

interface CreateProjectCliOptions {
  path?: string;
  name?: string;
  dotnet?: boolean;
}

export const createProjectCommand = new Command('create-project')
  .description(
    'Scaffold a minimal valid Godot 4.x project (project.godot + icon.svg). Pass --dotnet to also scaffold a Godot.NET.Sdk (C#) csproj. Refuses to overwrite an existing Godot/Unity/Unreal project.',
  )
  .argument('[path]', 'Target directory to scaffold the project into (defaults to current directory)')
  .option('--path <path>', 'Target directory to scaffold the project into')
  .option('--name <name>', 'Project name (defaults to a name derived from the folder name)')
  .option('--dotnet', 'Also scaffold a Godot.NET.Sdk (C#) csproj')
  .action(async (positionalPath: string | undefined, options: CreateProjectCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    ui.heading('Creating Godot project');
    verbose(`Target path: ${projectPath}`);
    verbose(`--dotnet: ${options.dotnet === true}`);

    const spinner = ui.startSpinner('Scaffolding project...');
    const result = await createProject({
      projectPath,
      name: options.name,
      dotnet: options.dotnet === true,
    });

    if (result.kind === 'failure') {
      spinner.error('Failed to create project');
      ui.error(result.errorMessage);
      // Surface failure-shape diagnostics BEFORE exiting: leftover files a
      // partial rollback could not remove, plus any rollback warnings.
      if (result.filesWritten.length > 0) {
        ui.label('Files left behind (cleanup failed)', result.filesWritten.join(', '));
      }
      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
      process.exit(1);
    }

    spinner.success(`Created Godot project "${result.projectName}"`);
    ui.label('Project path', result.projectPath);
    ui.label('Files written', result.filesWritten.join(', '));
    if (result.dotnet) {
      ui.label('C# (.NET)', 'enabled');
    }

    for (const warning of result.warnings) {
      console.log('');
      ui.warn(warning);
    }
  });
