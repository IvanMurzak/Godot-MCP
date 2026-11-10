import { afterEach, describe, expect, it } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { isRqlMutation, resourcesWrittenSince, rqlErrorHint, rqlMaySave, rqlStages, stripToolName, toolResponseText } from '../src/lib/rql.js';

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
    expect(rqlErrorHint("error syntax-error: unexpected 'startsWith' in 'where' stage at 20")).toMatch(/starts-with/);
    expect(rqlErrorHint('error unknown-source: unknown source')).toBeUndefined();
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
