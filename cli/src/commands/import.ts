import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { CommandOutput, addFormatOption, parseFormat } from '../utils/output.js';
import { importProject } from '../lib/import-project.js';
import {
  addHeadlessOptions,
  diagnosticErrors,
  finishHeadless,
  headlessProjectPath,
  headlessTimeout,
  printDiagnostics,
  printSteps,
  type HeadlessCliOptions,
} from './headless-common.js';

export const importCommand = addFormatOption(
  addHeadlessOptions(
    new Command('import')
      .description('Import the project headless (godot --headless --import); fails on import and script errors in the log')
      .argument('[path]', 'Godot project path'),
    600_000,
  ),
).action(async (positional: string | undefined, options: HeadlessCliOptions) => {
  const output = new CommandOutput('import', parseFormat(options.format));
  const result = await importProject({
    projectPath: headlessProjectPath(positional, options),
    timeoutMs: headlessTimeout(options),
    allowOpenProject: options.allowOpen === true,
    ...(options.editorPath ? { editorPath: options.editorPath } : {}),
    onProgress: (m) => output.progress(m),
  });
  const diagnostics = result.diagnostics ?? [];
  for (const d of diagnostics.filter((x) => x.severity === 'warning')) output.warnings.push(d.message);
  if (!output.machine) {
    ui.heading('Godot Import');
    if (result.projectPath) ui.label('Project', result.projectPath);
    printSteps(result.steps ?? []);
    printDiagnostics(diagnostics.filter((d) => d.severity === 'error'));
  }
  const data = result.projectPath ? { projectPath: result.projectPath, diagnostics, steps: result.steps } : null;
  finishHeadless(output, result, data, result.kind === 'failure'
    ? diagnostics.filter((d) => d.severity === 'error').flatMap((d) => diagnosticErrors(d.source === 'engine' ? 'IMPORT_FAILED' : 'SCRIPT_ERRORS', [d]))
    : [], 'Imported');
});
