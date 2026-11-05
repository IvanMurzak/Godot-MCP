// Probes the Glama image the way Glama's introspection does: spawns the server command, writes
// initialize + notifications/initialized + tools/list to its stdin at once, and reports the FIRST tools/list.
// Optional tool calls follow (name + JSON arguments), then stdin is closed and the container must exit.
//
// Usage: node glama/probe.mjs [toolName '<jsonArgs>']... -- docker run -i --rm <image>
//   (any stdio command works after `--`, e.g. `ssh <host> docker run -i --rm <image>` for a remote Docker)
// Exits non-zero when the first tools/list is empty, a tool lacks a title or annotations, a call errors,
// or stdout carries a non-JSON line.
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

const argv = process.argv.slice(2);
const sep = argv.indexOf('--');
const [command, ...commandArgs] = sep < 0 ? [] : argv.slice(sep + 1);
const callArgs = sep < 0 ? argv : argv.slice(0, sep);
if (!command) {
  console.error("usage: node glama/probe.mjs [toolName '<jsonArgs>']... -- docker run -i --rm <image>");
  process.exit(2);
}
const calls = [];
for (let i = 0; i < callArgs.length; i += 2) calls.push({ name: callArgs[i], args: JSON.parse(callArgs[i + 1] ?? '{}') });

const started = Date.now();
const secs = () => ((Date.now() - started) / 1000).toFixed(1);
const child = spawn(command, commandArgs, { stdio: ['pipe', 'pipe', 'inherit'] });
const pending = new Map();
let failed = false;

createInterface({ input: child.stdout }).on('line', (line) => {
  let msg;
  try {
    msg = JSON.parse(line);
  } catch {
    console.error(`[probe] FAIL: non-JSON line on stdout: ${line.slice(0, 200)}`);
    failed = true;
    return;
  }
  pending.get(msg.id)?.(msg);
  pending.delete(msg.id);
});

let nextId = 1;
const send = (method, params) => child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method, params })}\n`);
const request = (method, params) => {
  const id = nextId++;
  const reply = new Promise((resolve) => pending.set(id, resolve));
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
  return reply;
};

// Written back to back, with no wait between them, exactly like an eager client.
const init = request('initialize', {
  protocolVersion: '2025-06-18',
  capabilities: {},
  clientInfo: { name: 'glama-probe', version: '1.0.0' },
});
send('notifications/initialized', {});
const list = request('tools/list', {});

const initReply = await init;
console.error(`[probe] initialize after ${secs()}s: ${initReply.result?.serverInfo?.name} ${initReply.result?.serverInfo?.version}`);
const tools = (await list).result?.tools ?? [];
console.error(`[probe] first tools/list after ${secs()}s: ${tools.length} tools`);
for (const t of tools) {
  const ok = Boolean(t.title) && t.annotations && Object.keys(t.annotations).length > 0;
  if (!ok) failed = true;
  console.error(`  ${ok ? 'ok ' : 'BAD'} ${t.name} — ${t.title ?? '(no title)'} ${JSON.stringify(t.annotations ?? {})}`);
}
if (tools.length === 0) failed = true;

for (const { name, args } of calls) {
  const reply = await request('tools/call', { name, arguments: args });
  const isError = Boolean(reply.error || reply.result?.isError);
  if (isError) failed = true;
  const text = JSON.stringify(reply.error ?? reply.result?.content ?? reply.result).slice(0, 300);
  console.error(`[probe] tools/call ${name}: ${isError ? 'ERROR' : 'ok'} ${text}`);
}

const closedAt = Date.now();
child.stdin.end();
child.on('exit', (code) => {
  console.error(`[probe] container exited ${code} ${((Date.now() - closedAt) / 1000).toFixed(1)}s after stdin closed`);
  process.exit(failed ? 1 : 0);
});
