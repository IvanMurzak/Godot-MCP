// Pure helpers for resolving WHERE the shared `gamedev-mcp-server` binary comes
// from when `install-plugin --with-server` materializes it: RID detection, the
// trusted GitHub-release download URL, the `SHA256SUMS` integrity manifest, and
// the fail-closed verify verdict. These MIRROR the addon's own server logic in
// `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs` (github.com only,
// `releases/download/v<version>/gamedev-mcp-server-<rid>.zip`, verify-before-
// extract against `SHA256SUMS`). No side effects; unit-testable with no network.
//
// The DEFAULT server version is single-sourced from the addon's pinned
// `ServerVersion` constant (a parity test — `cli/tests/server-source-parity.test.ts`
// — reads `GodotMcpServerView.cs` and FAILS the build if this drifts), so the CLI
// and the addon can never download two different server versions.

import { createHash } from 'crypto';

/**
 * The DEFAULT shared-server version `--with-server` downloads. Single-sourced
 * from the addon's `GodotMcpServerView.ServerVersion` constant (parity-tested).
 * `--server-version <v>` overrides it; `--server-source <path>` skips the
 * download entirely (offline / CI). Bumping the addon's server pin requires
 * bumping this in the same PR or the parity test goes red.
 */
export const DEFAULT_SERVER_VERSION = '9.1.1';

/** GitHub-release host the server zip + manifest are downloaded from. NOTHING else is trusted. */
export const TRUSTED_DOWNLOAD_HOST = 'github.com';

/** The `owner/repo` the shared server releases live under. */
export const SERVER_RELEASE_REPO = 'IvanMurzak/GameDev-MCP-Server';

/** The server-binary base name (matches the addon's `GodotMcpServerView.ExecutableName`). */
export const SERVER_EXECUTABLE_NAME = 'gamedev-mcp-server';

/** The release-asset / RID prefix (matches the addon's `AssetPrefix`). */
export const SERVER_ASSET_PREFIX = SERVER_EXECUTABLE_NAME;

/** The integrity-manifest asset attached to every GameDev-MCP-Server release. */
export const SHA256SUMS_ASSET_NAME = 'SHA256SUMS';

/** Map a Node `process.platform` token to the .NET RID os segment (`win`/`osx`/`linux`). */
export function osToken(platform: NodeJS.Platform = process.platform): string {
  switch (platform) {
    case 'win32':
      return 'win';
    case 'darwin':
      return 'osx';
    case 'linux':
      return 'linux';
    default:
      return 'unknown';
  }
}

/** Map a Node `process.arch` token to the .NET RID arch segment (`x86`/`x64`/`arm`/`arm64`). */
export function archToken(arch: string = process.arch): string {
  switch (arch) {
    case 'x64':
      return 'x64';
    case 'arm64':
      return 'arm64';
    case 'ia32':
      return 'x86';
    case 'arm':
      return 'arm';
    default:
      return 'unknown';
  }
}

/**
 * The RID-style platform name `<os>-<arch>` (e.g. `win-x64`) for the given (or
 * current) host. Matches the addon's `GodotMcpServerView.PlatformName` and the
 * GameDev-MCP-Server release asset RIDs (linux-arm64/linux-x64/osx-arm64/osx-x64/
 * win-arm64/win-x64/win-x86). Pure.
 */
export function detectHostRid(platform: NodeJS.Platform = process.platform, arch: string = process.arch): string {
  return `${osToken(platform)}-${archToken(arch)}`;
}

/** The on-disk executable file name for an os: `gamedev-mcp-server.exe` on Windows, else `gamedev-mcp-server`. */
export function serverExecutableFileName(platform: NodeJS.Platform = process.platform): string {
  return platform === 'win32' ? `${SERVER_EXECUTABLE_NAME}.exe` : SERVER_EXECUTABLE_NAME;
}

/** The GitHub release-zip asset name for a RID: `gamedev-mcp-server-<rid>.zip`. Pure. */
export function serverAssetName(rid: string): string {
  return `${SERVER_ASSET_PREFIX}-${rid}.zip`;
}

/** The git release TAG for a server version: the version with a leading `v` (`9.0.0` → `v9.0.0`). Pure. */
export function serverReleaseTag(version: string): string {
  const v = (version ?? '').trim();
  return v.startsWith('v') ? v : `v${v}`;
}

/**
 * The download URL for a server version's per-RID zip:
 * `https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v<version>/gamedev-mcp-server-<rid>.zip`.
 * Mirrors `GodotMcpServerView.DownloadUrl`. The host is always `github.com` by
 * construction; `assertTrustedServerUrl` re-checks a URL before any network call. Pure.
 */
export function serverDownloadUrl(version: string, rid: string): string {
  return `https://${TRUSTED_DOWNLOAD_HOST}/${SERVER_RELEASE_REPO}/releases/download/${serverReleaseTag(version)}/${serverAssetName(rid)}`;
}

/**
 * The `SHA256SUMS` manifest URL — the sibling of the per-RID zip under the SAME
 * `v<version>` release tag. Mirrors `GodotMcpServerView.Sha256SumsUrl`. Pure.
 */
export function sha256SumsUrl(version: string): string {
  return `https://${TRUSTED_DOWNLOAD_HOST}/${SERVER_RELEASE_REPO}/releases/download/${serverReleaseTag(version)}/${SHA256SUMS_ASSET_NAME}`;
}

/**
 * Fail-closed host trust check: throw unless `url` is an HTTPS URL whose host is
 * EXACTLY `github.com` (no subdomains, no `github.com.evil.test`, no http). The
 * security boundary the task requires for the server download. Returns the parsed
 * URL on success. Pure (throws on the unhappy path; callers turn it into a
 * structured failure so nothing escapes the public boundary).
 */
