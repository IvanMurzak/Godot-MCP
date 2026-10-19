// Godot editor sessions on the shared cli-core EditorSession (Code-Exec plan §4.6): reuse any running editor of
// the project, otherwise start our own (headless by default) connected to the project's LOCAL MCP server, with a
// lease so only our own editor is ever closed. Library-safe: no console output, no `process.exit`.

import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import {
  defaultSessionProcessOps,
  EditorSession,
  EditorSessionError,
  CredentialLockBusyError,
  MachineCredentialLock,
  godotAdapter,
  mcpReadinessProbe,
  SESSION_DIR_NAME,
  type EngineAdapter,
  type EnsureResult,
  type SessionProcessOps,
} from '@baizor/gamedev-cli-core';
import { buildProject } from './build.js';
import { runSystemTool } from './run-tool.js';
import { findGodotBinary } from '../utils/godot-editor.js';
import { findGodotProcess } from '../utils/godot-process.js';
import { isLocalhostUrl } from '../utils/project-marker.js';
import {
  ENV_AUTH_OPTION,
  ENV_CONNECTION_MODE,
  ENV_HOST,
  ENV_START_SERVER,
  ENV_TOKEN,
  localProjectUrl,
} from '../utils/connection.js';
import type { BuildProjectResult } from './types.js';

/** The plugin's system tool that saves and quits a CLI-started editor (a windowless one gets no WM_CLOSE). */
export const SESSION_CLOSE_TOOL = 'session-close';

/** Serializes the pre-start C# build of a project (beside the session lock). */
const BUILD_LOCK_FILE_NAME = 'build.lock';

/** How long to wait for a user's own editor whose local server does not answer yet (it may still be loading). */
const FOREIGN_READY_TIMEOUT_MS = 20_000;

export interface GodotSessionOptions {
  projectPath: string;
  /**
   * The server the editor's plugin connects to and the session probes. Default: the project's local port
   * (`http://localhost:<derived>`). A non-loopback URL is rejected: headless sessions are local only.
   */
  url?: string;
  /** Bearer token: the started editor uses it (auth Required) and every probe / call sends it. */
  token?: string;
  /** Explicit Godot executable (else GODOT_BIN / PATH / common install locations). */
  editorPath?: string;
  /** A started editor quits after this many seconds without tool calls. `0` = never. Default 300. */
  idleSeconds?: number;
  /** How long to wait for a started editor's MCP server. Default 300000. */
  readyTimeoutMs?: number;
  onStatus?: (message: string) => void;
  /** Tests: OS process primitives. */
  processOps?: Partial<SessionProcessOps>;
}

export interface GodotSession {
  session: EditorSession<string>;
  /** One readiness check of the local server (the session's warm-path probe): the URL, or undefined. */
  probe: () => Promise<string | undefined>;
  /** The local server URL the session probes and a started editor binds. */
  url: string;
}

/**
 * Environment of an editor the session starts: Custom mode on the local URL, the plugin starts the local server
 * (there is no dock button to press in a headless editor), and the token when one is given.
 */
export function sessionEditorEnv(url: string, token: string | undefined): Record<string, string> {
  return {
    [ENV_CONNECTION_MODE]: 'Custom',
    [ENV_HOST]: url,
    [ENV_START_SERVER]: 'true',
    ...(token ? { [ENV_TOKEN]: token, [ENV_AUTH_OPTION]: 'Required' } : {}),
  };
}

/** The Godot adapter with the CLI's hooks: running-editor detection and the plugin-side graceful close. */
export function godotSessionAdapter(url: string, token: string | undefined, readNonce: () => string | undefined): EngineAdapter {
  return {
    ...godotAdapter,
    editor: {
      ...godotAdapter.editor!,
      findRunningEditor: async (projectRoot) => findGodotProcess(projectRoot) !== null,
      requestClose: async () => {
        const nonce = readNonce();
        if (nonce === undefined) return false;
        const result = await runSystemTool({
          toolName: SESSION_CLOSE_TOOL,
          url,
          ...(token ? { token } : {}),
          input: { nonce },
          timeoutMs: 15_000,
        });
        // A refusing tool (wrong nonce, an addon without session-close) answers 200 with status "error".
        return result.kind === 'success' && (result.data as { status?: string } | null)?.status !== 'error';
      },
    },
  };
}

/** Build the session of a Godot project. Throws when `url` is not local. */
export function createGodotSession(options: GodotSessionOptions): GodotSession {
  const projectRoot = path.resolve(options.projectPath);
  const url = (options.url ?? localProjectUrl(projectRoot)).replace(/\/+$/, '');
  if (!isLocalhostUrl(url)) {
    throw new Error(`editor sessions connect to a local server only; ${url} is not on this machine`);
  }
  const probe = mcpReadinessProbe({ urls: [url], ...(options.token ? { token: options.token } : {}) });
  const session: EditorSession<string> = new EditorSession<string>({
    // requestClose runs long after construction: it reads the lease of this very session.
    adapter: godotSessionAdapter(url, options.token, () => session.readLease()?.nonce),
    projectRoot,
    probeReady: probe,
    editorBinary: () => {
      const binary = findGodotBinary(options.editorPath);
      if (!binary) throw new Error('no Godot editor binary found (pass --editor-path or set GODOT_BIN)');
      return binary;
    },
    env: sessionEditorEnv(url, options.token),
    ...(options.idleSeconds !== undefined ? { idleSeconds: options.idleSeconds } : {}),
    ...(options.readyTimeoutMs !== undefined ? { readyTimeoutMs: options.readyTimeoutMs } : {}),
    ...(options.onStatus ? { onStatus: options.onStatus } : {}),
    ...(options.processOps ? { processOps: options.processOps } : {}),
  });
  return { session, probe, url };
}

