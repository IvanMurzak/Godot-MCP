import { afterEach, describe, expect, it } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { decodeQueryFile, isRqlChange, isRqlMutation, resourcesWrittenSince, rqlErrorHint, rqlMaySave, rqlStages, rqlStatements, rqlToolCall, saveReceipt, stripToolName } from '../src/lib/rql.js';
import { toolResponseText, withoutImageData } from '../src/lib/exec.js';

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

  it('splits statements at ; outside strings, identifiers and brackets, and routes a change of several to rql-mutate', () => {
    expect(rqlStatements('scenes | select name; nodes | count;')).toEqual(['scenes | select name', 'nodes | count']);
    expect(rqlStatements('nodes | where name == "a;b" | count')).toEqual(['nodes | where name == "a;b" | count']);
    expect(rqlStatements("nodes | where `x;y` == 1 | select [1; 2] | where name == 'it\\'s;'")).toHaveLength(1);
    expect(rqlStatements('nodes | where name == "open; | count')).toHaveLength(1);
    // The last statement reads, an earlier one writes: still a change (the old last-stage rule sent it to rql-query).
    const text = 'nodes | where name == "A" | set Visible = false; nodes | count';
    expect(isRqlMutation(text)).toBe(false);
    expect(isRqlChange(text)).toBe(true);
    expect(rqlToolCall(text, { dryRun: true })).toEqual({ toolName: 'rql-mutate', input: { query: text, dry_run: true } });
    expect(rqlToolCall('scenes | count; nodes | count', {})).toEqual({ toolName: 'rql-query', input: { query: 'scenes | count; nodes | count' } });
    expect(isRqlChange('nodes | where name == "x; | set a = 1" | count')).toBe(false);
    expect(rqlStatements(' ; ;')).toEqual([]);
  });

  it('ignores string literals and backtick identifiers', () => {
    expect(rqlMaySave('nodes | where name == "SaveScene(" | set Visible = false')).toBe(false);
    expect(isRqlMutation("nodes | where `Bob's` == 1 | set Visible = false")).toBe(true);
    expect(rqlErrorHint('script-execute', "unexpected 'startsWith'")).toBeUndefined();
  });

  it('names the files a save wrote', () => {
    expect(saveReceipt(['res://a.tscn'], true)).toBe('Saved to disk: res://a.tscn');
    expect(saveReceipt(['a', 'b', 'c'], false, 2)).toBe('Saved to disk: a, b, +1 more');
    expect(saveReceipt([], true)).toMatch(/No scene or resource file/);
    expect(saveReceipt([], false)).toBeUndefined();
  });

  it('reads || as or, decodes a UTF-16 query file, and knows the other save calls', () => {
    expect(isRqlMutation('nodes | where name == "a" || name == "b" | set Visible = false')).toBe(true);
    const text = 'nodes | count';
    expect(decodeQueryFile(Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(text, 'utf16le')]))).toBe(text);
    expect(rqlMaySave('editor | call SaveAllScenes()')).toBe(true);
    expect(rqlMaySave('godot.resources("Material") | set props["x"] = 1')).toBe(true);
  });
});

describe('withoutImageData', () => {
  it('replaces only image data with a size note, keeping the text and the rest of the answer', () => {
    const answer = { status: 'success', content: [{ type: 'text', text: 'Saved to x.png' }, { type: 'image', mimeType: 'image/png', data: 'A'.repeat(4000) }] };
    const printed = withoutImageData(answer) as typeof answer;
    expect(printed.status).toBe('success');
    expect(printed.content[0]).toEqual(answer.content[0]);
    expect(printed.content[1]!.mimeType).toBe('image/png');
    expect(printed.content[1]!.data).toMatch(/^<4000 base64 chars omitted/);
    expect(answer.content[1]!.data).toHaveLength(4000);
  });

  it('leaves an answer without content as it is', () => {
    const answer = { error: 'boom' };
    expect(withoutImageData(answer)).toBe(answer);
  });
});
