import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { CommandOutput, addFormatOption, parseFormat } from '../utils/output.js';
import { checkProject, failingDiagnostics } from '../lib/check.js';
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

interface CheckCliOptions extends HeadlessCliOptions {
  strict?: boolean;
  includeAddons?: boolean;
}

export const checkCommand = addFormatOption(
  addHeadlessOptions(
    new Command('check')
      .description('CI gate without an editor: import, C# build, editor boot and a GDScript load check, judged from the logs')
      .argument('[path]', 'Godot project path')
      .option('--strict', 'Fail on GDScript and C# compiler warnings too')
      .option('--include-addons', 'Also load the GDScript under res://addons/'),
    900_000,
  ),
).action(async (positional: string | undefined, options: CheckCliOptions) => {
  const output = new CommandOutput('check', parseFormat(options.format));
  const timeoutMs = headlessTimeout(options);
  const result = await checkProject({
    projectPath: headlessProjectPath(positional, options),
    timeoutMs,
    strict: options.strict === true,
    includeAddons: options.includeAddons === true,
    allowOpenProject: options.allowOpen === true,
    ...(options.editorPath ? { editorPath: options.editorPath } : {}),
    onProgress: (m) => output.progress(m),
  });

  const diagnostics = result.diagnostics ?? [];
  const failing = failingDiagnostics(diagnostics, options.strict === true);
  for (const m of result.engineMessages ?? []) output.warnings.push(`engine ${m.severity}: ${m.message}`);
  for (const d of diagnostics.filter((x) => x.severity === 'warning' && !options.strict)) output.warnings.push(`${d.file ?? ''}${d.line ? `:${d.line}` : ''} ${d.message}`.trim());

  if (!output.machine) {
    ui.heading('Godot Check');
    if (result.projectPath) ui.label('Project', result.projectPath);
    printSteps(result.steps ?? []);
    printDiagnostics(diagnostics);
  }
  const data = result.kind === 'failure' && result.steps === undefined ? null : {
    projectPath: result.projectPath,
    addon: result.addon,
    gdscript: result.gdscript,
    csharp: result.csharp,
    diagnostics,
    steps: result.steps,
  };
  const errors = result.kind === 'failure'
    ? diagnosticErrors((d) => (d.source === 'csharp' ? 'COMPILATION_FAILED' : 'SCRIPT_ERRORS'), failing)
    : [];
  finishHeadless(output, result, data, errors, 'No script or compilation errors');
});
