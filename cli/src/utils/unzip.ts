import * as zlib from 'zlib';

/**
 * Minimal, dependency-free ZIP reader for the addon-install path. Parses the ZIP
 * central directory and inflates each entry (STORED `0` + DEFLATE `8` — the only
 * two compression methods the GitHub release `zip -r` produces). Pure: takes the
 * archive bytes, returns `{ path, bytes }` entries; performs NO filesystem writes
 * (the caller decides where the files land, after path-safety checks).
 *
 * This avoids adding a native/3rd-party unzip dependency to the lean `godot-cli`
 * package while staying cross-platform (no shelling out to a system `unzip`).
 * Exported for unit tests.
 */

export interface ZipEntry {
  /** The entry's path inside the archive, using forward slashes. */
  path: string;
  /** Decompressed bytes. Empty for directory entries. */
  bytes: Buffer;
  /** True when the entry is a directory (path ends with `/`). */
  isDirectory: boolean;
}

// ZIP signatures.
const EOCD_SIGNATURE = 0x06054b50; // End Of Central Directory
const CDH_SIGNATURE = 0x02014b50; // Central Directory Header
const LFH_SIGNATURE = 0x04034b50; // Local File Header

/**
 * Parse a ZIP archive buffer into its entries. Throws a descriptive Error on a
 * malformed archive (the caller wraps this into a structured failure — it never
 * propagates past the library's public boundary).
 */
export function parseZip(buffer: Buffer): ZipEntry[] {
  const eocdOffset = findEndOfCentralDirectory(buffer);
  if (eocdOffset === -1) {
    throw new Error('Invalid zip: End Of Central Directory record not found.');
  }

  const entryCount = buffer.readUInt16LE(eocdOffset + 10);
  const centralDirOffset = buffer.readUInt32LE(eocdOffset + 16);

  const entries: ZipEntry[] = [];
  let ptr = centralDirOffset;

  for (let i = 0; i < entryCount; i++) {
    if (buffer.readUInt32LE(ptr) !== CDH_SIGNATURE) {
      throw new Error(`Invalid zip: bad central directory header at entry ${i}.`);
    }
    const compressionMethod = buffer.readUInt16LE(ptr + 10);
    const compressedSize = buffer.readUInt32LE(ptr + 20);
    const fileNameLength = buffer.readUInt16LE(ptr + 28);
    const extraFieldLength = buffer.readUInt16LE(ptr + 30);
    const commentLength = buffer.readUInt16LE(ptr + 32);
    const localHeaderOffset = buffer.readUInt32LE(ptr + 42);

    const fileName = buffer.toString('utf8', ptr + 46, ptr + 46 + fileNameLength);

    const bytes = readLocalEntry(buffer, localHeaderOffset, compressionMethod, compressedSize);
    entries.push({
      path: fileName.replace(/\\/g, '/'),
      bytes,
      isDirectory: fileName.endsWith('/'),
    });

    ptr += 46 + fileNameLength + extraFieldLength + commentLength;
  }

  return entries;
}

/** Read + decompress a single entry given its central-directory metadata. */
function readLocalEntry(
  buffer: Buffer,
  localHeaderOffset: number,
  compressionMethod: number,
  compressedSize: number,
): Buffer {
  if (buffer.readUInt32LE(localHeaderOffset) !== LFH_SIGNATURE) {
    throw new Error('Invalid zip: bad local file header.');
  }
  // The local header repeats the name/extra lengths; the data starts after them.
  const lfhNameLength = buffer.readUInt16LE(localHeaderOffset + 26);
  const lfhExtraLength = buffer.readUInt16LE(localHeaderOffset + 28);
  const dataStart = localHeaderOffset + 30 + lfhNameLength + lfhExtraLength;
  const compressed = buffer.subarray(dataStart, dataStart + compressedSize);

  if (compressionMethod === 0) {
    // STORED — no compression.
    return Buffer.from(compressed);
  }
  if (compressionMethod === 8) {
    // DEFLATE (raw, no zlib header).
    return zlib.inflateRawSync(compressed);
  }
  throw new Error(`Unsupported zip compression method ${compressionMethod}.`);
}

/**
 * Locate the End Of Central Directory record by scanning backwards from EOF for
 * its signature (the trailing comment is almost always empty, so it is usually
 * the last 22 bytes — but we scan to be safe).
 */
function findEndOfCentralDirectory(buffer: Buffer): number {
  const minEocdSize = 22;
  if (buffer.length < minEocdSize) return -1;
  // Max comment length is 0xFFFF; scan that far back at most.
  const scanStart = Math.max(0, buffer.length - minEocdSize - 0xffff);
  for (let i = buffer.length - minEocdSize; i >= scanStart; i--) {
    if (buffer.readUInt32LE(i) === EOCD_SIGNATURE) {
      return i;
    }
  }
  return -1;
}
