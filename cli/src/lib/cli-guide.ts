// The one source of the agent-facing usage text: the root --help, `rql --help` and the `godot-cli` agent skill
// (`setup-skills`) all render from these constants, so the three cannot drift apart.
// Example names stay neutral (forest, cave, Lamp, Tree, Rock, Enemy): never a name a test or evaluation task uses.

/** RQL recipes: [query, what it does]. */
export type RqlRecipes = ReadonlyArray<readonly [string, string]>;
/** A featured command: [name, usage, what it does]. */
export type CommandRow = readonly [string, string, string];

/** The CLI's command name. */
export const CLI_NAME = 'godot-cli';

/** The recipes `rql --help` and the skill show; the root help shows the first {@link ROOT_RQL_RECIPES}. */
export const RQL_RECIPES: RqlRecipes = [
  ['editor | call OpenScene("res://scenes/forest.tscn")', 'open a scene (writes no file): nodes lists the EDITED scene only'],
  ['editor | call NewScene("res://scenes/cave.tscn", "Node2D", "Cave")', 'create, save and open a scene (path, root class, root name)'],
  ['editor | call CreateNode("OmniLight3D", {name: "Lamp", position: [1, 3, 0]})', 'a new node under the scene root (meters, Y up)'],
  ['nodes | where name starts-with "Tree" | select name class Position', 'read'],
  ['nodes | where name contains "Enemy" | count', 'count'],
  ['nodes | where name starts-with "Rock" | set Scale = Scale * 1.5', 'change (vectors compute per axis)'],
  ['editor | call SaveScene()', 'write the edited scene to its .tscn; prints the files written'],
  ['nodes | where name == "Old" | call Free()', 'delete'],
  ['godot.resources("StandardMaterial3D") | select path props["albedo_color"]', 'resources of a class'],
  ['nodes | where name == "Lamp" | set props["light_energy"] = 2', 'a Godot property or exported script variable by name: props["..."]'],
];

/** How many recipes the root help shows. */
export const ROOT_RQL_RECIPES = 7;

/** The RQL language in two lines (`rql --help`, the skill's RQL reference). */
export const RQL_LANGUAGE = [
  'Stages: where, select a b, sort x [desc], first N, count, get <member>, set a = expr, call Method(args).',
  'Operators: == != < <= > >= and or not, contains, starts-with, ends-with, x in [a, b], $ is Node3D.',
];

/** The editor commands the root help and the skill feature. */
export const EDITOR_COMMANDS: readonly CommandRow[] = [
  ['rql', "rql '<query>'", 'read or change scenes, nodes, resources (RQL; recipes below)'],
  ['exec', "exec '<C#>'", 'C# in the editor'],
  ['tools', 'tools [name]', "the editor's AI tools, or one in detail; run-tool <name> --input '<json>' calls one"],
];

/** The code commands (no editor needed). */
export const CODE_COMMANDS: readonly CommandRow[] = [
  ['check', 'check', 'GDScript and C# errors (file:line), no editor needed'],
  ['test', 'test', 'run GUT / gdUnit4 tests'],
];

/** How an editor command gets its editor (the root help's heading). */
export const EDITOR_START = 'the first call starts a headless editor and keeps it warm; no open / status needed';

/** Where the addon lives in a project: absent means `install-plugin` comes first. */
export const PLUGIN_FOLDER = 'addons/godot_mcp';

/**
 * The rules an agent needs beyond the recipes (the skill's list). Each answers a stumble seen in agent transcripts.
 * Kept terse: the agent re-reads every byte of the skill on every later turn, and a rule worded as a warning invites
 * extra checks.
 */
export const RQL_RULES = [
  '`nodes` lists only the edited scene (OpenScene first). Scene edits stay unsaved until `SaveScene()`; `NewScene` saves itself.',
  'The output of `set` / `call` lists each change (old, new) and the files saved: that is the check, no re-read needed.',
  'A wrong source or member fails with the valid names.',
];
