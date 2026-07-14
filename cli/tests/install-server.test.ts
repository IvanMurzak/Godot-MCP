import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as zlib from 'zlib';
import { installServer, getManagedServerDir, getManagedServerExecutablePath } from '../src/lib/install-server.js';
import { serverExecutableFileName, serverAssetName, detectHostRid, sha256Hex } from '../src/utils/server-source.js';

// ---- in-memory zip builder (DEFLATE) — mirrors install-plugin-installer.test.ts ----
function crc32(buf: Buffer): number {
  let crc = ~0;
  for (let i = 0; i < buf.length; i++) {
    crc ^= buf[i];
    for (let j = 0; j < 8; j++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return ~crc >>> 0;
}
function buildZip(files: { path: string; content: Buffer }[]): Buffer {
  const local: Buffer[] = [];
  const central: Buffer[] = [];
  let offset = 0;
  for (const f of files) {
    const name = Buffer.from(f.path, 'utf8');
    const comp = zlib.deflateRawSync(f.content);
    const crc = crc32(f.content);
    const lfh = Buffer.alloc(30);
    lfh.writeUInt32LE(0x04034b50, 0);
    lfh.writeUInt16LE(20, 4);
    lfh.writeUInt16LE(8, 8);
    lfh.writeUInt32LE(crc, 14);
    lfh.writeUInt32LE(comp.length, 18);
    lfh.writeUInt32LE(f.content.length, 22);
    lfh.writeUInt16LE(name.length, 26);
    const rec = Buffer.concat([lfh, name, comp]);
    local.push(rec);
    const cdh = Buffer.alloc(46);
    cdh.writeUInt32LE(0x02014b50, 0);
    cdh.writeUInt16LE(8, 10);
    cdh.writeUInt32LE(crc, 16);
    cdh.writeUInt32LE(comp.length, 20);
    cdh.writeUInt32LE(f.content.length, 24);
    cdh.writeUInt16LE(name.length, 28);
    cdh.writeUInt32LE(offset, 42);
    central.push(Buffer.concat([cdh, name]));
    offset += rec.length;
  }
  const lb = Buffer.concat(local);
  const cb = Buffer.concat(central);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(files.length, 8);
  eocd.writeUInt16LE(files.length, 10);
  eocd.writeUInt32LE(cb.length, 12);
  eocd.writeUInt32LE(lb.length, 16);
  return Buffer.concat([lb, cb, eocd]);
}

const EXE = serverExecutableFileName(); // host-platform exe name
const RID = detectHostRid();
const ASSET = serverAssetName(RID);

const SERVER_ZIP = buildZip([
  { path: EXE, content: Buffer.from('#!/bin/sh\necho server\n') },
  { path: 'gamedev-mcp-server.dll', content: Buffer.from('deps') },
]);
const ZIP_DIGEST = sha256Hex(SERVER_ZIP);

/** A mock fetch that serves the RID zip on the `.zip` URL and a manifest on the SHA256SUMS URL. */
function mockServerFetch(opts: { zip?: Buffer; manifest?: string | null; zipStatus?: number; sumsStatus?: number }): typeof fetch {
  const zip = opts.zip ?? SERVER_ZIP;
  const manifest = opts.manifest === undefined ? `${ZIP_DIGEST}  ${ASSET}\n` : opts.manifest;
  return (async (url: string) => {
    if (url.endsWith('SHA256SUMS')) {
      const status = opts.sumsStatus ?? 200;
      return {
        ok: status >= 200 && status < 300,
        status,
        statusText: 'x',
        text: async () => manifest ?? '',
      } as unknown as Response;
    }
    const status = opts.zipStatus ?? 200;
    return {
      ok: status >= 200 && status < 300,
      status,
      statusText: 'x',
      arrayBuffer: async () => zip.buffer.slice(zip.byteOffset, zip.byteOffset + zip.byteLength),
    } as unknown as Response;
  }) as unknown as typeof fetch;
}

describe('installServer — download path (verified)', () => {
  let tmp: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-srv-'));
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  it('downloads, verifies the SHA256, extracts to the managed dir, and records the exe path', async () => {
    const result = await installServer({ godotProjectPath: tmp, version: '9.0.0', fetchImpl: mockServerFetch({}) });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.source).toBe('download');
    expect(result.version).toBe('9.0.0');
    expect(result.rid).toBe(RID);
    expect(result.downloadUrl).toContain('github.com');
    expect(result.downloadUrl).toContain('v9.0.0');
    expect(result.executablePath).toBe(getManagedServerExecutablePath(tmp));
    expect(fs.existsSync(result.executablePath)).toBe(true);
    // no staging leftovers
    expect(fs.readdirSync(tmp).filter((n) => n.startsWith('.gamedev-server-staging'))).toHaveLength(0);
  });

  it('FAILS CLOSED on a digest mismatch (tampered zip) and leaves no managed dir', async () => {
    const tamperedZip = buildZip([{ path: EXE, content: Buffer.from('malware') }]);
    const result = await installServer({
      godotProjectPath: tmp,
      version: '9.0.0',
      fetchImpl: mockServerFetch({ zip: tamperedZip /* manifest still lists the ORIGINAL digest */ }),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/did not match|fail-closed/i);
    expect(fs.existsSync(getManagedServerDir(tmp))).toBe(false);
  });

  it('FAILS CLOSED when the asset has no SHA256SUMS entry', async () => {
    const result = await installServer({
      godotProjectPath: tmp,
      version: '9.0.0',
      fetchImpl: mockServerFetch({ manifest: `${'c'.repeat(64)}  some-other-asset.zip\n` }),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/no entry|fail-closed/i);
    expect(fs.existsSync(getManagedServerDir(tmp))).toBe(false);
  });

  it('FAILS CLOSED when the SHA256SUMS manifest cannot be downloaded (unverified)', async () => {
    const result = await installServer({
      godotProjectPath: tmp,
      version: '9.0.0',
      fetchImpl: mockServerFetch({ sumsStatus: 404 }),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/unverified|SHA256SUMS/i);
    expect(fs.existsSync(getManagedServerDir(tmp))).toBe(false);
  });

  it('surfaces a clean 404 when the release zip is missing', async () => {
    const result = await installServer({
      godotProjectPath: tmp,
      version: '404.0.0',
      fetchImpl: mockServerFetch({ zipStatus: 404 }),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/HTTP 404/);
  });

  it('rejects an unsupported host RID (unknown token) before any network call', async () => {
    let called = false;
    const spy = (async () => {
      called = true;
      throw new Error('no network');
    }) as unknown as typeof fetch;
    const guarded = await installServer({ godotProjectPath: tmp, version: '9.0.0', rid: 'unknown-unknown', fetchImpl: spy });
    expect(guarded.kind).toBe('failure');
    if (guarded.kind === 'failure') expect(guarded.error.message).toMatch(/RID|--server-source/i);
    expect(called).toBe(false);
  });
});

describe('installServer — --server-source (offline / CI)', () => {
  let tmp: string;
  let src: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-srvsrc-'));
    src = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-srvbin-'));
    fs.writeFileSync(path.join(src, EXE), '#!/bin/sh\necho stub\n');
    fs.writeFileSync(path.join(src, 'gamedev-mcp-server.dll'), 'deps');
  });
  afterEach(() => {
    fs.rmSync(tmp, { recursive: true, force: true });
    fs.rmSync(src, { recursive: true, force: true });
  });

  it('copies a local server dir into the managed dir and never hits the network', async () => {
    let called = false;
    const spy = (async () => {
      called = true;
      throw new Error('network should not be used with --server-source');
    }) as unknown as typeof fetch;
    const result = await installServer({ godotProjectPath: tmp, source: src, fetchImpl: spy });
    expect(result.kind).toBe('success');
    expect(called).toBe(false);
    if (result.kind === 'success') {
      expect(result.source).toBe('local');
      expect(fs.existsSync(result.executablePath)).toBe(true);
      expect(result.executablePath).toBe(getManagedServerExecutablePath(tmp));
    }
  });

  it('installs from a local .zip archive', async () => {
    const zipPath = path.join(src, 'server.zip');
    fs.writeFileSync(zipPath, SERVER_ZIP);
    const result = await installServer({ godotProjectPath: tmp, source: zipPath });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') expect(fs.existsSync(result.executablePath)).toBe(true);
  });

  it('fails clearly when --server-source has no server executable', async () => {
    const bare = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-srvbare-'));
    try {
      const result = await installServer({ godotProjectPath: tmp, source: bare });
      expect(result.kind).toBe('failure');
      if (result.kind === 'failure') expect(result.error.message).toMatch(/did not yield|gamedev-mcp-server/i);
    } finally {
      fs.rmSync(bare, { recursive: true, force: true });
    }
  });
});