/**
 * Build the C# assembly before an editor is started (Godot does not build it in a headless editor, and a missing
 * assembly disables the addon). Serialized per project, so parallel cold calls do not run two MSBuilds on one obj/
 * folder; the second one finds an up-to-date build.
 */
export async function buildBeforeStart(projectPath: string): Promise<BuildProjectResult> {
  const dir = path.join(path.resolve(projectPath), SESSION_DIR_NAME);
  removeDeadBuildLock(path.join(dir, BUILD_LOCK_FILE_NAME));
  const lock = new MachineCredentialLock(dir, {
    lockFileName: BUILD_LOCK_FILE_NAME,
    staleMs: 600_000,
    acquireBudgetMs: 900_000,
  });
  return lock.withLock(() => buildProject({ projectPath }));
}

/** Injection points for tests. */
export interface EnsureEditorDeps {
  ensure: (session: EditorSession<string>, mode: 'headless' | 'window') => Promise<EnsureResult<string>>;
  build: (projectPath: string) => Promise<BuildProjectResult>;
}

export interface EnsureEditorOptions extends GodotSessionOptions {
  /** Start the editor with a window instead of headless (only when none is running yet). */
  window?: boolean;
  /** Build the C# assembly before starting an editor. Default true. */
  build?: boolean;
  deps?: Partial<EnsureEditorDeps>;
}

export type EnsureEditorResult =
  | { kind: 'success'; success: true; godot: GodotSession; ensured: EnsureResult<string> }
  | {
      kind: 'failure';
      success: false;
      /**
       * `invalid-options`: e.g. a non-local URL; `build-failed`: the C# build before the start failed;
       * `launch-failed`: no editor could be started; `not-ready`: its server never answered.
       */
      reason: 'invalid-options' | 'build-failed' | 'launch-failed' | 'not-ready';
      message: string;
      error?: Error;
    };

/**
 * A ready editor of the project (the `exec` / `open --headless` policy): reuse a running one, otherwise build the
 * C# assembly and start ours.
 */
export async function ensureEditor(options: EnsureEditorOptions): Promise<EnsureEditorResult> {
  const deps: EnsureEditorDeps = {
    ensure: (session, mode) => session.ensure({ mode }),
    build: buildBeforeStart,
    ...options.deps,
  };
  const fail = (reason: Extract<EnsureEditorResult, { kind: 'failure' }>['reason'], err: unknown, hint = '') => {
    const error = err instanceof Error ? err : new Error(String(err));
    return { kind: 'failure', success: false, reason, message: error.message + hint, error } as const;
  };

  let godot: GodotSession;
  try {
    godot = createGodotSession(options);
  } catch (err) {
    return fail('invalid-options', err);
  }

  // Warm path: the local server answers, so an editor is up (the session would only probe it again).
  const running = await godot.probe();
  if (running !== undefined) return { kind: 'success', success: true, godot, ensured: { ready: running, source: 'running' } };

  const ours = await godot.session.ownedSession();
  const foreign = ours === undefined && godot.session.readLease() === undefined && findGodotProcess(godot.session.projectRoot) !== null;
  if (foreign) {
    // The user's editor without a reachable local server: it may still be loading, but do not wait the full start budget.
    godot = createGodotSession({ ...options, readyTimeoutMs: Math.min(options.readyTimeoutMs ?? FOREIGN_READY_TIMEOUT_MS, FOREIGN_READY_TIMEOUT_MS) });
  } else if (options.build !== false && ours === undefined) {
    // Build only when an editor is about to be started: none of ours runs (or is starting), and no other editor has the project.
    options.onStatus?.('Building C# assembly...');
    let built: BuildProjectResult;
    try {
      built = await deps.build(options.projectPath);
    } catch (err) {
      return fail('build-failed', err, err instanceof CredentialLockBusyError ? ' (another godot-cli call is building this project)' : '');
    }
    if (built.kind === 'failure') return fail('build-failed', built.error);
  }

  try {
    return { kind: 'success', success: true, godot, ensured: await deps.ensure(godot.session, options.window ? 'window' : 'headless') };
  } catch (err) {
    const sessionReason = err instanceof EditorSessionError ? err.reason : undefined;
    let hint = '';
    if (sessionReason === 'not-ready') {
      if (godot.session.readLease() === undefined) {
        // The user's editor, connected elsewhere or without its local server.
        hint = ` (an editor of this project is running, but its local MCP server ${godot.url} does not answer: start the server in its AI Game Developer dock in Custom mode, or close that editor)`;
      } else {
        // Our editor started but its plugin never answered (e.g. the addon failed to load): it would never idle out.
        const closed = await godot.session.closeOwned({ timeoutMs: 5_000, force: true });
        hint = closed.kind === 'closed' ? ` (stopped the unresponsive editor, PID ${closed.pid}; see its log)` : '';
      }
    }
    return fail(sessionReason === 'exited' || sessionReason === 'not-ready' ? 'not-ready' : 'launch-failed', err, hint);
  }
}

/**
 * Delete a build lock whose holder was this machine and is gone (a Ctrl+C mid-build never releases it); the lock
 * protocol alone would wait out its staleness bar. Best-effort.
 */
function removeDeadBuildLock(lockPath: string): void {
  try {
    const holder = JSON.parse(fs.readFileSync(lockPath, 'utf-8')) as { pid?: unknown; hostId?: unknown };
    if (typeof holder.pid === 'number' && typeof holder.hostId === 'string' && holder.hostId.toLowerCase() === os.hostname().toLowerCase()
      && !defaultSessionProcessOps.isAlive(holder.pid)) {
      fs.rmSync(lockPath, { force: true });
    }
  } catch {
    // no lock, or one we cannot read: the lock protocol decides
  }
}
