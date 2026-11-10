import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { CommandOutput, addFormatOption, parseFormat } from '../utils/output.js';
import { runScript } from '../lib/run-script.js';
import {
  addHeadlessOptions,
  diagnosticErrors,
  finishHeadless,
  headlessProjectPath,
  headlessTimeout,
  type HeadlessCliOptions,
} from './headless-common.js';

export const runScriptCommand = addFormatOption(
  addHeadlessOptions(
    new Command('run-script')
      .description('Run a one-shot SceneTree GDScript headless (godot -s); exit 0 only when it called quit(0)')
      .argument('<script>', 'GDScript file (res://… or a file path) extending SceneTree or MainLoop')
      .argument('[args...]', 'Arguments for the script (OS.get_cmdline_user_args())'),
    300_000,
    false,
  ),
).action(async (script: string, scriptArgs: string[], options: HeadlessCliOptions) => {
  const output = new CommandOutput('run-script', parseFormat(options.format));
  const result = await runScript({
    projectPath: headlessProjectPath(undefined, options),
    timeoutMs: headlessTimeout(options),
    // A -s script runs the game, not the editor: it does not touch the import cache an open editor owns.
    allowOpenProject: true,
    script,
    scriptArgs,
    ...(options.editorPath ? { editorPath: options.editorPath } : {}),
    // Text: stream the script's output as it runs.
    ...(!output.machine ? { onLine: (line: string) => console.log(line) } : {}),
  });
  const data = result.projectPath ? {
    projectPath: result.projectPath,
    script: result.script,
    scriptExitCode: result.scriptExitCode ?? null,
    output: result.output,
    diagnostics: result.diagnostics,
  } : null;
  if (!output.machine && result.kind === 'failure') ui.divider();
  finishHeadless(output, result, data, result.kind === 'failure' ? diagnosticErrors('SCRIPT_ERRORS', (result.diagnostics ?? []).filter((d) => d.severity === 'error')) : []);
});
