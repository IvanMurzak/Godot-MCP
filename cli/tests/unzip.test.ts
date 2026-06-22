import { describe, it, expect } from 'vitest';
import * as zlib from 'zlib';
import { parseZip } from '../src/utils/unzip.js';

/**
 * Build a minimal but spec-valid ZIP archive in memory from `{ path -> bytes }`.
 * Supports STORED (method 0) and DEFLATE (method 8). Enough to exercise parseZip
 * without adding a zip-writer dependency. CRC32 is computed correctly so a strict
 * reader would also accept it.
 */
function crc32(buf: Buffer): number {
  let crc = ~0;
  for (let i = 0; i < buf.length; i++) {
    crc ^= buf[i];
    for (let j = 0; j < 8; j++) {
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
  }
  return ~crc >>> 0;
}

function buildZip(files: { path: string; content: Buffer; method?: 0 | 8 }[]): Buffer {
  const localParts: Buffer[] = [];
  const centralParts: Buffer[] = [];
  let offset = 0;

  for (const f of files) {
    const method = f.method ?? 8;
    const nameBuf = Buffer.from(f.path, 'utf8');
    const compressed = method === 8 ? zlib.deflateRawSync(f.content) : f.content;
    const crc = crc32(f.content);

    const lfh = Buffer.alloc(30);
    lfh.writeUInt32LE(0x04034b50, 0);
    lfh.writeUInt16LE(20, 4); // version
    lfh.writeUInt16LE(0, 6); // flags
    lfh.writeUInt16LE(method, 8);
    lfh.writeUInt16LE(0, 10); // time
    lfh.writeUInt16LE(0, 12); // date
    lfh.writeUInt32LE(crc, 14);
    lfh.writeUInt32LE(compressed.length, 18);
    lfh.writeUInt32LE(f.content.length, 22);
    lfh.writeUInt16LE(nameBuf.length, 26);
    lfh.writeUInt16LE(0, 28); // extra len

    const localRecord = Buffer.concat([lfh, nameBuf, compressed]);
    localParts.push(localRecord);

    const cdh = Buffer.alloc(46);
    cdh.writeUInt32LE(0x02014b50, 0);
    cdh.writeUInt16LE(20, 4);
    cdh.writeUInt16LE(20, 6);
    cdh.writeUInt16LE(0, 8);
    cdh.writeUInt16LE(method, 10);
    cdh.writeUInt16LE(0, 12);
    cdh.writeUInt16LE(0, 14);
    cdh.writeUInt32LE(crc, 16);
    cdh.writeUInt32LE(compressed.length, 20);
    cdh.writeUInt32LE(f.content.length, 24);
    cdh.writeUInt16LE(nameBuf.length, 28);
    cdh.writeUInt16LE(0, 30);
    cdh.writeUInt16LE(0, 32);
    cdh.writeUInt16LE(0, 34);
    cdh.writeUInt16LE(0, 36);
    cdh.writeUInt32LE(0, 38);
    cdh.writeUInt32LE(offset, 42);
    centralParts.push(Buffer.concat([cdh, nameBuf]));

    offset += localRecord.length;
  }

  const localBlob = Buffer.concat(localParts);
  const centralBlob = Buffer.concat(centralParts);

  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(0, 4);
  eocd.writeUInt16LE(0, 6);
  eocd.writeUInt16LE(files.length, 8);
  eocd.writeUInt16LE(files.length, 10);
  eocd.writeUInt32LE(centralBlob.length, 12);
  eocd.writeUInt32LE(localBlob.length, 16);
  eocd.writeUInt16LE(0, 20);

  return Buffer.concat([localBlob, centralBlob, eocd]);
}

describe('parseZip', () => {
  it('round-trips DEFLATE-compressed entries', () => {
    const zip = buildZip([
      { path: 'addons/godot_mcp/plugin.cfg', content: Buffer.from('[plugin]\nname="godot_mcp"\n') },
      { path: 'addons/godot_mcp/Editor/GodotMcpPlugin.cs', content: Buffer.from('// boot\n') },
    ]);
    const entries = parseZip(zip);
    expect(entries).toHaveLength(2);
    const cfg = entries.find((e) => e.path === 'addons/godot_mcp/plugin.cfg');
    expect(cfg?.bytes.toString('utf8')).toBe('[plugin]\nname="godot_mcp"\n');
  });

  it('round-trips STORED (uncompressed) entries', () => {
    const zip = buildZip([{ path: 'a.txt', content: Buffer.from('hello'), method: 0 }]);
    const entries = parseZip(zip);
    expect(entries[0].bytes.toString('utf8')).toBe('hello');
  });

  it('flags directory entries', () => {
    const zip = buildZip([
      { path: 'addons/godot_mcp/', content: Buffer.alloc(0), method: 0 },
      { path: 'addons/godot_mcp/plugin.cfg', content: Buffer.from('x') },
    ]);
    const entries = parseZip(zip);
    expect(entries.find((e) => e.path === 'addons/godot_mcp/')?.isDirectory).toBe(true);
    expect(entries.find((e) => e.path === 'addons/godot_mcp/plugin.cfg')?.isDirectory).toBe(false);
  });

  it('throws on a buffer with no EOCD record', () => {
    expect(() => parseZip(Buffer.from('not a zip'))).toThrowError(/End Of Central Directory/i);
  });
});
