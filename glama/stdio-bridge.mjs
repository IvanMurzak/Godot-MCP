// stdio <-> streamable-HTTP MCP bridge for the Glama listing image (see glama/README.md).
// Usage: node stdio-bridge.mjs <mcp-url>
//
// Reads newline-delimited JSON-RPC from stdin, POSTs each message to <mcp-url> and writes every JSON-RPC message of
// the reply (a JSON body or an SSE stream) to stdout as one line. `initialize` and notifications are sent in order,
// and nothing else is sent before `initialize` has returned the session id, so an eager client that writes
// initialize + initialized + tools/list at once never races the session. Requests after that run concurrently.
// When stdin closes it waits for the requests in flight, ends the HTTP session and exits. Logs go to stderr only.
import { createInterface } from 'node:readline';

const url = process.argv[2];
const log = (msg) => process.stderr.write(`[glama-bridge] ${msg}\n`);
const write = (msg) => process.stdout.write(`${JSON.stringify(msg)}\n`);

let sessionId;
let protocolVersion;
let ordered = Promise.resolve(); // initialize + notifications, in arrival order
const inFlight = new Set();

function headers() {
  const h = { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' };
  if (sessionId) h['Mcp-Session-Id'] = sessionId;
  if (protocolVersion) h['MCP-Protocol-Version'] = protocolVersion;
  return h;
}

function emit(payload, request) {
  const messages = Array.isArray(payload) ? payload : [payload];
  for (const m of messages) {
    if (request.method === 'initialize' && m.id === request.id && m.result?.protocolVersion)
      protocolVersion = m.result.protocolVersion;
    write(m);
  }
}

async function readSse(body, request) {
  const decoder = new TextDecoder();
  let buffer = '';
  const flush = (event) => {
    const data = event.split(/\r?\n/).filter((l) => l.startsWith('data:')).map((l) => l.slice(5).trimStart()).join('\n');
    if (data) emit(JSON.parse(data), request);
  };
  for await (const chunk of body) {
    buffer += decoder.decode(chunk, { stream: true });
    let end;
    while ((end = buffer.search(/\r?\n\r?\n/)) >= 0) {
      flush(buffer.slice(0, end));
      buffer = buffer.slice(end).replace(/^\r?\n\r?\n/, '');
    }
  }
  if (buffer.trim()) flush(buffer);
}

async function post(msg) {
  const isRequest = msg.method !== undefined && msg.id !== undefined;
  try {
    const res = await fetch(url, { method: 'POST', headers: headers(), body: JSON.stringify(msg) });
    sessionId = res.headers.get('mcp-session-id') ?? sessionId;
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${(await res.text()).slice(0, 300)}`);
    if (res.status === 202 || !res.body) return;
    if (res.headers.get('content-type')?.includes('text/event-stream')) await readSse(res.body, msg);
    else {
      const text = await res.text();
      if (text.trim()) emit(JSON.parse(text), msg);
    }
  } catch (err) {
    log(`${msg.method ?? 'response'} failed: ${err.message}`);
    if (isRequest) write({ jsonrpc: '2.0', id: msg.id, error: { code: -32603, message: `Bridge error: ${err.message}` } });
  }
}

function track(promise) {
  inFlight.add(promise);
  promise.finally(() => inFlight.delete(promise));
}

createInterface({ input: process.stdin })
  .on('line', (line) => {
    if (!line.trim()) return;
    let msg;
    try {
      msg = JSON.parse(line);
    } catch {
      log(`ignoring a non-JSON stdin line: ${line.slice(0, 200)}`);
      return;
    }
    if (msg.method === 'initialize' || msg.id === undefined) {
      ordered = ordered.then(() => post(msg));
      track(ordered);
    } else {
      track(ordered.then(() => post(msg)));
    }
  })
  .on('close', async () => {
    while (inFlight.size) await Promise.allSettled([...inFlight]);
    if (sessionId) await fetch(url, { method: 'DELETE', headers: headers() }).catch(() => {});
    process.exit(0);
  });
