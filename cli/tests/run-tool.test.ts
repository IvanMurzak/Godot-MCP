import { describe, it, expect, vi } from 'vitest';
import { runTool, runSystemTool } from '../src/lib/run-tool.js';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('runTool', () => {
  it('fails with invalid-input when toolName is missing', async () => {
    const result = await runTool({ toolName: '', url: 'http://localhost:8080' });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.reason).toBe('invalid-input');
      expect(result.message).toContain('toolName is required');
    }
  });

  it('fails with invalid-input when url is missing (no port-hash fallback)', async () => {
    const result = await runTool({ toolName: 'ping' });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.reason).toBe('invalid-input');
      expect(result.message).toContain('url is required');
    }
  });

  it('POSTs to /api/tools/<name> and returns parsed data on success', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, { structured: { result: 'pong' } }));
    const result = await runTool({
      toolName: 'ping',
      url: 'http://localhost:8080/',
      input: { message: 'hi' },
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    const [endpoint, init] = fetchImpl.mock.calls[0] as [string, RequestInit];
    expect(endpoint).toBe('http://localhost:8080/api/tools/ping');
    expect(init.method).toBe('POST');
    expect(init.body).toBe(JSON.stringify({ message: 'hi' }));

    expect(result.kind).toBe('success');
    if (result.kind === 'success') {
      expect(result.httpStatus).toBe(200);
      expect(result.data).toEqual({ structured: { result: 'pong' } });
    }
  });

  it('attaches a Bearer header when a token is provided', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, {}));
    await runTool({
      toolName: 'ping',
      url: 'http://localhost:8080',
      token: 'tok',
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const [, init] = fetchImpl.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(headers['Authorization']).toBe('Bearer tok');
  });

  it('returns http-error on a non-OK response', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(404, { error: 'not found' }));
    const result = await runTool({
      toolName: 'missing',
      url: 'http://localhost:8080',
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.reason).toBe('http-error');
      expect(result.httpStatus).toBe(404);
    }
  });

  it('rejects malformed JSON input strings', async () => {
    const result = await runTool({
      toolName: 'ping',
      url: 'http://localhost:8080',
      input: '{not json',
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.reason).toBe('invalid-input');
    }
  });
});

describe('runSystemTool', () => {
  it('POSTs to /api/system-tools/<name>', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, {}));
    await runSystemTool({
      toolName: 'reload',
      url: 'http://localhost:8080',
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const [endpoint] = fetchImpl.mock.calls[0] as [string];
    expect(endpoint).toBe('http://localhost:8080/api/system-tools/reload');
  });
});
