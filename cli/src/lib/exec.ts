// Library-safe `execCode`: run C# in a project's Godot editor through `script-execute`, starting a warm headless
// editor (cli-core EditorSession) when none is running. No console output, no `process.exit`.

import type { EnsureResult } from '@baizor/gamedev-cli-core';
import { runTool } from './run-tool.js';
import { ensureEditor, type EnsureEditorDeps, type EnsureEditorOptions, type EnsureEditorResult } from './session.js';
import type { RunToolFailure, RunToolOptions, RunToolResult } from './types.js';

export const SCRIPT_EXECUTE_TOOL = 'script-execute';

/**
 * Default `script-execute` call timeout. Longer than the server `tool-timeout` the addon passes
 * (`GodotMcpServerView.ToolTimeoutMs`, parity-tested), so a call the server gives up on returns the server's
 * timeout error instead of a client abort.
 */
export const DEFAULT_EXEC_TIMEOUT_MS = 310_000;

/** Injection points for tests. */
export interface ExecDeps extends EnsureEditorDeps {
  invoke: (options: RunToolOptions) => Promise<RunToolResult>;
}

export interface ExecCodeOptions extends EnsureEditorOptions {
  /**
   * Run on this server as is, starting no editor (Cloud mode: the ai-game.dev hub, where only an editor the user
   * connected answers).
   */
  remoteUrl?: string;
  code: string;
  usings?: string[];
  include?: string[];
  /** Tool call timeout. Default {@link DEFAULT_EXEC_TIMEOUT_MS}. */
  timeoutMs?: number;
  deps?: Partial<ExecDeps>;
}

export interface ExecCodeSuccess {
  kind: 'success';
  success: true;
  /** The server that ran the code. */
  baseUrl: string;
  /** How the editor was obtained (cli-core EditorSession): `running`, `foreign`, `owned` or `started`. */
  editor: EnsureResult<string>['source'];
  /** Parsed `script-execute` response body. */
  data: unknown;
}

export interface ExecCodeFailure {
  kind: 'failure';
  success: false;
  /** {@link ensureEditor}'s reasons, or `tool-failed`: the call failed. */
  reason: Extract<EnsureEditorResult, { kind: 'failure' }>['reason'] | 'tool-failed';
  /** The server that was called (`tool-failed` only). */
  baseUrl?: string;
  /** The underlying call failure (`tool-failed` only). */
  toolFailure?: RunToolFailure;
  message: string;
  error?: Error;
}

export type ExecCodeResult = ExecCodeSuccess | ExecCodeFailure;

/**
 * Run C# in the project's editor. A running editor (GUI or headless) is reused and never closed; otherwise one is
 * started headless (or windowed) on the project's local server that quits by itself after `idleSeconds` without
 * tool calls, so the next call stays warm.
 */
export async function execCode(options: ExecCodeOptions): Promise<ExecCodeResult> {
  let baseUrl: string;
  let source: EnsureResult<string>['source'];
  if (options.remoteUrl) {
    baseUrl = options.remoteUrl;
    source = 'foreign';
  } else {
    const editor = await ensureEditor(options);
    if (editor.kind === 'failure') return editor;
    baseUrl = editor.ensured.ready;
    source = editor.ensured.source;
  }

  options.onStatus?.('Running...');
  const result = await (options.deps?.invoke ?? runTool)({
    toolName: SCRIPT_EXECUTE_TOOL,
    url: baseUrl,
    ...(options.token ? { token: options.token } : {}),
    input: buildScriptExecuteInput(options.code, options.usings ?? [], options.include ?? []),
    timeoutMs: options.timeoutMs ?? DEFAULT_EXEC_TIMEOUT_MS,
  });
  if (result.kind === 'failure') {
    return { kind: 'failure', success: false, reason: 'tool-failed', baseUrl, toolFailure: result, message: toolErrorText(result), error: result.error };
  }
  return { kind: 'success', success: true, baseUrl, editor: source, data: result.data };
}

