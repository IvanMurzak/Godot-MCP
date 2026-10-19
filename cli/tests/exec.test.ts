import { afterEach, describe, expect, it, vi } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { EditorSessionError, type EnsureResult } from '@baizor/gamedev-cli-core';
import { buildScriptExecuteInput, execCode, readToolResponse, toolErrorText } from '../src/lib/exec.js';
import { createGodotSession, ensureEditor, sessionEditorEnv } from '../src/lib/session.js';
import type { RunToolFailure, RunToolOptions } from '../src/lib/types.js';

// No real process scan: no other editor runs for the temporary projects below.
vi.mock('../src/utils/godot-process.js', () => ({ findGodotProcess: vi.fn(() => null) }));

/** A closed local port: the readiness probe never answers. */
const DEAD_URL = 'http://127.0.0.1:9';

const dirs: string[] = [];
function tempProject(): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-exec-'));
  fs.writeFileSync(path.join(dir, 'project.godot'), 'config_version=5\n');
  dirs.push(dir);
  return dir;
}
afterEach(() => {
  for (const dir of dirs.splice(0)) fs.rmSync(dir, { recursive: true, force: true });
});

const ready = (url: string, source: EnsureResult<string>['source'] = 'started'): EnsureResult<string> => ({ ready: url, source });

describe('buildScriptExecuteInput', () => {
  it('leaves empty lists out', () => {
    expect(buildScriptExecuteInput('1 + 2', [], [])).toEqual({ csharpCode: '1 + 2' });
    expect(buildScriptExecuteInput('x', ['A.B'], ['helpers'])).toEqual({ csharpCode: 'x', usings: ['A.B'], include: ['helpers'] });
  });
});

describe('readToolResponse', () => {
  it('prints the RQL text content', () => {
    expect(readToolResponse({ status: 'success', content: [{ type: 'text', text: 'System.Int32: 3' }] }))
      .toEqual({ ok: true, text: 'System.Int32: 3' });
    expect(readToolResponse({ status: 'error', content: [{ type: 'text', text: 'boom' }] })).toEqual({ ok: false, text: 'boom' });
  });

  it('prints the JSON-format result the same way, then the logs', () => {
    const data = { status: 'success', structured: { result: { result: { name: 'result', typeName: 'System.Int32', value: 3 }, logs: ['hi'] } } };
    expect(readToolResponse(data)).toEqual({ ok: true, text: 'System.Int32: 3\nlogs:\nhi' });
    const text = { status: 'success', structured: { result: { result: { typeName: 'System.String', value: 'abc' } } } };
    expect(readToolResponse(text)).toEqual({ ok: true, text: 'System.String: abc' });
  });

  it('falls back to JSON', () => {
    expect(readToolResponse('plain')).toEqual({ ok: true, text: 'plain' });
    expect(readToolResponse({ status: 'success', content: [] })).toEqual({ ok: true, text: '{"status":"success","content":[]}' });
  });
});

describe('toolErrorText', () => {
  const failure = (data: unknown): RunToolFailure => ({ kind: 'failure', success: false, endpoint: 'x', reason: 'http-error', message: 'HTTP 500', data });
  it('prefers the server-reported error', () => {
    expect(toolErrorText(failure({ error: 'Compilation failed:\nsnippet.cs(2,5): error' }))).toBe('Compilation failed:\nsnippet.cs(2,5): error');
    expect(toolErrorText(failure('text'))).toBe('text');
    expect(toolErrorText(failure(undefined))).toBe('HTTP 500');
  });
});

describe('sessionEditorEnv', () => {
  it('connects the started editor to its local server, which it starts itself', () => {
    expect(sessionEditorEnv('http://localhost:5000', undefined)).toEqual({
      GODOT_MCP_CONNECTION_MODE: 'Custom',
      GODOT_MCP_HOST: 'http://localhost:5000',
      GODOT_MCP_START_SERVER: 'true',
    });
    expect(sessionEditorEnv('http://localhost:5000', 't')).toMatchObject({ GODOT_MCP_TOKEN: 't', GODOT_MCP_AUTH_OPTION: 'Required' });
  });
});

