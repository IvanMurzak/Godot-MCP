// Godot editor sessions on the shared cli-core EditorSession (Code-Exec plan §4.6): reuse any running editor of
// the project, otherwise start our own (headless by default) connected to the project's LOCAL MCP server, with a
// lease so only our own editor is ever closed. Library-safe: no console output, no `process.exit`.

import * as path from 'path';
import {
  defaultSessionProcessOps,
  EditorSession,
  EditorSessionError,
  godotAdapter,
  mcpReadinessProbe,
  readSessionLease,
  type EditorSessionOptions,
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

/** How long to wait for a user's own editor whose local server does not answer yet (it may still be loading). */
const FOREIGN_READY_TIMEOUT_MS = 60_000;

/** The connection mode of an editor the session starts (a local server), in its env and its lease. */
const SESSION_CONNECTION_MODE = 'Custom';

export interface GodotSessionOptions {
  projectPath: string;
  /**
   * The server the editor's plugin connects to and the session probes. Default: the server recorded in the lease
   * of a live editor started by the CLI, else the project's local port (`http://localhost:<derived>`). A non-loopback
   * URL is rejected: headless sessions are local only.
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
  /** Runs under the session's startup lock right before it starts an editor (the C# build). */
  beforeLaunch?: EditorSessionOptions<string>['beforeLaunch'];
  onStatus?: (message: string) => void;
  /** Tests: OS process primitives. */
  processOps?: Partial<SessionProcessOps>;
}

export interface GodotSession {
  session: EditorSession<string>;
  /** The local server URL the session probes and a started editor binds. */
  url: string;
}

/**
 * Environment of an editor the session starts: Custom mode on the local URL, the plugin starts the local server
 * (there is no dock button to press in a headless editor), and the token when one is given.
 */
export function sessionEditorEnv(url: string, token: string | undefined): Record<string, string> {
  return {
    [ENV_CONNECTION_MODE]: SESSION_CONNECTION_MODE,
    [ENV_HOST]: url,
    [ENV_START_SERVER]: 'true',
    ...(token ? { [ENV_TOKEN]: token, [ENV_AUTH_OPTION]: 'Required' } : {}),
  };
}

/**
 * The Godot adapter with the CLI's hooks: running-editor detection (its pid, so the session notices when it exits)
 * and the plugin-side graceful close through the verified lease (its nonce, and the server it was started on;
 * `url` serves a lease written before cli-core 0.7.1, which has no `serverUrl`).
 */
export function godotSessionAdapter(url: string, token: string | undefined): EngineAdapter {
  return {
    ...godotAdapter,
    editor: {
      ...godotAdapter.editor!,
      findRunningEditor: async (projectRoot) => findGodotProcess(projectRoot)?.pid ?? false,
      requestClose: async (_pid, _projectRoot, lease) => {
        if (lease === undefined) return false;
        const result = await runSystemTool({
          toolName: SESSION_CLOSE_TOOL,
          url: lease.serverUrl ?? url,
          ...(token ? { token } : {}),
          input: { nonce: lease.nonce },
          timeoutMs: 15_000,
        });
        // A refusing tool (wrong nonce, an addon without session-close) answers 200 with status "error".
        return result.kind === 'success' && (result.data as { status?: string } | null)?.status !== 'error';
      },
    },
  };
}

/**
 * The local server of the editor the CLI started for the project, from its lease, while that editor lives — so a
 * call without `--url` reaches an editor started with one.
 */
function leasedServerUrl(projectRoot: string, isAlive: (pid: number) => boolean): string | undefined {
  const lease = readSessionLease(projectRoot);
  return lease?.serverUrl !== undefined && isLocalhostUrl(lease.serverUrl) && isAlive(lease.pid) ? lease.serverUrl : undefined;
}

/** Build the session of a Godot project. Throws when `url` is not local. */
export function createGodotSession(options: GodotSessionOptions): GodotSession {
  const projectRoot = path.resolve(options.projectPath);
  const isAlive = options.processOps?.isAlive ?? defaultSessionProcessOps.isAlive;
  const url = (options.url ?? leasedServerUrl(projectRoot, isAlive) ?? localProjectUrl(projectRoot)).replace(/\/+$/, '');
  if (!isLocalhostUrl(url)) {
    throw new Error(`editor sessions connect to a local server only; ${url} is not on this machine`);
  }
  const session = new EditorSession<string>({
    adapter: godotSessionAdapter(url, options.token),
    projectRoot,
    probeReady: mcpReadinessProbe({ urls: [url], ...(options.token ? { token: options.token } : {}) }),
    editorBinary: () => {
      const binary = findGodotBinary(options.editorPath);
      if (!binary) throw new Error('no Godot editor binary found (pass --editor-path or set GODOT_BIN)');
      return binary;
    },
    env: sessionEditorEnv(url, options.token),
    // The resolved, normalised connection the started editor gets (env above), recorded in the lease.
    serverUrl: url,
    connectionMode: SESSION_CONNECTION_MODE,
    // The user's editor without a reachable local server: it may still be loading, but do not wait the full start budget.
    foreignReadyTimeoutMs: Math.min(options.readyTimeoutMs ?? FOREIGN_READY_TIMEOUT_MS, FOREIGN_READY_TIMEOUT_MS),
    ...(options.beforeLaunch ? { beforeLaunch: options.beforeLaunch } : {}),
    ...(options.idleSeconds !== undefined ? { idleSeconds: options.idleSeconds } : {}),
    ...(options.readyTimeoutMs !== undefined ? { readyTimeoutMs: options.readyTimeoutMs } : {}),
    ...(options.onStatus ? { onStatus: options.onStatus } : {}),
    ...(options.processOps ? { processOps: options.processOps } : {}),
  });
  return { session, url };
}

/** Injection points for tests. */
export interface EnsureEditorDeps {
  ensure: (session: EditorSession<string>, mode: 'headless' | 'window') => Promise<EnsureResult<string>>;
  build: (projectPath: string) => Promise<BuildProjectResult>;
}

export interface EnsureEditorOptions extends Omit<GodotSessionOptions, 'beforeLaunch'> {
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

/** The pre-launch C# build failed (thrown from `beforeLaunch`, which `ensure` propagates unchanged). */
class BuildFailedError extends Error {}

/**
 * A ready editor of the project (the `exec` / `open --headless` policy): reuse a running one, otherwise build the
 * C# assembly (Godot does not build it in a headless editor, and a missing assembly disables the addon) and start
 * ours. The build runs under the session's startup lock, only when this call starts the editor: parallel cold calls
 * build once and join that editor.
 */
export async function ensureEditor(options: EnsureEditorOptions): Promise<EnsureEditorResult> {
  const deps: EnsureEditorDeps = {
    ensure: (session, mode) => session.ensure({ mode }),
    build: (projectPath) => buildProject({ projectPath }),
    ...options.deps,
  };
  const fail = (reason: Extract<EnsureEditorResult, { kind: 'failure' }>['reason'], err: unknown, hint = '') => {
    const error = err instanceof Error ? err : new Error(String(err));
    return { kind: 'failure', success: false, reason, message: error.message + hint, error } as const;
  };

  const beforeLaunch = async (projectRoot: string) => {
    options.onStatus?.('Building C# assembly...');
    const built = await deps.build(projectRoot);
    if (built.kind === 'failure') throw new BuildFailedError(built.errorMessage);
  };

  let godot: GodotSession;
  try {
    godot = createGodotSession({ ...options, ...(options.build !== false ? { beforeLaunch } : {}) });
  } catch (err) {
    return fail('invalid-options', err);
  }

  try {
    return { kind: 'success', success: true, godot, ensured: await deps.ensure(godot.session, options.window ? 'window' : 'headless') };
  } catch (err) {
    if (err instanceof BuildFailedError) return fail('build-failed', err);
    const sessionError = err instanceof EditorSessionError ? err : undefined;
    let hint = '';
    if (sessionError?.reason === 'not-ready') {
      if (sessionError.source === 'foreign') {
        // Core's message says what is wrong; add only how to fix it in Godot.
        hint = ` (Godot: in that editor's AI Game Developer dock choose Custom mode and start the server on ${godot.url})`;
      } else {
        // Our editor started but its plugin never answered (e.g. the addon failed to load): it would never idle out.
        const closed = await godot.session.closeOwned({ timeoutMs: 5_000, force: true });
        hint = closed.kind === 'closed' ? ` (stopped the unresponsive editor, PID ${closed.pid}; see its log)` : '';
      }
    }
    const notReady = sessionError?.reason === 'exited' || sessionError?.reason === 'not-ready';
    return fail(notReady ? 'not-ready' : 'launch-failed', err, hint);
  }
}
