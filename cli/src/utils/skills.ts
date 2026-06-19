// Godot-MCP skill catalog + SKILL.md content generation.
//
// The Godot analog of Unity-MCP's server-side skill generation. Unlike the
// Unity CLI — which POSTs to a running editor's `/api/system-tools/unity-skill-
// generate` endpoint to have the engine emit skill files — the Godot MCP server
// exposes NO skill-generate HTTP endpoint, and the Godot addon only auto-
// generates skills in-process on plugin boot (`GodotMcpConnection.Start` →
// `GenerateSkillFilesIfNeeded`). That in-process path requires a live, connected
// editor and writes into `user://`-derived locations the CLI cannot drive.
//
// So `godot-cli setup-skills` generates the skill files LOCALLY from this
// catalog: a `SKILL.md`-per-tool-family directory written under the selected
// agent's `skillsPath`, with content describing the godot_mcp addon's tool
// families (NOT Unity's tool names). This makes the command self-contained — no
// server, no running editor — and idempotent (re-running rewrites the same
// bytes). The catalog mirrors `Godot-MCP/CLAUDE.md` § Tool families; keep the
// two in lockstep when the addon's tool surface changes.

/** A single Godot-MCP tool family and the individual tools it exposes. */
export interface SkillFamily {
  /** Stable slug used for the per-family directory name (e.g. `node`). */
  id: string;
  /** Human-facing family name (matches the addon's `Tool_<Family>` class). */
  title: string;
  /** One-line summary of what the family is for. */
  summary: string;
  /** The MCP tool names in this family (as exposed to MCP clients). */
  tools: { name: string; description: string }[];
}

/**
 * The Godot-MCP tool-family catalog. Mirrors the 10 families documented in
 * `Godot-MCP/CLAUDE.md` § Tool families and the `[AiToolType]` classes under
 * `addons/godot_mcp/Runtime/Tools/` and `addons/godot_mcp/Editor/Tools/`. Tool names are the `[AiTool("<name>")]` identifiers
 * an MCP client invokes.
 */
export const SKILL_FAMILIES: readonly SkillFamily[] = [
  {
    id: 'node',
    title: 'Node',
    summary: 'Find, create, modify, reparent, duplicate, and delete nodes in the open scene tree.',
    tools: [
      { name: 'node-find', description: 'Find nodes in the open scene by path, name, or type.' },
      { name: 'node-create', description: 'Create a new node (optionally instancing a sub-scene) under a parent.' },
      { name: 'node-modify', description: 'Set properties on an existing node.' },
      { name: 'node-set-parent', description: 'Reparent a node to a new parent in the scene tree.' },
      { name: 'node-duplicate', description: 'Duplicate an existing node.' },
      { name: 'node-delete', description: 'Delete a node from the scene tree.' },
    ],
  },
  {
    id: 'scene',
    title: 'Scene',
    summary: 'Open, save, create, list, and inspect Godot scenes (`.tscn`).',
    tools: [
      { name: 'scene-open', description: 'Open a scene file in the editor.' },
      { name: 'scene-save', description: 'Save the current (or a specified) scene.' },
      { name: 'scene-create', description: 'Create a new scene with a given root node.' },
      { name: 'scene-list-opened', description: 'List the scenes currently open in the editor.' },
      { name: 'scene-get-data', description: 'Read the structured node tree / data of a scene.' },
    ],
  },
  {
    id: 'resource',
    title: 'Resource',
    summary: 'Find, read, modify, create, move, and delete Godot resources (`.tres`/`.res` and assets).',
    tools: [
      { name: 'resource-find', description: 'Find resource files under `res://` by path or type.' },
      { name: 'resource-get-data', description: 'Read the structured data of a resource.' },
      { name: 'resource-modify', description: 'Set properties on an existing resource.' },
      { name: 'resource-create', description: 'Create a new resource of a given type.' },
      { name: 'resource-move', description: 'Move/rename a resource within the project.' },
      { name: 'resource-delete', description: 'Delete a resource file from the project.' },
    ],
  },
  {
    id: 'filesystem',
    title: 'FileSystem',
    summary: 'List and reimport project files through the Godot editor filesystem.',
    tools: [
      { name: 'filesystem-list', description: 'List files/folders under a `res://` path.' },
      { name: 'filesystem-reimport', description: 'Trigger a reimport of project assets.' },
    ],
  },
  {
    id: 'script',
    title: 'Script',
    summary: 'Read, create, update, delete, and attach GDScript / C# scripts.',
    tools: [
      { name: 'script-read', description: 'Read the source of a script file.' },
      { name: 'script-create', description: 'Create a new script file.' },
      { name: 'script-update', description: 'Replace or patch the contents of a script.' },
      { name: 'script-delete', description: 'Delete a script file.' },
      { name: 'script-attach-to-node', description: 'Attach a script to a node in the scene.' },
    ],
  },
  {
    id: 'screenshot',
    title: 'Screenshot',
    summary: 'Capture the editor viewport, a camera, or an isolated node as an image.',
    tools: [
      { name: 'screenshot-viewport', description: 'Capture the editor viewport.' },
      { name: 'screenshot-camera', description: 'Capture from a specific camera.' },
      { name: 'screenshot-isolated', description: 'Capture an isolated node/subtree.' },
    ],
  },
  {
    id: 'editor',
    title: 'Editor',
    summary: 'Read/modify editor application state and the current node selection.',
    tools: [
      { name: 'editor-application-get-state', description: 'Read editor application state (play mode, focus, etc.).' },
      { name: 'editor-application-set-state', description: 'Change editor application state.' },
      { name: 'editor-selection-get', description: 'Read the current editor node selection.' },
      { name: 'editor-selection-set', description: 'Set the current editor node selection.' },
    ],
  },
  {
    id: 'console',
    title: 'Console',
    summary: 'Read and clear the editor/runtime log buffer.',
    tools: [
      { name: 'console-get-logs', description: 'Read collected editor/runtime log entries.' },
      { name: 'console-clear-logs', description: 'Clear the collected log buffer.' },
    ],
  },
  {
    id: 'reflection',
    title: 'Reflection',
    summary: 'Discover and invoke engine methods reflectively via ReflectorNet.',
    tools: [
      { name: 'reflection-method-find', description: 'Find methods on an engine type by name/signature.' },
      { name: 'reflection-method-call', description: 'Invoke a method reflectively with serialized arguments.' },
    ],
  },
  {
    id: 'ping',
    title: 'Ping',
    summary: 'Liveness check — confirm the MCP connection to the Godot editor is alive.',
    tools: [
      { name: 'ping', description: 'Return `pong` (or the echoed message) to confirm connectivity.' },
    ],
  },
] as const;