describe('createGodotSession', () => {
  it('defaults to the project local port and refuses a remote server', () => {
    const project = tempProject();
    expect(createGodotSession({ projectPath: project }).url).toMatch(/^http:\/\/localhost:\d+$/);
    expect(() => createGodotSession({ projectPath: project, url: 'https://ai-game.dev/mcp' })).toThrow(/local server only/);
  });
});

describe('ensureEditor / execCode', () => {
  it('builds before starting an editor, then runs the snippet on the ready server', async () => {
    const project = tempProject();
    const build = vi.fn(async () => ({ kind: 'success' as const, success: true as const, projectPath: project, skipped: false, warnings: [] }));
    const ensure = vi.fn(async () => ready('http://localhost:1234'));
    const invoke = vi.fn(async (_: RunToolOptions) => ({ kind: 'success' as const, success: true as const, endpoint: 'x', httpStatus: 200, data: { status: 'success' } }));
    const result = await execCode({ projectPath: project, url: DEAD_URL, code: '1 + 2', usings: ['A'], deps: { build, ensure, invoke } });

    expect(result).toMatchObject({ kind: 'success', baseUrl: 'http://localhost:1234', editor: 'started' });
    expect(build).toHaveBeenCalledOnce();
    expect(ensure.mock.calls[0]![1]).toBe('headless');
    expect(invoke.mock.calls[0]![0]).toMatchObject({ toolName: 'script-execute', url: 'http://localhost:1234', input: { csharpCode: '1 + 2', usings: ['A'] } });
  });

  it('skips the build when asked and starts with a window', async () => {
    const build = vi.fn();
    const ensure = vi.fn(async () => ready(DEAD_URL));
    await ensureEditor({ projectPath: tempProject(), url: DEAD_URL, build: false, window: true, deps: { build, ensure } });
    expect(build).not.toHaveBeenCalled();
    expect(ensure.mock.calls[0]![1]).toBe('window');
  });

  it('reports a failed build and does not start the editor', async () => {
    const ensure = vi.fn();
    const result = await ensureEditor({
      projectPath: tempProject(),
      url: DEAD_URL,
      deps: { ensure, build: async () => ({ kind: 'failure', success: false, warnings: [], errorMessage: 'CS1002', error: new Error('CS1002') }) },
    });
    expect(result).toMatchObject({ kind: 'failure', reason: 'build-failed', message: 'CS1002' });
    expect(ensure).not.toHaveBeenCalled();
  });

  it('maps session errors, with a hint for an editor that is not ours', async () => {
    const fail = (reason: ConstructorParameters<typeof EditorSessionError>[0]) =>
      ensureEditor({ projectPath: tempProject(), url: DEAD_URL, build: false, deps: { ensure: async () => { throw new EditorSessionError(reason, 'nope'); } } });
    expect(await fail('not-ready')).toMatchObject({ kind: 'failure', reason: 'not-ready', message: expect.stringContaining('AI Game Developer dock') });
    expect(await fail('exited')).toMatchObject({ kind: 'failure', reason: 'not-ready', message: 'nope' });
    expect(await fail('no-editor-binary')).toMatchObject({ kind: 'failure', reason: 'launch-failed' });
    expect(await ensureEditor({ projectPath: tempProject(), url: 'https://example.com' })).toMatchObject({ reason: 'invalid-options' });
  });

  it('two parallel cold calls start ONE editor (cli-core lease + startup lock)', async () => {
    const project = tempProject();
    let spawned = 0;
    const processOps = {
      spawnDetached: async () => ++spawned + 40_000,
      isAlive: () => true,
      processStart: async () => 'start',
      sleep: async () => {},
    };
    const call = () =>
      ensureEditor({ projectPath: project, url: DEAD_URL, editorPath: process.execPath, build: false, readyTimeoutMs: 300, processOps });
    const results = await Promise.all([call(), call()]);
    expect(spawned).toBe(1);
    // Neither got a ready server (nothing listens), but the second one waited for the first one's editor.
    expect(results.map((r) => r.kind)).toEqual(['failure', 'failure']);
    expect(createGodotSession({ projectPath: project, url: DEAD_URL }).session.readLease()?.pid).toBe(40_001);
  });
});
