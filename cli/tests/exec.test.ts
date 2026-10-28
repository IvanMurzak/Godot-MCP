import { afterEach, describe, expect, it, vi } from 'vitest';
import * as fs from 'fs';
import * as http from 'http';
import type { AddressInfo } from 'net';
import * as os from 'os';
import * as path from 'path';
import { EditorSessionError, readSessionLease, type EditorSession, type EnsureResult, type SessionLease } from '@baizor/gamedev-cli-core';
import { buildScriptExecuteInput, execCode, readToolResponse, toolErrorText } from '../src/lib/exec.js';
import { createGodotSession, ensureEditor, godotSessionAdapter, sessionEditorEnv } from '../src/lib/session.js';
import { runSystemTool } from '../src/lib/run-tool.js';
import { findGodotProcess } from '../src/utils/godot-process.js';
import type { RunToolFailure, RunToolOptions, RunToolResult } from '../src/lib/types.js';

// No real process scan: no other editor runs for the temporary projects below (unless a test says otherwise).
vi.mock('../src/utils/godot-process.js', () => ({ findGodotProcess: vi.fn(() => null) }));
// No real session-close call (the adapter's requestClose): refused unless a test sets a result.
const refused: RunToolResult = { kind: 'failure', success: false, endpoint: 'x', reason: 'connection-refused', message: 'refused' };
vi.mock('../src/lib/run-tool.js', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../src/lib/run-tool.js')>()),
  runSystemTool: vi.fn(),
}));

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
  vi.mocked(findGodotProcess).mockReset().mockReturnValue(null);
  vi.mocked(runSystemTool).mockReset().mockResolvedValue(refused);
});
vi.mocked(runSystemTool).mockResolvedValue(refused);

function lease(extra: Partial<SessionLease> = {}): SessionLease {
  return { pid: 40_001, nonce: 'n', engine: 'godot', mode: 'headless', startedAt: '2026-01-01T00:00:00Z', ownedBy: 'cli', hostId: os.hostname(), ...extra };
}

function writeLease(project: string, extra: Partial<SessionLease> = {}): void {
  fs.mkdirSync(path.join(project, '.ai-game-dev'), { recursive: true });
  fs.writeFileSync(path.join(project, '.ai-game-dev', 'session.json'), JSON.stringify(lease(extra)));
}

/** OS primitives of a fake editor: spawns count up from 40001; each lives until force-killed. */
function fakeProcessOps(order: string[] = []) {
  const killed = new Set<number>();
  let spawned = 0;
  return {
    get spawned() {
      return spawned;
    },
    ops: {
      spawnDetached: async () => {
        order.push('spawn');
        return ++spawned + 40_000;
      },
      isAlive: (pid: number) => !killed.has(pid),
      processStart: async () => 'start',
      forceKill: async (pid: number) => {
        killed.add(pid);
        return true;
      },
      sleep: async () => {},
    },
  };
}

/**
 * The session's own `ensure`, but a not-ready editor (nothing listens on DEAD_URL) is reported as a success with the
 * error's source, so `ensureEditor` does not stop it: its lease stays to be inspected.
 */
async function ensureKeepingEditor(session: EditorSession<string>, mode: 'headless' | 'window'): Promise<EnsureResult<string>> {
  try {
    return await session.ensure({ mode });
  } catch (err) {
    if (err instanceof EditorSessionError && err.source !== undefined) return ready(DEAD_URL, err.source);
    throw err;
  }
}

const builtOk = (projectPath: string) => ({ kind: 'success' as const, success: true as const, projectPath, skipped: false, warnings: [] });

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
      ...(process.env.DOTNET_ThreadPool_ThreadsToKeepAlive === undefined ? { DOTNET_ThreadPool_ThreadsToKeepAlive: '-1' } : {}),
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

describe('godotSessionAdapter', () => {
  it('reports a running editor by pid', async () => {
    const find = godotSessionAdapter(DEAD_URL, undefined).editor!.findRunningEditor!;
    vi.mocked(findGodotProcess).mockReturnValueOnce({ pid: 77, projectPath: 'p', commandLine: '' });
    expect(await find('p')).toBe(77);
    expect(await find('p')).toBe(false);
  });

  it('closes through session-close with the verified lease nonce, on the leased server', async () => {
    const ok = (status: string): RunToolResult => ({ kind: 'success', success: true, endpoint: 'x', httpStatus: 200, data: { status } });
    vi.mocked(runSystemTool).mockResolvedValue(ok('success'));
    const close = godotSessionAdapter('http://localhost:1', 't').editor!.requestClose!;
    expect(await close(5, 'p', lease({ serverUrl: 'http://localhost:2' }))).toBe(true);
    expect(vi.mocked(runSystemTool).mock.calls[0]![0]).toMatchObject({ toolName: 'session-close', url: 'http://localhost:2', token: 't', input: { nonce: 'n' } });
    // A lease written before cli-core 0.7.1 has no serverUrl: the session's own URL.
    await close(5, 'p', lease());
    expect(vi.mocked(runSystemTool).mock.calls[1]![0]).toMatchObject({ url: 'http://localhost:1' });
    expect(await close(5, 'p')).toBe(false);
    vi.mocked(runSystemTool).mockResolvedValue(ok('error'));
    expect(await close(5, 'p', lease())).toBe(false);
  });
});

