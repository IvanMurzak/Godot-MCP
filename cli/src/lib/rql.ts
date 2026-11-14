// `rql` — RQL text straight from the command line: which rql-* tool a query goes to, hints for the RQL slips agents
// make most, and which files a save wrote. Never prints, never exits.

import * as fs from 'fs';
import * as path from 'path';

import {
  RQL_MUTATE_TOOL,
  RQL_QUERY_TOOL,
  RQL_SCHEMA_TOOL,
  decodeQueryFile,
  isRqlMutation,
  rqlStages,
  rqlToolCall,
  rqlToolFor,
  splitRqlStatements,
  stripToolName,
} from '@baizor/gamedev-cli-core';

export {
  RQL_MUTATE_TOOL,
  RQL_QUERY_TOOL,
  RQL_SCHEMA_TOOL,
  decodeQueryFile,
  isRqlMutation,
  rqlStages,
  rqlToolCall,
  rqlToolFor,
  splitRqlStatements,
  stripToolName,
};

/** `rql` without a query (core's {@link rqlToolCall} returns it as the usage error). */
export const RQL_NO_QUERY_USAGE = 'No query given: godot-cli rql \'nodes | where name starts-with "Tree" | count\' (recipes: rql --help)';

/** Operators agents bring from other languages, with RQL's spelling. */
const OPERATOR_HINTS: ReadonlyArray<readonly [RegExp, string]> = [
  [/^startswith$/i, 'RQL spells it starts-with: where name starts-with "Tree"'],
  [/^endswith$/i, 'RQL spells it ends-with: where name ends-with "_lod0"'],
  [/^(like|ilike)$/i, 'RQL has no like: use starts-with / ends-with / contains, or matches "regex"'],
  [/^(includes|has)$/i, 'RQL spells it contains: where name contains "Tree"'],
  [/^(isnull|is_null)$/i, 'RQL spells it x is null / x is not null'],
  [/^(notin|not_in)$/i, 'RQL spells it not (x in [a, b])'],
  [/^(regex|regexp|match|rlike)$/i, 'RQL spells it matches "regex": where name matches "^Tree[0-9]+$"'],
];

/**
 * A hint for a failed RQL tool (`toolName` rql-*) whose error is a familiar slip (an operator spelled as in another
 * language), else `undefined`.
 */
export function rqlErrorHint(toolName: string, message: string): string | undefined {
  if (!toolName.startsWith('rql-')) return undefined;
  const token = /unexpected '([^']+)'|unknown method '([^']+)'/.exec(message);
  const word = token?.[1] ?? token?.[2];
  if (word) {
    for (const [pattern, hint] of OPERATOR_HINTS) if (pattern.test(word)) return hint;
  }
  return undefined;
}

/** A query that saves files: a scene save, a new scene (saved at its path), a resource edit or creation. */
export function rqlMaySave(query: string): boolean {
  // Outside string literals and `identifiers`: where name == "SaveScene(" saves nothing.
  const code = query.replace(/"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`[^`]*`/g, '""');
  return /\b(SaveScenes?|SaveAllScenes|NewScene|CreateResource|Save)\s*\(|godot\.resources?\(/.test(code);
}

/** The Godot files a save writes: scenes and resources. */
const SAVED_FILE = /\.(tscn|scn|tres|res)$/i;

/**
 * The scene / resource files of the project (dot folders such as `.godot` and `addons/` left out) modified at or after
 * `sinceMs`, as res:// paths, sorted — what a save call actually wrote.
 */
export function resourcesWrittenSince(projectDir: string, sinceMs: number): string[] {
  const found: string[] = [];
  const walk = (dir: string): void => {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (!entry.name.startsWith('.') && !(dir === projectDir && entry.name === 'addons')) walk(full);
      } else if (SAVED_FILE.test(entry.name)) {
        try {
          if (fs.statSync(full).mtimeMs >= sinceMs) found.push(`res://${path.relative(projectDir, full).split(path.sep).join('/')}`);
        } catch {
          /* removed meanwhile */
        }
      }
    }
  };
  walk(projectDir);
  return found.sort();
}

/**
 * The line a change prints: the files it wrote (the first `limit`, then how many more). When it wrote none: that, for a
 * save (`maySave`); nothing for any other change (scene edits stay unsaved until SaveScene()).
 */
export function saveReceipt(written: readonly string[], maySave: boolean, limit = 20): string | undefined {
  if (written.length === 0) return maySave ? 'No scene or resource file of the project changed on disk.' : undefined;
  const more = written.length > limit ? `, +${written.length - limit} more` : '';
  return `Saved to disk: ${written.slice(0, limit).join(', ')}${more}`;
}