export function assertTrustedServerUrl(url: string): URL {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`Refusing to download server: malformed URL "${url}".`);
  }
  if (parsed.protocol !== 'https:') {
    throw new Error(`Refusing to download server: only https is allowed, got "${parsed.protocol}" (${url}).`);
  }
  if (parsed.hostname.toLowerCase() !== TRUSTED_DOWNLOAD_HOST) {
    throw new Error(
      `Refusing to download server from untrusted host "${parsed.hostname}". Only ${TRUSTED_DOWNLOAD_HOST} is trusted.`,
    );
  }
  return parsed;
}

// --- SHA256SUMS integrity manifest (download-verify-before-extract, fail-closed) ---
// Ported from GodotMcpServerView.{ParseSha256Sums,LookupDigest,DigestMatches,VerifyZipChecksum}
// so the CLI and the addon apply byte-identical integrity rules.

/** True when `value` is exactly 64 ASCII hex characters (a SHA256 hex digest). */
function isHex64(value: string): boolean {
  if (value.length !== 64) return false;
  return /^[0-9a-fA-F]{64}$/.test(value);
}

/**
 * Parse a coreutils `sha256sum` manifest into a `{filename → lowercase-hex-digest}`
 * map. One line per file: a 64-char hex digest, whitespace, then the file name.
 * Tolerances (matching the C# reference): CRLF + bare-LF; blank lines skipped;
 * leading/trailing whitespace trimmed; the binary-mode `*` marker before the
 * filename stripped; a line whose first token is not 64-hex, or which has no
 * filename, is skipped (never a spurious entry — fail-closed at lookup). On a
 * duplicate filename the LAST entry wins. Never throws. Pure.
 */
export function parseSha256Sums(sha256SumsText: string | null | undefined): Map<string, string> {
  const map = new Map<string, string>();
  if (!sha256SumsText) return map;

  for (const rawLine of sha256SumsText.replace(/\r\n/g, '\n').split('\n')) {
    const line = rawLine.trim();
    if (line.length === 0) continue;

    // Split into the digest token and the remainder (the filename) on the FIRST
    // run of whitespace, so single-space/tab variants still parse.
    let sepIndex = -1;
    for (let i = 0; i < line.length; i++) {
      if (line[i] === ' ' || line[i] === '\t') {
        sepIndex = i;
        break;
      }
    }
    if (sepIndex <= 0 || sepIndex >= line.length - 1) continue;

    const digestToken = line.slice(0, sepIndex);
    if (!isHex64(digestToken)) continue;

    let fileName = line.slice(sepIndex + 1).replace(/^[ \t]+/, '');
    if (fileName.startsWith('*')) fileName = fileName.slice(1);
    fileName = fileName.trim();
    if (fileName.length === 0) continue;

    map.set(fileName, digestToken.toLowerCase());
  }

  return map;
}

/** Look up the expected digest for an asset zip name in a parsed manifest, or null when absent. Pure. */
export function lookupDigest(parsed: Map<string, string>, assetZipName: string): string | null {
  if (!parsed || !assetZipName) return null;
  return parsed.get(assetZipName) ?? null;
}

/** Case-insensitive hex-digest equality (both trimmed). A null/empty side is NEVER a match (fail-closed). Pure. */
export function digestMatches(expectedHexDigest: string | null | undefined, actualHexDigest: string | null | undefined): boolean {
  if (!expectedHexDigest || !expectedHexDigest.trim() || !actualHexDigest || !actualHexDigest.trim()) return false;
  return expectedHexDigest.trim().toLowerCase() === actualHexDigest.trim().toLowerCase();
}

/** The verdict of verifying a downloaded zip against a release `SHA256SUMS` manifest. */
export type ChecksumVerdict = 'verified' | 'manifest-unparsable' | 'missing-entry' | 'digest-mismatch';

/**
 * The single fail-closed integrity decision the installer calls BEFORE extract:
 * parse the release `SHA256SUMS`, find the entry for `assetZipName`, and compare
 * it (case-insensitive hex) against the locally-computed SHA256 of the downloaded
 * zip. Returns `'verified'` ONLY when the manifest parsed, contained the asset,
 * and the digest matched; every other outcome is a distinct fail-closed verdict
 * the caller MUST treat as "do NOT extract". Never throws. Pure.
 */
export function verifyZipChecksum(
  sha256SumsText: string | null | undefined,
  assetZipName: string,
  actualZipHexDigest: string | null | undefined,
): ChecksumVerdict {
  const parsed = parseSha256Sums(sha256SumsText);
  if (parsed.size === 0) return 'manifest-unparsable';

  const expected = lookupDigest(parsed, assetZipName);
  if (expected === null) return 'missing-entry';

  return digestMatches(expected, actualZipHexDigest) ? 'verified' : 'digest-mismatch';
}

/** A short, actionable reason for a non-`verified` verdict, for the installer's fail-closed error. Pure. */
export function checksumFailureReason(verdict: ChecksumVerdict, assetZipName: string): string {
  switch (verdict) {
    case 'manifest-unparsable':
      return `the downloaded ${SHA256SUMS_ASSET_NAME} manifest was empty or unparsable`;
    case 'missing-entry':
      return `the ${SHA256SUMS_ASSET_NAME} manifest has no entry for '${assetZipName}'`;
    case 'digest-mismatch':
      return `the downloaded '${assetZipName}' SHA256 did not match the ${SHA256SUMS_ASSET_NAME} manifest entry`;
    default:
      return 'the checksum was verified';
  }
}

/** The lowercase hex SHA256 of a byte buffer (the downloaded zip). Pure. */
export function sha256Hex(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}
