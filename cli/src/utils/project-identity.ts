import { createHash } from 'crypto';

/**
 * The single canonical derivation of a project's **routing pin** and its
 * **deterministic local port** from the project root path — the TypeScript port of the
 * shared 7.0 library `com.IvanMurzak.McpPlugin.AgentConfig.ProjectIdentity`
 * (`MCP-Plugin-dotnet/McpPlugin/src/AgentConfig/ProjectIdentity.cs`), which the Godot addon
 * consumes via `GodotProjectIdentity`. Every runtime (Unity/Godot/Unreal plugins, the .NET
 * sidecar, and the engine CLIs) derives identical values with no shared state and no probing,
 * so an agent session launched in a project folder routes strictly to that project's engine
 * instance (design `06-engine-plugins.md` § D14/D15).
 *
 * Algorithm (kept verbatim from the shipped Unity `GeneratePortFromDirectory` / the C# reference):
 *   1. Trim trailing directory separators (`/` and `\`) so `/a/b` and `/a/b/` are the same project.
 *      Separators are NOT converted — `C:\a` and `C:/a` hash differently (do NOT `path.normalize`).
 *   2. Lowercase with an invariant fold (`ToLowerInvariant`-equivalent — see {@link toLowerInvariant}).
 *   3. UTF-8 encode, then SHA-256 hash.
 *   4. pin  = the first 4 bytes of the hash as 8 lowercase hex chars.
 *   5. port = 20000 + (littleEndianUInt32(first 4 bytes) % 10000).  Range 20000-29999.
 *
 * Cross-language parity (C# vs TS) is pinned by the committed golden-vector file
 * (`ProjectIdentity.GoldenVectors.json`) and reproduced byte-for-byte by
 * `tests/project-identity.test.ts`.
 */

/** Inclusive lower bound of the deterministic local-port range. */
export const MIN_PORT = 20000;

/** Inclusive upper bound of the deterministic local-port range. */
export const MAX_PORT = 29999;

/** Number of ports in the deterministic range (10000). */
export const PORT_RANGE = MAX_PORT - MIN_PORT + 1;

/** Number of hex characters in the routing pin (first 4 bytes of the hash). */
export const PIN_LENGTH = 8;

const SEPARATOR_FORWARD = '/';
const SEPARATOR_BACK = String.fromCharCode(92); // backslash

/** The resolved identity for a project root. */
export interface ProjectIdentity {
  /** The routing pin: first 8 lowercase hex chars of the SHA-256 of the normalized project root. */
  pin: string;
  /** The resolved local port — the hash-derived port unless an explicit override was supplied. */
  port: number;
  /** True when {@link port} came from an explicit user override rather than the hash. */
  portIsOverridden: boolean;
}

/**
 * Invariant lowercasing that matches C# `string.ToLowerInvariant()`.
 *
 * JS `String.prototype.toLowerCase()` and C# `ToLowerInvariant()` disagree on some Unicode
 * characters. The one that matters for real project paths — and the one the golden-vector file
 * explicitly pins (`unicodeDivergence`) — is **U+0130 LATIN CAPITAL LETTER I WITH DOT ABOVE**:
 * C# `ToLowerInvariant` leaves it unchanged, whereas a naive JS `toLowerCase()` folds it to
 * `i` + U+0307 (COMBINING DOT ABOVE), producing a DIFFERENT hash. Preserve U+0130 so the TS
 * derivation reproduces the canonical C# value byte-for-byte; every other character lowercases
 * identically under both implementations.
 */
export function toLowerInvariant(value: string): string {
  let result = '';
  for (const ch of value) {
    result += ch === 'İ' ? ch : ch.toLowerCase();
  }
  return result;
}

/**
 * The exact string that is UTF-8/SHA-256 hashed: the project root with trailing directory
 * separators trimmed, then invariant-lowercased. Exposed so callers/tests can reproduce the
 * pre-hash string.
 */
export function normalize(projectRoot: string): string {
  return toLowerInvariant(trimTrailingSeparators(projectRoot));
}

/** The routing pin only (first 8 lowercase hex chars of the hash). Never affected by overrides. */
export function derivePin(projectRoot: string): string {
  return hashOf(projectRoot).subarray(0, PIN_LENGTH / 2).toString('hex');
}

/**
 * The pure hash-derived port (ignores any override). Byte-for-byte equivalent of the shipped
 * Unity `GeneratePortFromDirectory` when given the same directory string.
 */
export function derivePort(projectRoot: string): number {
  return portFromHash(hashOf(projectRoot));
}

/**
 * The FULL project-path hash: the complete 64-char lowercase hex SHA-256 of the normalized
 * project root — the `projectPathHash` an engine plugin sends in its hub instance-metadata
 * handshake. The routing pin is a case-insensitive prefix of this value by construction.
 */
export function deriveProjectPathHash(projectRoot: string): string {
  return hashOf(projectRoot).toString('hex');
}

/**
 * Derive the identity for `projectRoot`. When `portOverride` is non-null (the user's explicit
 * override from the project marker) it always wins for {@link ProjectIdentity.port}; the
 * {@link ProjectIdentity.pin} is always hash-derived.
 */
export function deriveProjectIdentity(projectRoot: string, portOverride?: number | null): ProjectIdentity {
  const hash = hashOf(projectRoot);
  const pin = hash.subarray(0, PIN_LENGTH / 2).toString('hex');
  if (portOverride !== undefined && portOverride !== null) {
    return { pin, port: portOverride, portIsOverridden: true };
  }
  return { pin, port: portFromHash(hash), portIsOverridden: false };
}

function hashOf(projectRoot: string): Buffer {
  if (projectRoot === null || projectRoot === undefined) {
    throw new TypeError('projectRoot must be a string');
  }
  return createHash('sha256').update(Buffer.from(normalize(projectRoot), 'utf8')).digest();
}

function portFromHash(hash: Buffer): number {
  // First 4 bytes as an explicit little-endian uint32 — matches the C# byte-shift
  // (`hash[0] | hash[1]<<8 | hash[2]<<16 | hash[3]<<24`) and is CPU-endianness independent.
  const value = hash.readUInt32LE(0);
  return MIN_PORT + (value % PORT_RANGE);
}

function trimTrailingSeparators(path: string): string {
  let end = path.length;
  while (end > 1 && (path[end - 1] === SEPARATOR_FORWARD || path[end - 1] === SEPARATOR_BACK)) {
    end--;
  }
  return end === path.length ? path : path.slice(0, end);
}
