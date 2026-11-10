import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { CommandOutput, addFormatOption, exitWithError, parseFormat } from '../utils/output.js';
import { ExitCode } from '../utils/exit-codes.js';
import { exportProject, type ExportMode } from '../lib/export-project.js';
import {
  addHeadlessOptions,
  diagnosticErrors,
  finishHeadless,
  headlessProjectPath,
  headlessTimeout,
  printSteps,
  type HeadlessCliOptions,
} from './headless-common.js';

interface ExportCliOptions extends HeadlessCliOptions {
  debug?: boolean;
  pack?: boolean;
}

export const exportCommand = addFormatOption(
  addHeadlessOptions(
    new Command('export')
      .description('Export a preset headless; checks the export templates first and the output file afterwards')
      .argument('<preset>', 'Export preset name (export_presets.cfg)')
      .argument('<output>', 'Output file path')
      .option('--debug', 'Debug export (--export-debug)')
      .option('--pack', 'Export only the .pck/.zip pack (--export-pack); needs no export templates'),
    1_800_000,
  ),
).action(async (preset: string, outputPath: string, options: ExportCliOptions) => {
  const output = new CommandOutput('export', parseFormat(options.format));
  if (options.debug && options.pack) exitWithError(ExitCode.Usage, 'USAGE', 'Pass either --debug or --pack, not both.');
  const mode: ExportMode = options.pack ? 'pack' : options.debug ? 'debug' : 'release';

  const result = await exportProject({
    projectPath: headlessProjectPath(undefined, options),
    timeoutMs: headlessTimeout(options),
    allowOpenProject: options.allowOpen === true,
    preset,
    output: outputPath,
    mode,
    ...(options.editorPath ? { editorPath: options.editorPath } : {}),
    onProgress: (m) => output.progress(m),
  });

  if (!output.machine) {
    ui.heading('Godot Export');
    ui.label('Preset', `${preset} (${mode})`);
    if (result.output) ui.label('Output', result.output);
    printSteps(result.steps ?? []);
  }
  const data = result.projectPath ? {
    projectPath: result.projectPath,
    preset,
    mode,
    output: result.output,
    sizeBytes: result.sizeBytes ?? null,
    templatesDir: result.templatesDir ?? null,
    steps: result.steps,
  } : null;
  finishHeadless(
    output,
    result,
    data,
    result.kind === 'failure' ? diagnosticErrors('EXPORT_FAILED', (result.diagnostics ?? []).slice(0, 20)) : [],
    result.sizeBytes !== undefined ? `Exported ${result.output} (${result.sizeBytes} bytes)` : 'Exported',
  );
});
