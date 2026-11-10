import { afterEach, describe, expect, it } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { isRqlMutation, resourcesWrittenSince, rqlErrorHint, rqlMaySave, rqlStages, rqlToolCall, saveReceipt, stripToolName } from '../src/lib/rql.js';
import { toolResponseText } from '../src/lib/exec.js';

const dirs: string[] = [];
afterEach(() => {
  for (const dir of dirs.splice(0)) fs.rmSync(dir, { recursive: true, force: true });
});

describe('rql query routing', () => {
  it('splits stages only on top-level pipes', () => {
    expect(rqlStages('nodes | where name == "a|b" | select [1, (2 | 3)]')).toEqual(['nodes', 'where name == "a|b"', 'select [1, (2 | 3)]']);
  });

  it('sends a query ending in set / call to rql-mutate, any other to rql-query', () => {
    expect(isRqlMutation('nodes | where name starts-with "Tree" | set Visible = false')).toBe(true);
    expect(isRqlMutation('editor | call SaveScene()')).toBe(true);
    expect(isRqlMutation('nodes | where name starts-with "Tree" | count')).toBe(false);
    expect(isRqlMutation('nodes | where name == "set | call" | select name')).toBe(false);
  });

  it('knows which queries may write files and drops a pasted tool name', () => {
    expect(rqlMaySave('editor | call SaveScene()')).toBe(true);
    expect(rqlMaySave('godot.resource("res://m.tres") | set props["albedo_color"] = "#ff0000"')).toBe(true);
    expect(rqlMaySave('editor | call OpenScene("res://a.tscn")')).toBe(false);
    expect(stripToolName('rql-query gameLog | count')).toBe('gameLog | count');
  });
});

describe('toolResponseText / rqlErrorHint', () => {
  it('returns the text of an all-text answer and leaves other answers to JSON', () => {
    expect(toolResponseText({ status: 'success', content: [{ type: 'text', text: '3' }] })).toBe('3');
    expect(toolResponseText({ content: [{ type: 'image', data: 'AAA' }] })).toBeUndefined();
  });

  it('names the RQL spelling of an operator from another language', () => {
    expect(rqlErrorHint('rql-query', "error syntax-error: unexpected 'startsWith' in 'where' stage at 20")).toMatch(/starts-with/);
    expect(rqlErrorHint('rql-query', 'error unknown-source: unknown source')).toBeUndefined();
  });
});

describe('resourcesWrittenSince', () => {
  it('lists the scenes / resources written since a time, as res:// paths, skipping .godot and addons', () => {
    const project = fs.mkdtempSync(path.join(os.tmpdir(), 'rql-godot-'));
    dirs.push(project);
    for (const dir of ['scenes', '.godot/imported', 'addons/x']) fs.mkdirSync(path.join(project, dir), { recursive: true });
    const old = path.join(project, 'scenes', 'old.tscn');
    fs.writeFileSync(old, '');
    const past = new Date(Date.now() - 60_000);
    fs.utimesSync(old, past, past);
    const since = Date.now() - 1000;
    fs.writeFileSync(path.join(project, 'scenes', 'main.tscn'), '');
    fs.writeFileSync(path.join(project, '.godot', 'imported', 'x.res'), '');
    fs.writeFileSync(path.join(project, 'addons', 'x', 'y.tres'), '');
    fs.writeFileSync(path.join(project, 'red.tres'), '');
    expect(resourcesWrittenSince(project, since)).toEqual(['res://red.tres', 'res://scenes/main.tscn']);
  });
});

describe('rqlToolCall / saveReceipt', () => {
  it('routes a query, and refuses a query with --schema or a read-only query with mutate flags', () => {
    expect(rqlToolCall('nodes | count', {})).toEqual({ toolName: 'rql-query', input: { query: 'nodes | count' } });
    expect(rqlToolCall('nodes | set Visible = false', { dryRun: true, maxTargets: 300 }))
      .toEqual({ toolName: 'rql-mutate', input: { query: 'nodes | set Visible = false', dry_run: true, max_targets: 300 } });
    expect(rqlToolCall(undefined, { schema: 'Node3D' })).toEqual({ toolName: 'rql-schema', input: { type: 'Node3D' } });
    expect(rqlToolCall(undefined, { schema: 'nodes | count' })).toHaveProperty('usage');
    expect(rqlToolCall('nodes | count', { dryRun: true })).toHaveProperty('usage');
  });

  it('ignores string literals and backtick identifiers', () => {
    expect(rqlMaySave('nodes | where name == "SaveScene(" | set Visible = false')).toBe(false);
    expect(isRqlMutation("nodes | where `Bob's` == 1 | set Visible = false")).toBe(true);
    expect(rqlErrorHint('script-execute', "unexpected 'startsWith'")).toBeUndefined();
  });

  it('names the files a save wrote', () => {
    expect(saveReceipt(['res://a.tscn'])).toBe('Saved to disk: res://a.tscn');
    expect(saveReceipt(['a', 'b', 'c'], 2)).toBe('Saved to disk: a, b, +1 more');
    expect(saveReceipt([])).toMatch(/No scene or resource file/);
  });
});
