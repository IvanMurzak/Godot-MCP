import { Command, Option } from 'commander';
import { DEFAULT_SESSION_READY_TIMEOUT_MS } from '@baizor/gamedev-cli-core';
import { RQL_MUTATE_TOOL, RQL_QUERY_TOOL, RQL_SCHEMA_TOOL, isRqlMutation, resourcesWrittenSince, rqlMaySave, stripToolName } from '../lib/rql.js';
import { runTool } from '../lib/run-tool.js';
import { resolveAndValidateProjectPath } from '../utils/connection.js';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat } from '../utils/output.js';
import { runToolAction, type BuilderOptions, type RunToolCliOptions } from './run-tool-builder.js';

interface RqlCliOptions extends Omit<RunToolCliOptions, 'input' | 'inputFile'> {
  schema?: string | boolean;
  dryRun?: boolean;
  maxTargets?: string;
}

/** The recipes `rql --help` shows; the root help shows the first ones. */
export const RQL_RECIPES: ReadonlyArray<readonly [string, string]> = [
  ['editor | call OpenScene("res://scenes/forest.tscn")', 'open a scene (writes no file): nodes lists the EDITED scene only'],
  ['editor | call NewScene("res://scenes/cave.tscn", "Node2D", "Cave")', 'create, save and open a scene (path, root class, root name)'],
  ['editor | call CreateNode("OmniLight3D", {name: "Lamp", position: [1, 3, 0]})', 'a new node under the scene root (meters, Y up)'],
  ['nodes | where name starts-with "Tree" | select name class Position', 'read'],
  ['nodes | where name contains "Enemy" | count', 'count'],
  ['nodes | where name starts-with "Rock" | set Scale = Scale * 1.5', 'change (vectors compute per axis)'],
  ['editor | call SaveScene()', 'write the edited scene to its .tscn; prints the files written'],
  ['nodes | where name == "Old" | call Free()', 'delete'],
  ['godot.resources("StandardMaterial3D") | select path props["albedo_color"]', 'resources of a class'],
];

/** How `rql` calls a tool: as run-tool does. */
const RQL_CALL: BuilderOptions = {
  name: 'rql',
  description: '',
  argDescription: '',
  routePrefix: '/api/tools',
  verboseLabel: 'Tool',
  headingLabel: 'RQL',
  errorNoun: 'tool',
  invoke: runTool,
};

const RQL_HELP_FOOTER = [
  'Recipes:',
  ...RQL_RECIPES.map(([query, what]) => `  ${query}  # ${what}`),
  '',
  'Stages: where, select a b, sort x [desc], first N, count, get <member>, set a = expr, call Method(args).',
  'Operators: == != < <= > >= and or not, contains, starts-with, ends-with, x in [a, b], $ is Node3D.',
  'Quote the query with single quotes; strings inside it take double quotes. A receipt (set / call) lists every change,',
  'so a separate check is rarely needed; scene edits are unsaved until SaveScene() (resource edits save at once).',
  'Sources, types and members: godot-cli rql --schema [Type]  (full language: godot-cli tools rql-query)',
].join('\n');

export const rqlCommand = addFormatOption(
  new Command('rql')
    .description('Read or change the editor with one RQL query: scenes, nodes, resources (a query ending in set / call changes the editor, any other only reads); starts a headless editor when none answers')
    .argument('[query]', 'RQL query, e.g. \'nodes | where name starts-with "Tree" | count\'')
    .option('--path <path>', 'Godot project path (defaults to cwd)')
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
  const query = queryArg === undefined ? undefined : stripToolName(queryArg);
  let toolName: string;
  let input: Record<string, unknown>;
  if (options.schema !== undefined) {
    toolName = RQL_SCHEMA_TOOL;
    input = typeof options.schema === 'string' ? { type: options.schema } : {};
  } else if (query === undefined || query.trim() === '') {
    const output = new CommandOutput('rql', parseFormat(options.format));
    return output.fail(ExitCode.Usage, 'USAGE', 'No query given: godot-cli rql \'nodes | where name starts-with "Tree" | count\' (recipes: rql --help)');
  } else if (isRqlMutation(query)) {
    toolName = RQL_MUTATE_TOOL;
    input = {
      query,
      ...(options.dryRun ? { dry_run: true } : {}),
      ...(options.maxTargets !== undefined ? { max_targets: Number(options.maxTargets) } : {}),
    };
  } else {
    toolName = RQL_QUERY_TOOL;
    input = { query };
  }

  // A save answers without naming its files; which files it wrote is read from the disk, so nobody re-opens the scene.
  const human = !options.raw && parseFormat(options.format) === 'text';
  const saveCheck = toolName === RQL_MUTATE_TOOL && !options.dryRun && human && rqlMaySave(query!)
    ? resolveAndValidateProjectPath(undefined, options) : undefined;
  const startedAt = Date.now();
  await runToolAction(RQL_CALL, toolName, undefined, { ...options, input: JSON.stringify(input) }, () => {
    if (saveCheck === undefined) return;
    const written = resourcesWrittenSince(saveCheck, startedAt);
    console.log(written.length > 0 ? `Saved to disk: ${written.join(', ')}` : 'No scene or resource file changed on disk (nothing unsaved?)');
  });
});