/** Request body for `script-execute`; empty lists are left out. */
export function buildScriptExecuteInput(code: string, usings: string[], include: string[]): Record<string, unknown> {
  return {
    csharpCode: code,
    ...(usings.length > 0 ? { usings } : {}),
    ...(include.length > 0 ? { include } : {}),
  };
}

/** The tool's own error text when the server reports one (`{ error }` / text content), else the call failure. */
/** The text blocks of a tool answer's `content`, and how many blocks it has in all. */
function textBlocks(data: unknown): { texts: string[]; blocks: number } {
  const content = (data as { content?: unknown } | null | undefined)?.content;
  const blocks = Array.isArray(content) ? (content as Array<{ type?: string; text?: unknown } | null>) : [];
  return { texts: blocks.filter((c) => c?.type === 'text' && typeof c.text === 'string').map((c) => c!.text as string), blocks: blocks.length };
}

/**
 * The text of a tool answer whose content is all text blocks, joined; `undefined` for any other answer (an image, no
 * content), which keeps its JSON.
 */
export function toolResponseText(data: unknown): string | undefined {
  const { texts, blocks } = textBlocks(data);
  return texts.length > 0 && texts.length === blocks ? texts.join('\n') : undefined;
}

export function toolErrorText(failure: RunToolFailure): string {
  const data = failure.data as {
    error?: unknown;
    message?: unknown;
    title?: unknown;
    detail?: unknown;
    errors?: unknown;
    content?: Array<{ type?: string; text?: unknown }>;
  } | string | undefined;
  if (typeof data === 'string' && data.length > 0) return data;
  if (data && typeof data === 'object') {
    if (typeof data.error === 'string') return data.error;
    const { texts } = textBlocks(data);
    if (texts.length > 0) return texts.join('\n');
    if (typeof data.message === 'string') return data.message;
    // ASP.NET ProblemDetails (a 400 for bad arguments): the title plus the field errors.
    if (typeof data.title === 'string') {
      const fields = data.errors && typeof data.errors === 'object'
        ? Object.entries(data.errors as Record<string, unknown>).map(([k, v]) => `${k}: ${Array.isArray(v) ? v.join(' ') : String(v)}`)
        : [];
      return [data.title, typeof data.detail === 'string' ? data.detail : '', ...fields].filter(Boolean).join(' ');
    }
  }
  return failure.message;
}

/**
 * The tool's text output and whether it reported success. RQL tool format (the default): the text content
 * (`System.Int32: 3`, then `logs:` lines). JSON format: `{ structured: { result: { result, logs } } }`, shown the same way.
 */
export function readToolResponse(data: unknown): { ok: boolean; text: string } {
  if (typeof data === 'string') return { ok: true, text: data };
  const response = data as {
    status?: string;
    content?: Array<{ type?: string; text?: string }>;
    structured?: { result?: unknown } | null;
  } | null;
  const ok = response?.status === undefined || response.status === 'success';
  const { texts } = textBlocks(response);
  if (texts.length > 0) return { ok, text: texts.join('\n') };

  const result = response?.structured?.result as {
    result?: { typeName?: string; value?: unknown; fields?: unknown; props?: unknown };
    logs?: unknown;
  } | undefined;
  if (result && typeof result === 'object' && 'result' in result) {
    const member = result.result;
    // A scalar member carries `value`; an object (Node, Vector3, ...) its `fields` / `props`.
    const value = member?.value !== undefined ? member.value : member?.fields !== undefined || member?.props !== undefined
      ? { ...(member.fields !== undefined ? { fields: member.fields } : {}), ...(member.props !== undefined ? { props: member.props } : {}) }
      : null;
    let text = member ? `${member.typeName ?? 'object'}: ${typeof value === 'string' ? value : JSON.stringify(value)}` : 'null';
    if (Array.isArray(result.logs) && result.logs.length > 0) text += '\nlogs:\n' + result.logs.join('\n');
    return { ok, text };
  }
  return { ok, text: data === undefined ? '' : JSON.stringify(data) };
}