/** A single skill file the generator will write (path is relative to the skills dir). */
export interface SkillFile {
  /** Path relative to the agent's skills directory, e.g. `godot-mcp-node/SKILL.md`. */
  relativePath: string;
  /** Full file contents (UTF-8, LF-terminated). */
  content: string;
}

const ROOT_SKILL_NAME = 'godot-mcp';

/**
 * YAML-escape a scalar that goes into single quotes in frontmatter.
 *
 * Correctness depends on the value staying single-line: a single-quoted YAML
 * scalar cannot contain a literal newline, so an embedded `\n` would produce
 * invalid frontmatter. All catalog descriptions here are single-line ASCII, so
 * doubling `'` is sufficient; revisit if multi-line descriptions are ever added.
 */
function yamlSingleQuote(value: string): string {
  return `'${value.replace(/'/g, "''")}'`;
}

/**
 * Build the root overview `SKILL.md` — a map of all tool families pointing the
 * agent at the per-family skills.
 */
function buildRootSkill(): SkillFile {
  const lines: string[] = [];
  lines.push('---');
  lines.push(`name: ${ROOT_SKILL_NAME}`);
  lines.push(
    `description: ${yamlSingleQuote(
      'Drive the Godot editor through Godot-MCP. Use when creating/editing scenes, nodes, resources, scripts, or capturing screenshots in a Godot project connected to an MCP server.',
    )}`,
  );
  lines.push('---');
  lines.push('');
  lines.push('# Godot-MCP');
  lines.push('');
  lines.push(
    'Godot-MCP exposes the Godot editor to an MCP client via the `godot_mcp` addon. The addon connects to an MCP server (cloud `ai-game.dev` by default, or a custom local server) and surfaces the tool families below. Invoke tools by their MCP names (e.g. `node-create`, `scene-open`).',
  );
  lines.push('');
  lines.push('## When to use');
  lines.push('');
  lines.push('- The project is a Godot project with the `godot_mcp` addon enabled and connected.');
  lines.push('- You need to read or mutate the open scene, project resources, or scripts programmatically.');
  lines.push('- You want to verify a change visually with a screenshot, or inspect the editor log.');
  lines.push('');
  lines.push('## Tool families');
  lines.push('');
  lines.push('| Family | Tools | Use for |');
  lines.push('| --- | --- | --- |');
  for (const fam of SKILL_FAMILIES) {
    const toolNames = fam.tools.map((t) => `\`${t.name}\``).join(', ');
    lines.push(`| ${fam.title} | ${toolNames} | ${fam.summary} |`);
  }
  lines.push('');
  lines.push('See the per-family skill files in this directory for the tool list and usage of each family.');
  lines.push('');
  return { relativePath: `${ROOT_SKILL_NAME}/SKILL.md`, content: lines.join('\n') };
}

/** Build a per-family `SKILL.md`. */
function buildFamilySkill(fam: SkillFamily): SkillFile {
  const slug = `${ROOT_SKILL_NAME}-${fam.id}`;
  const lines: string[] = [];
  lines.push('---');
  lines.push(`name: ${slug}`);
  lines.push(`description: ${yamlSingleQuote(`Godot-MCP ${fam.title} tools. ${fam.summary}`)}`);
  lines.push('---');
  lines.push('');
  lines.push(`# Godot-MCP — ${fam.title}`);
  lines.push('');
  lines.push(fam.summary);
  lines.push('');
  lines.push('## Tools');
  lines.push('');
  for (const tool of fam.tools) {
    lines.push(`- \`${tool.name}\` — ${tool.description}`);
  }
  lines.push('');
  lines.push('## Notes');
  lines.push('');
  lines.push(
    '- All editor-driving tools run on the Godot editor main thread; effects are applied to the live editor state.',
  );
  lines.push('- Results are returned as structured data (ReflectorNet-serialized), not free-form text.');
  lines.push('');
  return { relativePath: `${slug}/SKILL.md`, content: lines.join('\n') };
}

/**
 * Build the full list of skill files for a Godot project: one root overview plus
 * one per tool family. Deterministic — the same input always yields the same
 * bytes, so writing them is idempotent.
 */
export function buildSkillFiles(): SkillFile[] {
  const files: SkillFile[] = [buildRootSkill()];
  for (const fam of SKILL_FAMILIES) {
    files.push(buildFamilySkill(fam));
  }
  return files;
}
