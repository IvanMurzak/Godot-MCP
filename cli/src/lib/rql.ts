// `rql` — RQL text straight from the command line: which rql-* tool a query goes to, the text of a tool answer, hints
// for the RQL slips agents make most, and which files a save wrote. Never prints, never exits.

import * as fs from 'fs';
import * as path from 'path';

export const RQL_QUERY_TOOL = 'rql-query';
export const RQL_MUTATE_TOOL = 'rql-mutate';
export const RQL_SCHEMA_TOOL = 'rql-schema';

/** The query's top-level stages (split on `|` outside strings, parentheses, brackets and braces), trimmed. */
export function rqlStages(query: string): string[] {
  const stages: string[] = [];
  let depth = 0;
  let quote: string | undefined;
  let start = 0;
  for (let i = 0; i < query.length; i++) {
    const ch = query[i];
    if (quote) {
      if (ch === '\\') i++;
      else if (ch === quote) quote = undefined;
    } else if (ch === '"' || ch === '\'') {
      quote = ch;
    } else if (ch === '(' || ch === '[' || ch === '{') {
      depth++;
    } else if (ch === ')' || ch === ']' || ch === '}') {
      depth = Math.max(0, depth - 1);
    } else if (ch === '|' && depth === 0) {
      stages.push(query.slice(start, i).trim());
      start = i + 1;
    }
  }
  stages.push(query.slice(start).trim());
  return stages;
}

/** A query whose last stage is `set` or `call` changes the editor (rql-mutate); any other only reads (rql-query). */
export function isRqlMutation(query: string): boolean {
  const stages = rqlStages(query);
  return /^(set|call)\b/i.test(stages[stages.length - 1] ?? '');
}

/**
 * The query without a leading tool name: tool texts write "rql-query gameLog | ...", and agents paste that whole
 * into `rql`.
 */
export function stripToolName(query: string): string {
  return query.replace(/^\s*rql-(query|mutate)\s+/, '');
}

/**
 * The text blocks of a tool answer (`{ content: [{ type: "text", text }] }`) joined, or `undefined` when it has none —
 * an answer that is not all text keeps its JSON.
 */
export function toolResponseText(data: unknown): string | undefined {
  const content = (data as { content?: unknown } | null | undefined)?.content;
  if (!Array.isArray(content)) return undefined;
  const texts = content
    .filter((c): c is { type: string; text: string } => c?.type === 'text' && typeof c.text === 'string')
    .map((c) => c.text);
  return texts.length > 0 && texts.length === content.length ? texts.join('\n') : undefined;
}

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

/** A hint for an RQL error whose cause is a familiar slip (a camelCase operator, SQL `like`), else `undefined`. */
export function rqlErrorHint(message: string): string | undefined {
  const token = /unexpected '([^']+)'|unknown method '([^']+)'/.exec(message);
  const word = token?.[1] ?? token?.[2];
  if (word) {
    for (const [pattern, hint] of OPERATOR_HINTS) if (pattern.test(word)) return hint;
  }
  return undefined;
}

/** A query that may write files: a scene save, a new scene (saved at its path), a resource edit or creation. */
export function rqlMaySave(query: string): boolean {
  return /\b(SaveScene|NewScene|CreateResource)\s*\(|godot\.resource\(/.test(query);
}

/** The Godot files a save writes: scenes and resources. */
const SAVED_FILE = /\.(tscn|scn|tres|res)$/i;

/**
 * The scene / resource files of the project (dot folders such as `.godot` and `addons/` left out) modified at or after
 * `sinceMs`, as res:// paths, sorted — what a save call actually wrote. At most `limit`.
 */
export function resourcesWrittenSince(projectDir: string, sinceMs: number, limit = 20): string[] {
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
  return found.sort().slice(0, limit);
}
