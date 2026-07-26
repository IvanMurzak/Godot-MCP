import * as fs from 'fs';
import * as path from 'path';

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
 * The Godot-MCP tool-family catalog. Mirrors the 12 families documented in
 * `Godot-MCP/CLAUDE.md` § Tool families and the `[AiToolType]` classes under
 * `addons/godot_mcp/Runtime/Tools/` and `addons/godot_mcp/Editor/Tools/`. Tool names are the `[AiTool("<name>")]` identifiers
 * an MCP client invokes.
 *
 * Each family's `title` is the addon's `Tool_<title>` class-name suffix (e.g.
 * `FileSystem` → `Tool_FileSystem`, `RuntimeErrors` → `Tool_RuntimeErrors`). That
 * join key is what the CI cross-check (`discoverAddonToolFamilies`, exercised by
 * `cli/tests/skills-addon-parity.test.ts`) uses to fail the build if this catalog
 * ever drifts from the addon's actual `[AiToolType]` classes — so a new addon tool
 * family cannot ship without a matching catalog entry here.
 */
export const SKILL_FAMILIES: readonly SkillFamily[] = [
  {
    id: 'node',
    title: 'Node',
    summary: 'Find, create, modify, reparent, reorder, duplicate, and delete nodes in the open scene tree.',
    tools: [
      { name: 'node-find', description: 'Find nodes in the open scene by path, name, or type.' },
      {
        name: 'node-create',
        description:
          'Create a new node (optionally instancing a sub-scene) under a parent, optionally at a specific sibling index.',
      },
      { name: 'node-modify', description: 'Set properties on an existing node.' },
      { name: 'node-set-parent', description: 'Reparent a node to a new parent in the scene tree.' },
      {
        name: 'node-reorder',
        description:
          'Move a node to a different position among its siblings (child order is layout order in a container).',
      },
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
      { name: 'script-validate', description: 'Validate GDScript (.gd) files and return structured parse/compile diagnostics.' },
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
    id: 'runtime-errors',
    title: 'RuntimeErrors',
    summary:
      'Poll errors raised inside the RUNNING game (NOT the editor) — GDScript runtime errors, push_error/push_warning, shader errors, and C# unhandled/unobserved-Task exceptions, with multi-frame GDScript backtraces on Godot 4.5+.',
    tools: [
      {
        name: 'runtime-errors-get',
        description:
          'Read captured in-game runtime errors (oldest-first, newest-kept page). Returns `available:false` when the game never enabled runtime-error capture, so an empty list is never mistaken for health; poll only new errors by passing the previous result\'s `highestSequence` as `sinceSequence`.',
      },
      {
        name: 'runtime-errors-clear',
        description:
          'Clear the captured in-game runtime-error buffer (a no-op when capture is not enabled). The monotonic sequence counter is preserved, so a pre-clear `sinceSequence` poll still behaves correctly.',
      },
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
  {
    id: 'skills',
    title: 'Skills',
    summary:
      'Author new MCP tools for the project and regenerate the SKILL.md files from the tools the editor currently has registered.',
    tools: [
      {
        name: 'godot-skill-create',
        description:
          'Write a new C# (`.cs`) MCP tool file into the project under `res://`. The tool becomes callable after the project is rebuilt (Godot builds C# out-of-band). The file must declare an `[AiToolType]` partial class whose methods carry `[AiTool]`, and every Godot API call must be marshalled onto the editor main thread.',
      },
      {
        name: 'godot-skill-generate',
        description:
          'Regenerate every `SKILL.md` from the tools currently registered in the editor, into the selected AI agent\'s skills folder (or a project-relative `path` override).',
      },
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

// ---------------------------------------------------------------------------
// CI cross-check: catalog ⇄ addon tool-family parity
//
// `SKILL_FAMILIES` above is hand-maintained, but it must stay in lockstep with
// the addon's ACTUAL tool families — the `[AiToolType] partial class Tool_<Family>`
// classes under `addons/godot_mcp/{Runtime,Editor}/Tools/`. When a new family ships
// in the addon (as `runtime-errors` did in #161/#162) but nobody updates this
// catalog, the CLI silently stops advertising it. The parity test
// (`cli/tests/skills-addon-parity.test.ts`) compares the two sets and fails CI on
// any divergence, so the catalog can never quietly fall behind the addon again.
//
// Join key: each catalog family's `title` is exactly the addon class-name suffix
// (`Tool_<title>`). A naive PascalCase→kebab transform would NOT work — the addon
// has `Tool_FileSystem` (catalog id `filesystem`, no hyphen) and `Tool_RuntimeErrors`
// (catalog id `runtime-errors`, hyphenated) — so we match on the class-name suffix,
// not on a derived slug.
// ---------------------------------------------------------------------------

/** Catalog (skills.ts) side of the parity check: the `Tool_<title>` suffixes the CLI advertises. */
export function catalogToolFamilyClassSuffixes(): string[] {
  return SKILL_FAMILIES.map((f) => f.title).sort();
}

/**
 * Blank out the non-code spans of a C# source — always `//` line comments and `/* *\/` block comments,
 * and (when `blankStrings`) char + string literals too (regular `"…"` with `\` escapes, verbatim `@"…"`
 * with doubled `""`) — replacing each with a space while preserving newlines so nothing shifts lines.
 *
 * WHY THIS EXISTS: the discovery scans below are TEXT scans over the addon sources, and the addon
 * legitimately embeds C# SAMPLES inside comments and string literals — `Tool_Skills.Create.SkillBody.cs`
 * carries a full `[AiToolType] partial class Tool_Sample { … }` example in its `[AiSkillBody]` markdown so
 * the `godot-skill-create` skill can teach an agent the shape. Scanning the raw text discovers
 * `Tool_Sample` as a real addon family and fails parity against a phantom. Blanking non-code first leaves
 * exactly the real declarations — the same technique (and the same rationale) as the addon's
 * `scripts/check-runtime-boundary.py`, which blanks comments + strings before hunting editor-API tokens.
 *
 * The two scans need DIFFERENT strengths, hence the flag: the family scan matches a declaration
 * (`partial class Tool_X`) so it blanks strings too, while the tool-ID scan matches the QUOTED id in
 * `public const string XToolId = "x"` and would find nothing if strings were blanked. Keeping strings for
 * the id scan is safe: inside an embedded sample the quotes are necessarily escaped (`\"`) or doubled
 * (`""`), neither of which satisfies the `= "<id>"` shape.
 *
 * Deliberately a small single-purpose scanner, not a C# lexer: it covers the constructs this addon uses
 * (line/block comments, regular/verbatim/interpolated strings, escaped quotes, doubled `""`, char
 * literals). Raw string literals (`"""…"""`, C# 11) are not used in this addon and are not handled.
 */
export function stripNonCode(source: string, blankStrings: boolean): string {
  const out: string[] = [];
  let inLine = false;
  let inBlock = false;
  let inString = false; // regular "..."
  let inVerbatim = false; // @"..." (doubled "" is an escaped quote)
  let inChar = false; // '...'

  for (let i = 0; i < source.length; i++) {
    const c = source[i];
    const next = i + 1 < source.length ? source[i + 1] : '';

    if (inLine) {
      out.push(c === '\n' ? '\n' : ' ');
      if (c === '\n') inLine = false;
      continue;
    }
    if (inBlock) {
      if (c === '*' && next === '/') {
        out.push('  ');
        i++;
        inBlock = false;
        continue;
      }
      out.push(c === '\n' ? '\n' : ' ');
      continue;
    }
    if (inVerbatim) {
      if (c === '"' && next === '"') {
        out.push('  ');
        i++;
        continue;
      }
      out.push(c === '\n' ? '\n' : ' ');
      if (c === '"') inVerbatim = false;
      continue;
    }
    if (inString) {
      if (c === '\\') {
        out.push('  ');
        i++; // consume the escaped char (covers \" and \\)
        continue;
      }
      out.push(c === '\n' ? '\n' : ' ');
      if (c === '"') inString = false;
      continue;
    }
    if (inChar) {
      if (c === '\\') {
        out.push('  ');
        i++;
        continue;
      }
      // Preserve newlines like every sibling state does — the documented invariant is that blanking
      // never shifts line numbers, and an unterminated char literal would otherwise swallow one.
      out.push(c === '\n' ? '\n' : ' ');
      if (c === "'") inChar = false;
      continue;
    }

    // --- real code ---
    if (c === '/' && next === '/') {
      out.push('  ');
      i++;
      inLine = true;
      continue;
    }
    if (c === '/' && next === '*') {
      out.push('  ');
      i++;
      inBlock = true;
      continue;
    }
    if (blankStrings && c === '@' && next === '"') {
      out.push('  ');
      i++;
      inVerbatim = true;
      continue;
    }
    if (blankStrings && c === '"') {
      out.push(' ');
      inString = true;
      continue;
    }
    if (blankStrings && c === "'") {
      out.push(' ');
      inChar = true;
      continue;
    }
    out.push(c);
  }

  return out.join('');
}

/** Read an addon `.cs` file with its non-code spans blanked (see {@link stripNonCode}). */
function readAddonCode(file: string, blankStrings: boolean): string {
  return stripNonCode(fs.readFileSync(file, 'utf-8'), blankStrings);
}

/**
 * Resolve the addon's `Tools/` source directories relative to a repo root. The
 * cross-check passes the Godot-MCP repo root (the directory that contains both
 * `cli/` and `addons/`); each tool-family base file lives under one of these two
 * folders (Runtime = pure-managed families, Editor = `#if TOOLS` families).
 */
export function addonToolDirs(repoRoot: string): string[] {
  return [
    path.join(repoRoot, 'addons', 'godot_mcp', 'Runtime', 'Tools'),
    path.join(repoRoot, 'addons', 'godot_mcp', 'Editor', 'Tools'),
  ];
}

/**
 * Every addon `.cs` file under {@link addonToolDirs}, **recursively**.
 *
 * The scan used to be a flat `readdirSync` over the two `Tools/` folders, which made
 * any tool family placed in a SUBFOLDER (the Unity-MCP `API/SystemTool/` layout, and
 * the natural thing to reach for as the family list grows) invisible to both parity
 * checks below. That did not fail the gate — it made it pass VACUOUSLY at the stale
 * counts, which is strictly worse than no gate at all. Discovery is therefore
 * recursive, and nesting is a free organisational choice again.
 *
 * Implemented as an explicit walk rather than `readdirSync(dir, { recursive: true })`
 * so the behaviour is identical on every Node version this package supports
 * (`^20.19.0 || >=22.12.0`) — the `withFileTypes` + `recursive` combination reports
 * the parent directory through differently-named properties across those releases.
 * Returns absolute paths sorted for deterministic ordering.
 */
export function addonToolFiles(repoRoot: string): string[] {
  const files: string[] = [];

  const walk = (dir: string): void => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.isFile() && entry.name.endsWith('.cs')) files.push(full);
    }
  };

  for (const dir of addonToolDirs(repoRoot)) {
    if (!fs.existsSync(dir)) continue;
    walk(dir);
  }

  return files.sort();
}

/**
 * Addon side of the parity check: discover every tool family declared in the addon
 * by scanning for `[AiToolType] ... partial class Tool_<Family>` in the `.cs` sources
 * under the addon `Tools/` directories. Returns the sorted set of `<Family>` suffixes
 * (e.g. `Node`, `FileSystem`, `RuntimeErrors`).
 *
 * Robustness notes:
 * - The source scan is RECURSIVE (see {@link addonToolFiles}), so a family in a
 *   subfolder of `{Runtime,Editor}/Tools/` is discovered rather than silently
 *   skipped — a skipped family made the parity assertions pass vacuously.
 * - Tool families are partial classes split across files (`Tool_RuntimeErrors.cs`,
 *   `Tool_RuntimeErrors.Get.cs`, `Tool_RuntimeErrors.Clear.cs`). Only the base file
 *   carries the `[AiToolType]` attribute, so keying on `[AiToolType]` (not on file
 *   names) collapses each multi-file family to ONE entry automatically.
 * - The regex tolerates other attributes/whitespace between `[AiToolType]` and the
 *   class declaration, and an optional access modifier on the class.
 * - Sub-tool families use a COMPOUND class name (`Tool_Editor_Selection` adds the
 *   `editor-selection-*` tools to the `editor` family). We capture the whole
 *   `Tool_<Seg1>[_<Seg2>...]` suffix and attribute it to its FIRST segment
 *   (`Tool_Editor_Selection` → `Editor`), so a sub-tool class lands in its parent
 *   family instead of being silently dropped (which would happen if the capture
 *   stopped at the first `_`). The trailing `Set<string>` dedups the parent family
 *   when both `Tool_Editor` and `Tool_Editor_Selection` exist.
 * - The attribute MUST bind to its IMMEDIATELY-following type declaration: the gap
 *   between `[AiToolType]` and `Tool_<Family>` may contain only other attributes,
 *   doc comments, and whitespace — never an intervening type keyword. This stops a
 *   stray `[AiToolType]` on a non-`Tool_` type from being misattributed to a later
 *   `Tool_` class in the same file.
 */
export function discoverAddonToolFamilies(repoRoot: string): string[] {
  // `[AiToolType]` (possibly with args), then ONLY attributes / doc-comments /
  // whitespace (no intervening `class`/`struct`/`interface`/`enum`/`record`
  // keyword — `(?:(?!\b(?:class|struct|interface|enum|record)\b)[\s\S])*?` forbids
  // crossing one), then `partial class Tool_<Family>` where `<Family>` is the full
  // compound suffix (`Editor`, `Editor_Selection`, ...). First segment = the family.
  const familyRe =
    /\[AiToolType[^\]]*\](?:(?!\b(?:class|struct|interface|enum|record)\b)[\s\S])*?\bpartial\s+class\s+Tool_([A-Za-z][A-Za-z0-9]*(?:_[A-Za-z][A-Za-z0-9]*)*)\b/g;
  const found = new Set<string>();

  for (const file of addonToolFiles(repoRoot)) {
    const src = readAddonCode(file, /* blankStrings */ true);
    let m: RegExpExecArray | null;
    familyRe.lastIndex = 0;
    while ((m = familyRe.exec(src)) !== null) {
      // Attribute a compound sub-tool class (`Tool_Editor_Selection`) to its
      // parent family by taking the FIRST `_`-delimited segment (`Editor`).
      found.add(m[1].split('_')[0]);
    }
  }

  return [...found].sort();
}

// ---------------------------------------------------------------------------
// CI cross-check: catalog ⇄ addon tool-ID parity (the per-TOOL drift-guard)
//
// `discoverAddonToolFamilies` above guards the FAMILY set, but a tool can be
// added to an EXISTING family in the addon (as `script-validate` was added to
// `Tool_Script`) without changing the family set — so the family cross-check
// passes while the catalog silently lacks the new tool. These two helpers diff
// the catalog's tool-ID set against the addon's actual `*ToolId` constants so a
// missing/extra individual tool fails CI too (`skills-addon-parity.test.ts`).
// ---------------------------------------------------------------------------

/** Catalog (skills.ts) side of the per-tool parity check: every advertised tool name, sorted. */
export function catalogToolIds(): string[] {
  return SKILL_FAMILIES.flatMap((f) => f.tools.map((t) => t.name)).sort();
}

/**
 * Addon side of the per-tool parity check: discover every tool id declared in the
 * addon by scanning for `public const string <Name>ToolId = "<tool-id>"` in the
 * `.cs` sources under the addon `Tools/` directories. Returns the sorted, deduped
 * set of tool ids (e.g. `node-find`, `script-validate`, `runtime-errors-get`).
 *
 * Each tool family declares one `public const string <Name>ToolId = "<id>"` per
 * tool (the `[AiTool(<Name>ToolId, ...)]` identifier an MCP client invokes). The
 * regex captures the quoted kebab-case id; the `Set` dedups in case a constant is
 * ever referenced twice. Mirrors `discoverAddonToolFamilies`'s addon-source scan.
 */
export function discoverAddonToolIds(repoRoot: string): string[] {
  const toolIdRe = /public\s+const\s+string\s+\w+ToolId\s*=\s*"([a-z][a-z0-9-]*)"/g;
  const found = new Set<string>();

  for (const file of addonToolFiles(repoRoot)) {
    const src = readAddonCode(file, /* blankStrings */ false);
    let m: RegExpExecArray | null;
    toolIdRe.lastIndex = 0;
    while ((m = toolIdRe.exec(src)) !== null) {
      found.add(m[1]);
    }
  }

  return [...found].sort();
}
