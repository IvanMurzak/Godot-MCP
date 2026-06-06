// Library-safe `runTool` / `runSystemTool` implementations.
//
// Constraints (same contract as the rest of `lib/*.ts`):
// - No commander, no spinners, no process.exit, no console output.
// - Errors are returned in `{ kind: 'failure', success: false, ... }`,
//   never thrown past the public boundary.

import type {
  RunToolFailure,
  RunToolFailureReason,
  RunToolOptions,
  RunToolResult,
  RunToolSuccess,
} from './types.js';

const DEFAULT_TIMEOUT_MS = 60_000;

interface ErrorCause {
  code?: string;
  message?: string;
}

/**
 * Invoke a regular MCP tool over the Godot plugin's HTTP API.
 * POSTs to `<url>/api/tools/{name}`. No console output, no `process.exit`;
 * errors are returned in the `kind: 'failure'` variant.
 */
export async function runTool(opts: RunToolOptions): Promise<RunToolResult> {
  return invokeTool('/api/tools', opts);
}

/**
 * Invoke a system tool (not exposed to MCP clients) over the Godot plugin's
 * HTTP API. POSTs to `<url>/api/system-tools/{name}`.
 */
export async function runSystemTool(opts: RunToolOptions): Promise<RunToolResult> {
  return invokeTool('/api/system-tools', opts);
}

async function invokeTool(routePrefix: string, opts: RunToolOptions): Promise<RunToolResult> {
  const validationFailure = validateOptions(opts);
  if (validationFailure) return validationFailure;

  const url = opts.url!.replace(/\/$/, '');
  const token = opts.token;

  const body = serializeInput(opts.input);
  if ('error' in body) {
    return makeFailure({
      endpoint: '',
      reason: 'invalid-input',
      message: body.error.message,
      error: body.error,
    });
  }

  const endpoint = `${url}${routePrefix}/${encodeURIComponent(opts.toolName)}`;

  const fetchImpl = opts.fetchImpl ?? globalThis.fetch;
  const timeoutMs =
    typeof opts.timeoutMs === 'number' && opts.timeoutMs > 0 ? opts.timeoutMs : DEFAULT_TIMEOUT_MS;

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  const externalAbort = (): void => controller.abort();
  if (opts.signal) {
    if (opts.signal.aborted) controller.abort();
    else opts.signal.addEventListener('abort', externalAbort, { once: true });
  }

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  try {
    const response = await fetchImpl(endpoint, {
      method: 'POST',
      headers,
      body: body.json,
      signal: controller.signal,
    });

    const text = await safeReadText(response);
    const data = parseJsonOrText(text);

    if (!response.ok) {
      return makeFailure({
        endpoint,
        reason: 'http-error',
        httpStatus: response.status,
        data,
        message: response.statusText || `HTTP ${response.status}`,
      });
    }

    const success: RunToolSuccess = {
      kind: 'success',
      success: true,
      endpoint,
      httpStatus: response.status,
      data,
    };
    return success;
  } catch (err) {
    return classifyFetchError(err, endpoint, timeoutMs);
  } finally {
    clearTimeout(timer);
    opts.signal?.removeEventListener('abort', externalAbort);
  }
}

function validateOptions(opts: RunToolOptions): RunToolFailure | null {
  if (!opts || typeof opts !== 'object') {
    return makeFailure({
      endpoint: '',
      reason: 'invalid-input',
      message: 'options object is required.',
    });
  }
  if (typeof opts.toolName !== 'string' || opts.toolName.trim().length === 0) {
    return makeFailure({
      endpoint: '',
      reason: 'invalid-input',
      message: 'toolName is required and must be a non-empty string.',
    });
  }
  const hasUrl = typeof opts.url === 'string' && opts.url.length > 0;
  if (!hasUrl) {
    return makeFailure({
      endpoint: '',
      reason: 'invalid-input',
      message: 'url is required (the Godot-MCP server base URL).',
    });
  }
  return null;
}

function serializeInput(input: unknown): { json: string } | { error: Error } {
  if (input === undefined || input === null) return { json: '{}' };
  if (typeof input === 'string') {
    try {
      JSON.parse(input);
      return { json: input };
    } catch (err) {
      return {
        error: new Error(
          `input string is not valid JSON: ${err instanceof Error ? err.message : String(err)}`,
        ),
      };
    }
  }
  if (typeof input !== 'object') {
    return {
      error: new Error('input must be a plain object, JSON string, undefined, or null.'),
    };
  }
  try {
    return { json: JSON.stringify(input) };
  } catch (err) {
    return {
      error: new Error(
        `input could not be serialized to JSON: ${err instanceof Error ? err.message : String(err)}`,
      ),
    };
  }
}

async function safeReadText(response: Response): Promise<string> {
  try {
    return await response.text();
  } catch {
    return '';
  }
}

function parseJsonOrText(text: string): unknown {
  if (text.length === 0) return undefined;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function getCause(err: unknown): ErrorCause | undefined {
  if (!(err instanceof Error) || !('cause' in err)) return undefined;
  return err.cause as ErrorCause | undefined;
}

function classifyFetchError(err: unknown, endpoint: string, timeoutMs: number): RunToolFailure {
  if (err instanceof Error && err.name === 'AbortError') {
    return makeFailure({
      endpoint,
      reason: 'timeout',
      message: `Tool call timed out after ${timeoutMs}ms.`,
      error: err,
    });
  }

  const error = err instanceof Error ? err : new Error(String(err));
  const causeCode = getCause(err)?.code;

  let reason: RunToolFailureReason = 'unknown';
  if (causeCode === 'ECONNREFUSED') reason = 'connection-refused';
  else if (causeCode === 'ECONNRESET') reason = 'connection-reset';
  else if (causeCode === 'ENOTFOUND' || causeCode === 'EAI_AGAIN') reason = 'network-error';

  return makeFailure({
    endpoint,
    reason,
    message: error.message,
    error,
  });
}

function makeFailure(fields: Omit<RunToolFailure, 'kind' | 'success'>): RunToolFailure {
  return { kind: 'failure', success: false, ...fields };
}
