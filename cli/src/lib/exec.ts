// Library-safe `execCode`: run C# in a project's Godot editor through `script-execute`, starting a warm headless
// editor (cli-core EditorSession) when none is running. No console output, no `process.exit`.

import type { EnsureResult } from '@baizor/gamedev-cli-core';
import { runTool } from './run-tool.js';
import { ensureEditor, type EnsureEditorDeps, type EnsureEditorOptions } from './session.js';
import type { RunToolFailure, RunToolOptions, RunToolResult } from './types.js';

export const SCRIPT_EXECUTE_TOOL = 'script-execute';

const DEFAULT_TIMEOUT_MS = 180_000;

/** Injection points for tests. */
export interface ExecDeps extends EnsureEditorDeps {
  invoke: (options: RunToolOptions) => Promise<RunToolResult>;
}

export interface ExecCodeOptions extends EnsureEditorOptions {
  code: string;
  usings?: string[];
  include?: string[];
  /** Tool call timeout. Default 180000. */
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
  reason: 'invalid-options' | 'build-failed' | 'launch-failed' | 'not-ready' | 'tool-failed';
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
  const editor = await ensureEditor(options);
  if (editor.kind === 'failure') return editor;
  const { ensured } = editor;

  options.onStatus?.('Running...');
  const baseUrl = ensured.ready;
  const result = await (options.deps?.invoke ?? runTool)({
    toolName: SCRIPT_EXECUTE_TOOL,
    url: baseUrl,
    ...(options.token ? { token: options.token } : {}),
    input: buildScriptExecuteInput(options.code, options.usings ?? [], options.include ?? []),
    timeoutMs: options.timeoutMs ?? DEFAULT_TIMEOUT_MS,
  });
  if (result.kind === 'failure') {
    return { kind: 'failure', success: false, reason: 'tool-failed', baseUrl, toolFailure: result, message: toolErrorText(result), error: result.error };
  }
  return { kind: 'success', success: true, baseUrl, editor: ensured.source, data: result.data };
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
export function toolErrorText(failure: RunToolFailure): string {
  const data = failure.data as { error?: unknown } | string | undefined;
  if (typeof data === 'string' && data.length > 0) return data;
  if (data && typeof data === 'object' && typeof data.error === 'string') return data.error;
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
  const texts = (response?.content ?? []).filter((c) => c?.type === 'text' && typeof c.text === 'string').map((c) => c.text!);
  if (texts.length > 0) return { ok, text: texts.join('\n') };

  const result = response?.structured?.result as { result?: { typeName?: string; value?: unknown }; logs?: unknown } | undefined;
  if (result && typeof result === 'object' && 'result' in result) {
    const member = result.result;
    let text = member ? `${member.typeName ?? 'object'}: ${typeof member.value === 'string' ? member.value : JSON.stringify(member.value ?? null)}` : 'null';
    if (Array.isArray(result.logs) && result.logs.length > 0) text += '\nlogs:\n' + result.logs.join('\n');
    return { ok, text };
  }
  return { ok, text: data === undefined ? '' : JSON.stringify(data) };
}
