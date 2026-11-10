// `rql` — RQL text straight from the command line: which rql-* tool a query goes to, hints for the RQL slips agents
// make most, and which files a save wrote. Never prints, never exits.

import * as fs from 'fs';
import * as path from 'path';

export const RQL_QUERY_TOOL = 'rql-query';
export const RQL_MUTATE_TOOL = 'rql-mutate';
export const RQL_SCHEMA_TOOL = 'rql-schema';

/**
 * The query's top-level stages (split on `|` outside strings, `backtick` identifiers, parentheses, brackets and
 * braces; `||` is RQL's or, not a stage break), trimmed.
 */
export function rqlStages(query: string): string[] {
  const stages: string[] = [];
  let depth = 0;
  let quote: string | undefined;
  let start = 0;
  for (let i = 0; i < query.length; i++) {
    const ch = query[i];
    if (quote) {
      if (ch === '\\' && quote !== '`') i++;
      else if (ch === quote) quote = undefined;
    } else if (ch === '"' || ch === '\'' || ch === '`') {
      quote = ch;
    } else if (ch === '(' || ch === '[' || ch === '{') {
      depth++;
    } else if (ch === ')' || ch === ']' || ch === '}') {
      depth = Math.max(0, depth - 1);
    } else if (ch === '|' && query[i + 1] === '|') {
      i++;
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

/** A query file's text: UTF-8, or UTF-16 with a byte-order mark (Windows PowerShell 5.1 writes `>` files so). */
export function decodeQueryFile(bytes: Buffer): string {
  if (bytes[0] === 0xff && bytes[1] === 0xfe) return bytes.subarray(2).toString('utf16le');
  if (bytes[0] === 0xfe && bytes[1] === 0xff) return Buffer.from(bytes.subarray(2)).swap16().toString('utf16le');
  return bytes.toString('utf-8').replace(/^﻿/, '');
}

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
  if (!isRqlMutation(query)) {
    if (dryRun || maxTargets !== undefined) return { usage: '--dry-run and --max-targets are for a query ending in set / call.' };
    return { toolName: RQL_QUERY_TOOL, input: { query } };
  }
  return { toolName: RQL_MUTATE_TOOL, input: { query, ...(dryRun ? { dry_run: true } : {}), ...(maxTargets !== undefined ? { max_targets: maxTargets } : {}) } };
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
