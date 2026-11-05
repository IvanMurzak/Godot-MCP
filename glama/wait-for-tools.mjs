// Blocks until the local GameDev-MCP-Server lists the connected Godot editor's tools (see glama/README.md).
// Usage: node wait-for-tools.mjs <mcp-url> [timeoutSeconds]
// Polls initialize + tools/list over streamable HTTP and exits 0 once the tool count is non-zero and stable on
// two consecutive polls (the editor registers its tools in one batch, the second poll guards a partial list).
// Exits 1 on timeout. Logs to stderr only: stdout belongs to the MCP stream.

const [url, timeoutArg = '300'] = process.argv.slice(2);
const deadline = Date.now() + Number(timeoutArg) * 1000;
const log = (msg) => process.stderr.write(`[glama-wait] ${msg}\n`);

async function rpc(body, sessionId) {
  const headers = { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' };
  if (sessionId) headers['Mcp-Session-Id'] = sessionId;
  const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body), signal: AbortSignal.timeout(10000) });
  const text = await res.text();
  if (!res.ok && res.status !== 202) throw new Error(`HTTP ${res.status}: ${text.slice(0, 200)}`);
  // A JSON response, or an SSE stream whose `data:` lines carry the JSON-RPC message.
  const payload = res.headers.get('content-type')?.includes('text/event-stream')
    ? text.split('\n').filter((l) => l.startsWith('data:')).map((l) => l.slice(5).trim()).pop()
    : text;
  return { sessionId: res.headers.get('mcp-session-id') ?? sessionId, message: payload ? JSON.parse(payload) : undefined };
}

async function countTools() {
  const init = await rpc({
    jsonrpc: '2.0', id: 1, method: 'initialize',
    params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'glama-wait', version: '1.0.0' } },
  });
  const sid = init.sessionId;
  try {
    await rpc({ jsonrpc: '2.0', method: 'notifications/initialized' }, sid);
    const list = await rpc({ jsonrpc: '2.0', id: 2, method: 'tools/list', params: {} }, sid);
    return list.message?.result?.tools?.length ?? 0;
  } finally {
    if (sid) await fetch(url, { method: 'DELETE', headers: { 'Mcp-Session-Id': sid } }).catch(() => {});
  }
}

let previous = 0;
while (Date.now() < deadline) {
  try {
    const count = await countTools();
    if (count > 0 && count === previous) {
      log(`${count} tools listed`);
      process.exit(0);
    }
    previous = count;
  } catch (err) {
    previous = 0;
    log(`not ready: ${err.message}`);
  }
  await new Promise((r) => setTimeout(r, 1000));
}
log(`timed out after ${timeoutArg}s (last tool count ${previous})`);
process.exit(1);
