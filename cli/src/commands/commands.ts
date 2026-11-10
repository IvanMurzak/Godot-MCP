import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { EXIT_CODE_TABLE, ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat } from '../utils/output.js';

export interface CommandManifestEntry {
  name: string;
  description: string;
  arguments: Array<{ name: string; required: boolean; variadic: boolean; description: string }>;
  options: Array<{ flags: string; description: string; default?: unknown }>;
}

export interface CommandManifest {
  name: string;
  version: string;
  commands: CommandManifestEntry[];
  exitCodes: typeof EXIT_CODE_TABLE;
}

/** A machine-readable manifest of every subcommand of `program`, generated from the commander tree. */
export function buildCommandManifest(program: Command, version: string): CommandManifest {
  const commands = program.commands.map((cmd) => ({
    name: cmd.name(),
    description: cmd.description(),
    arguments: cmd.registeredArguments.map((a) => ({
      name: a.name(),
      required: a.required,
      variadic: a.variadic,
      description: a.description,
    })),
    options: cmd.options.map((o) => ({
      flags: o.flags,
      description: o.description,
      ...(o.defaultValue !== undefined ? { default: o.defaultValue as unknown } : {}),
    })),
  }));
  commands.sort((a, b) => a.name.localeCompare(b.name));
  return { name: program.name(), version, commands, exitCodes: EXIT_CODE_TABLE };
}

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
    }
    output.finish(ExitCode.Success, manifest);
  });
}
