import * as fs from 'fs';
import { Command, Option } from 'commander';
import { DEFAULT_SESSION_READY_TIMEOUT_MS } from '@baizor/gamedev-cli-core';
import { RQL_MUTATE_TOOL, decodeQueryFile, resourcesWrittenSince, rqlMaySave, rqlToolCall, saveReceipt, stripToolName } from '../lib/rql.js';
import { runTool } from '../lib/run-tool.js';
import { resolveAndValidateProjectPath } from '../utils/connection.js';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag } from '../utils/output.js';
import { RQL_LANGUAGE, RQL_RECIPES } from '../lib/cli-guide.js';
import { runToolAction, type RunToolCall, type RunToolCliOptions } from './run-tool-builder.js';

interface RqlCliOptions extends Omit<RunToolCliOptions, 'input' | 'inputFile'> {
  file?: string;
  schema?: string | boolean;
  dryRun?: boolean;
  maxTargets?: string;
}

/** How `rql` calls a tool: as run-tool does. */
const RQL_CALL: RunToolCall = {
  name: 'rql',
  routePrefix: '/api/tools',
  verboseLabel: 'Tool',
  errorNoun: 'tool',
  invoke: runTool,
};

const RQL_HELP_FOOTER = [
  'Recipes:',
  ...RQL_RECIPES.map(([query, what]) => `  ${query}  # ${what}`),
  '',
  ...RQL_LANGUAGE,
  'Quote the query with single quotes; strings inside it take double quotes (a shell that strips them: -f <file> or -f -).',
  'A receipt (set / call) lists every change, so a separate check is rarely needed; scene edits stay unsaved until',
  'SaveScene() (resource files and project settings are saved at once).',
  'Sources, types and members: godot-cli rql --schema [Type]  (full language: godot-cli tools rql-query)',
].join('\n');

/** The query from the argument or `-f <file>` (`-f -`: stdin; stdin is never read unasked). */
function readQuery(query: string | undefined, file: string | undefined): string | undefined {
  if (query !== undefined && file !== undefined) throw new Error('Pass the query either as an argument or with -f, not both.');
  if (file === undefined) return query;
  return decodeQueryFile(fs.readFileSync(file === '-' ? 0 : file)).trim();
}

export const rqlCommand = addFormatOption(
  new Command('rql')
    .description('Read or change the editor with one RQL query: scenes, nodes, resources (a query ending in set / call changes the editor, any other only reads); starts a headless editor when none answers')
    .argument('[query]', 'RQL query, e.g. \'nodes | where name starts-with "Tree" | count\'')
    .option('--path <path>', 'Godot project path (defaults to cwd)')
    .option('-f, --file <file>', 'Read the query from a file ("-" for stdin)')
    .option('--schema [type]', 'List the RQL sources (or one type\'s members) instead of running a query')
    .option('--dry-run', 'For set / call: show what would change, change nothing')
    .option('--max-targets <n>', 'For set / call: allow more than 200 targets (up to 1000)')
    .option('--no-start', 'Do not start a headless editor when none answers')
    .option('--once', 'Close the editor afterwards when this call started it')
    // The connection options of run-tool work here too; hidden to keep this help short.
    .addOption(new Option('--url <url>', 'MCP server base URL').hideHelp())
    .addOption(new Option('--token <token>', 'Bearer token override').hideHelp())
    .addOption(new Option('--raw', 'Output raw JSON').hideHelp())
    .addOption(new Option('--timeout <ms>', 'Request timeout in milliseconds').default('60000').hideHelp())
    .addOption(new Option('--no-build', 'Do not build the C# assembly before starting an editor').hideHelp())
    .addOption(new Option('--editor-path <path>', 'Godot editor executable for a started editor').hideHelp())
    .addOption(new Option('--ready-timeout <ms>', 'How long to wait for a started editor to become ready').default(String(DEFAULT_SESSION_READY_TIMEOUT_MS)).hideHelp())
    .addHelpText('after', `\n${RQL_HELP_FOOTER}\n`),
).action(async (queryArg: string | undefined, options: RqlCliOptions) => {
  const output = new CommandOutput('rql', parseFormat(options.format));
  let query: string | undefined;
  try {
    const text = readQuery(queryArg, options.file);
    query = text === undefined ? undefined : stripToolName(text);
  } catch (err) {
    return output.fail(ExitCode.Usage, 'USAGE', err instanceof Error ? err.message : String(err));
  }
  const maxTargets = options.maxTargets === undefined ? undefined : parseIntFlag(options.maxTargets, '--max-targets');
  const call = rqlToolCall(query, { schema: options.schema, dryRun: options.dryRun, maxTargets });
  if ('usage' in call) return output.fail(ExitCode.Usage, 'USAGE', call.usage);

  // A save answers without naming its files; which files a change wrote is read from the disk, so nobody re-opens the
  // scene: only for an editor on this machine, and only files written since the tool call itself.
  const receiptFor = call.toolName === RQL_MUTATE_TOOL && !options.dryRun && !options.raw && !output.machine
    ? resolveAndValidateProjectPath(undefined, options) : undefined;
  await runToolAction(RQL_CALL, call.toolName, undefined, { ...options, input: JSON.stringify(call.input) }, ({ calledAt, local }) => {
    if (receiptFor === undefined || !local) return;
    const receipt = saveReceipt(resourcesWrittenSince(receiptFor, calledAt), rqlMaySave(query!));
    if (receipt) console.log(receipt);
  });
});
