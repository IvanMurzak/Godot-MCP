import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { buildCommandManifest, toolFailureCodeLine } from '@baizor/gamedev-cli-core';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat } from '../utils/output.js';

/** `commands`: needs the finished program, so it is created with a getter. */
export function createCommandsCommand(getProgram: () => Command, version: string): Command {
  return addFormatOption(
    new Command('commands').description('Print a machine-readable manifest of every command, its arguments, options and the exit codes'),
  ).action((options: { format?: string }) => {
    const output = new CommandOutput('commands', parseFormat(options.format));
    const manifest = buildCommandManifest(getProgram(), version);
    if (!output.machine) {
      for (const c of manifest.commands) {
        const args = c.arguments.map((a) => (a.required ? `<${a.name}${a.variadic ? '...' : ''}>` : `[${a.name}${a.variadic ? '...' : ''}]`)).join(' ');
        console.log(`${c.name}${args ? ` ${args}` : ''}  —  ${c.description}`);
        for (const o of c.options) console.log(`    ${o.flags}${o.default !== undefined ? ` (default: ${JSON.stringify(o.default)})` : ''}`);
      }
      ui.heading('Exit codes');
      for (const e of manifest.exitCodes) console.log(`  ${String(e.code).padStart(2)}  ${e.name}: ${e.description}`);
      ui.heading('Error codes of a failed tool call (errors[].code)');
      for (const e of manifest.errorCodes) console.log(`  ${toolFailureCodeLine(e)}`);
    }
    output.finish(ExitCode.Success, manifest);
  });
}