describe('ensureEditor / execCode', () => {
  it('runs the snippet on the ready server', async () => {
    const ensure = vi.fn(async () => ready('http://localhost:1234'));
    const invoke = vi.fn(async (_: RunToolOptions) => ({ kind: 'success' as const, success: true as const, endpoint: 'x', httpStatus: 200, data: { status: 'success' } }));
    const result = await execCode({ projectPath: tempProject(), url: DEAD_URL, code: '1 + 2', usings: ['A'], deps: { ensure, invoke } });

    expect(result).toMatchObject({ kind: 'success', baseUrl: 'http://localhost:1234', editor: 'started' });
    expect(ensure.mock.calls[0]![1]).toBe('headless');
    expect(invoke.mock.calls[0]![0]).toMatchObject({ toolName: 'script-execute', url: 'http://localhost:1234', input: { csharpCode: '1 + 2', usings: ['A'] } });
  });

  it('builds right before starting the editor; an editor that never answers is stopped', async () => {
    const project = tempProject();
    const order: string[] = [];
    const fake = fakeProcessOps(order);
    const build = vi.fn(async (p: string) => {
      order.push('build');
      return builtOk(p);
    });
    const result = await ensureEditor({ projectPath: project, url: DEAD_URL, editorPath: process.execPath, readyTimeoutMs: 50, processOps: fake.ops, deps: { build } });

    expect(order).toEqual(['build', 'spawn']);
    expect(build).toHaveBeenCalledWith(path.resolve(project));
    expect(result).toMatchObject({ kind: 'failure', reason: 'not-ready', message: expect.stringContaining('stopped the unresponsive editor, PID 40001') });
  });

  it('records the server and connection mode in the lease and gitignores the session files', async () => {
    const project = tempProject();
    await ensureEditor({ projectPath: project, url: DEAD_URL, editorPath: process.execPath, build: false, readyTimeoutMs: 50, processOps: fakeProcessOps().ops, deps: { ensure: ensureKeepingEditor } });
    expect(readSessionLease(project)).toMatchObject({ pid: 40_001, serverUrl: DEAD_URL, connectionMode: 'Custom' });
    expect(fs.readFileSync(path.join(project, '.ai-game-dev', '.gitignore'), 'utf-8')).toContain('session.json');
  });

  it('skips the build when asked and starts with a window', async () => {
    const build = vi.fn();
    const fake = fakeProcessOps();
    const ensure = vi.fn((session: EditorSession<string>, mode: 'headless' | 'window') => session.ensure({ mode }));
    await ensureEditor({
      projectPath: tempProject(), url: DEAD_URL, editorPath: process.execPath, build: false, window: true, readyTimeoutMs: 50,
      processOps: fake.ops, deps: { build, ensure },
    });
    expect(build).not.toHaveBeenCalled();
    expect(fake.spawned).toBe(1);
    expect(ensure.mock.calls[0]![1]).toBe('window');
  });

  it('reports a failed build and does not start the editor', async () => {
    const project = tempProject();
    const fake = fakeProcessOps();
    const result = await ensureEditor({
      projectPath: project,
      url: DEAD_URL,
      editorPath: process.execPath,
      processOps: fake.ops,
      deps: { build: async () => ({ kind: 'failure', success: false, warnings: [], errorMessage: 'CS1002', error: new Error('CS1002') }) },
    });
    expect(result).toMatchObject({ kind: 'failure', reason: 'build-failed', message: 'CS1002' });
    expect(fake.spawned).toBe(0);
    expect(readSessionLease(project)).toBeUndefined();
  });

  it('maps session errors, with a hint for an editor that is not ours', async () => {
    const fail = (reason: ConstructorParameters<typeof EditorSessionError>[0], source?: 'foreign' | 'started' | 'owned') =>
      ensureEditor({
        projectPath: tempProject(), url: DEAD_URL, build: false,
        deps: { ensure: async () => { throw new EditorSessionError(reason, 'nope', source ? { source } : {}); } },
      });
    expect(await fail('not-ready', 'foreign')).toMatchObject({ kind: 'failure', reason: 'not-ready', message: expect.stringContaining('AI Game Developer dock') });
    expect(await fail('not-ready', 'started')).toMatchObject({ kind: 'failure', reason: 'not-ready', message: 'nope' });
    expect(await fail('not-ready', 'owned')).toMatchObject({ kind: 'failure', reason: 'not-ready', message: expect.stringContaining('close --owned-only --force') });
    expect(await fail('exited')).toMatchObject({ kind: 'failure', reason: 'not-ready', message: 'nope' });
    expect(await fail('no-editor-binary')).toMatchObject({ kind: 'failure', reason: 'launch-failed' });
    expect(await ensureEditor({ projectPath: tempProject(), url: 'https://example.com' })).toMatchObject({ reason: 'invalid-options' });
  });

  it('reports a foreign editor that does not answer, without building or starting one', async () => {
    vi.mocked(findGodotProcess).mockReturnValue({ pid: 99, projectPath: 'p', commandLine: '' });
    const fake = fakeProcessOps();
    const build = vi.fn(async (p: string) => builtOk(p));
    const result = await ensureEditor({ projectPath: tempProject(), url: DEAD_URL, editorPath: process.execPath, readyTimeoutMs: 50, processOps: fake.ops, deps: { build } });
    expect(result).toMatchObject({ kind: 'failure', reason: 'not-ready', message: expect.stringContaining('AI Game Developer dock') });
    expect(build).not.toHaveBeenCalled();
    expect(fake.spawned).toBe(0);
  });

  describe('the server of a leased editor', () => {
    /** A local server that answers every readiness probe. */
    async function answeringServer(): Promise<{ url: string; close: () => Promise<void> }> {
      const server = http.createServer((_req, res) => res.writeHead(200, { 'Content-Type': 'application/json' }).end('{}'));
      await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
      const { port } = server.address() as AddressInfo;
      return { url: `http://127.0.0.1:${port}`, close: () => new Promise((resolve) => server.close(() => resolve())) };
    }
    const urlOf = (r: Awaited<ReturnType<typeof ensureEditor>>) => (r.kind === 'success' ? r.godot.url : undefined);
    const capture = async (session: EditorSession<string>) => ready(session.projectRoot, 'running');

    it('is used once cli-core verified the lease live, over --url', async () => {
      const project = tempProject();
      writeLease(project, { serverUrl: 'http://localhost:4321', processStart: 'start' });
      const result = await ensureEditor({ projectPath: project, url: DEAD_URL, processOps: fakeProcessOps().ops, deps: { ensure: capture } });
      expect(urlOf(result)).toBe('http://localhost:4321');
    });

    it('is ignored for a reused pid (another process start time)', async () => {
      const project = tempProject();
      writeLease(project, { serverUrl: 'http://localhost:4321', processStart: 'earlier' });
      const result = await ensureEditor({ projectPath: project, url: DEAD_URL, processOps: fakeProcessOps().ops, deps: { ensure: capture } });
      expect(urlOf(result)).toBe(DEAD_URL);
    });

    it('a crashed editor whose port another editor now serves: the code never runs there', async () => {
      const other = await answeringServer();
      try {
        const project = tempProject();
        writeLease(project, { serverUrl: other.url, processStart: 'start' });
        const fake = fakeProcessOps();
        const ops = { ...fake.ops, isAlive: (pid: number) => pid !== 40_001 && fake.ops.isAlive(pid) };
        const invoke = vi.fn();
        const result = await execCode({
          projectPath: project, code: '1', editorPath: process.execPath, build: false, readyTimeoutMs: 50, processOps: ops, deps: { invoke },
        });
        expect(result.kind).toBe('failure');
        expect(invoke).not.toHaveBeenCalled();
        // It started its own editor on the project's port instead (which nothing answers in this test).
        expect(fake.spawned).toBe(1);
      } finally {
        await other.close();
      }
    });
  });

  it('two parallel cold calls build once and start ONE editor (cli-core lease + startup lock)', async () => {
    const project = tempProject();
    const fake = fakeProcessOps();
    const build = vi.fn(async (p: string) => builtOk(p));
    const call = () =>
      ensureEditor({ projectPath: project, url: DEAD_URL, editorPath: process.execPath, readyTimeoutMs: 300, processOps: fake.ops, deps: { build, ensure: ensureKeepingEditor } });
    const results = await Promise.all([call(), call()]);
    expect(fake.spawned).toBe(1);
    expect(build).toHaveBeenCalledOnce();
    // Neither got a ready server (nothing listens), but the second one waited for the first one's editor.
    expect(results.map((r) => (r.kind === 'success' ? r.ensured.source : r.reason)).sort()).toEqual(['owned', 'started']);
    expect(readSessionLease(project)?.pid).toBe(40_001);
  });
});
