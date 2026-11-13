// `rql` — RQL text straight from the command line: which rql-* tool a query goes to, hints for the RQL slips agents
// make most, and which files a save wrote. Never prints, never exits.

import * as fs from 'fs';
import * as path from 'path';

import { RQL_MUTATE_TOOL, RQL_QUERY_TOOL, RQL_SCHEMA_TOOL, decodeQueryFile, isRqlMutation, rqlStages, stripToolName } from '@baizor/gamedev-cli-core';

export { RQL_MUTATE_TOOL, RQL_QUERY_TOOL, RQL_SCHEMA_TOOL, decodeQueryFile, isRqlMutation, rqlStages, stripToolName };

export interface RqlCallOptions {
  /** `--schema [type]`: true without a type. */
  schema?: string | boolean;
  dryRun?: boolean;
  maxTargets?: number;
}

/** The rql-* tool and its input for a command-line query, or why the command line is wrong. */
export function rqlToolCall(query: string | undefined, options: RqlCallOptions):
  { toolName: string; input: Record<string, unknown> } | { usage: string } {
  const { schema, dryRun, maxTargets } = options;
  if (schema !== undefined) {
    // `rql --schema 'nodes | count'` hands the query to --schema as its value.
    if (query !== undefined || (typeof schema === 'string' && /[|\s]/.test(schema.trim()))) {
      return { usage: 'Pass either a query or --schema [type] (a type name), not both.' };
    }
    return { toolName: RQL_SCHEMA_TOOL, input: typeof schema === 'string' ? { type: schema } : {} };
  }
  if (query === undefined || query.trim() === '') {
    return { usage: 'No query given: godot-cli rql \'nodes | where name starts-with "Tree" | count\' (recipes: rql --help)' };
  }
  if (!isRqlChange(query)) {
    if (dryRun || maxTargets !== undefined) return { usage: '--dry-run and --max-targets are for a query ending in set / call.' };
    return { toolName: RQL_QUERY_TOOL, input: { query } };
  }
  return { toolName: RQL_MUTATE_TOOL, input: { query, ...(dryRun ? { dry_run: true } : {}), ...(maxTargets !== undefined ? { max_targets: maxTargets } : {}) } };
}

/**
 * The statements of RQL text: split at each `;` outside strings, quoted identifiers and brackets, blank ones dropped (RQL
 * spec §14; the same scan as RQL's splitStatements, unterminated quotes run to the end; blank-only text gives none).
 */
export function rqlStatements(text: string): string[] {
  const statements: string[] = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '"' || c === "'" || c === '`') {
      const quote = c;
      for (i++; i < text.length && text[i] !== quote; i++) if (quote !== '`' && text[i] === '\\') i++;
    } else if (c === '(' || c === '[' || c === '{') depth++;
    else if (c === ')' || c === ']' || c === '}') depth = Math.max(0, depth - 1);
    else if (c === ';' && depth === 0) {
      statements.push(text.slice(start, i).trim());
      start = i + 1;
    }
  }
  statements.push(text.slice(start).trim());
  return statements.filter((s) => s !== '');
}

/**
 * Whether RQL text changes the editor (goes to rql-mutate): any of its `;`-separated statements ends in set / call. rql-mutate
 * then refuses text whose other statements do not, before anything runs.
 */
export function isRqlChange(query: string): boolean {
  return rqlStatements(query).some(isRqlMutation);
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
