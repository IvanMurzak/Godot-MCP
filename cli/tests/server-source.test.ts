import { describe, it, expect } from 'vitest';
import {
  detectHostRid,
  osToken,
  archToken,
  serverAssetName,
  serverExecutableFileName,
  serverReleaseTag,
  serverDownloadUrl,
  sha256SumsUrl,
  assertTrustedServerUrl,
  parseSha256Sums,
  lookupDigest,
  digestMatches,
  verifyZipChecksum,
  checksumFailureReason,
  sha256Hex,
  TRUSTED_DOWNLOAD_HOST,
} from '../src/utils/server-source.js';

describe('server-source — RID detection', () => {
  it('maps os tokens like the addon (win/osx/linux)', () => {
    expect(osToken('win32')).toBe('win');
    expect(osToken('darwin')).toBe('osx');
    expect(osToken('linux')).toBe('linux');
    expect(osToken('aix' as NodeJS.Platform)).toBe('unknown');
  });

  it('maps arch tokens (x64/arm64/x86/arm)', () => {
    expect(archToken('x64')).toBe('x64');
    expect(archToken('arm64')).toBe('arm64');
    expect(archToken('ia32')).toBe('x86');
    expect(archToken('arm')).toBe('arm');
    expect(archToken('mips')).toBe('unknown');
  });

  it('composes <os>-<arch> matching the released RIDs', () => {
    expect(detectHostRid('win32', 'x64')).toBe('win-x64');
    expect(detectHostRid('linux', 'arm64')).toBe('linux-arm64');
    expect(detectHostRid('darwin', 'arm64')).toBe('osx-arm64');
    expect(detectHostRid('win32', 'ia32')).toBe('win-x86');
  });

  it('names the executable per-OS', () => {
    expect(serverExecutableFileName('win32')).toBe('gamedev-mcp-server.exe');
    expect(serverExecutableFileName('linux')).toBe('gamedev-mcp-server');
    expect(serverExecutableFileName('darwin')).toBe('gamedev-mcp-server');
  });
});

describe('server-source — URLs (github.com only, v-tag)', () => {
  it('builds the RID-matched download URL', () => {
    expect(serverDownloadUrl('9.0.0', 'win-x64')).toBe(
      'https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v9.0.0/gamedev-mcp-server-win-x64.zip',
    );
  });

  it('builds the SHA256SUMS sibling URL under the same tag', () => {
    expect(sha256SumsUrl('9.0.0')).toBe(
      'https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v9.0.0/SHA256SUMS',
    );
  });

  it('v-prefixes the tag but never double-prefixes', () => {
    expect(serverReleaseTag('9.0.0')).toBe('v9.0.0');
    expect(serverReleaseTag('v9.0.0')).toBe('v9.0.0');
  });

  it('asset name is gamedev-mcp-server-<rid>.zip', () => {
    expect(serverAssetName('linux-x64')).toBe('gamedev-mcp-server-linux-x64.zip');
  });

  it('accepts a trusted github.com https URL', () => {
    expect(assertTrustedServerUrl(serverDownloadUrl('9.0.0', 'osx-x64')).hostname).toBe(TRUSTED_DOWNLOAD_HOST);
  });

  it('rejects non-github hosts, http, and lookalike domains (fail-closed)', () => {
    expect(() => assertTrustedServerUrl('http://github.com/x')).toThrow(/https/);
    expect(() => assertTrustedServerUrl('https://evil.test/x')).toThrow(/untrusted host/);
    expect(() => assertTrustedServerUrl('https://github.com.evil.test/x')).toThrow(/untrusted host/);
    expect(() => assertTrustedServerUrl('not a url')).toThrow(/malformed/);
  });
});

describe('server-source — SHA256SUMS parsing (ported from the addon)', () => {
  const digestA = 'a'.repeat(64);
  const digestB = 'b'.repeat(64);

  it('parses the coreutils two-space format', () => {
    const text = `${digestA}  gamedev-mcp-server-win-x64.zip\n${digestB}  gamedev-mcp-server-linux-x64.zip\n`;
    const map = parseSha256Sums(text);
    expect(map.get('gamedev-mcp-server-win-x64.zip')).toBe(digestA);
    expect(map.get('gamedev-mcp-server-linux-x64.zip')).toBe(digestB);
  });

  it('tolerates CRLF, the binary-mode * marker, and lowercases the digest', () => {
    const text = `${digestA.toUpperCase()} *gamedev-mcp-server-win-x64.zip\r\n\r\n`;
    const map = parseSha256Sums(text);
    expect(map.get('gamedev-mcp-server-win-x64.zip')).toBe(digestA);
  });

  it('skips lines whose first token is not a 64-hex digest (fail-closed, no spurious entry)', () => {
    const map = parseSha256Sums(`not-a-digest  file.zip\n${digestA}  ok.zip\n`);
    expect(map.has('file.zip')).toBe(false);
    expect(map.get('ok.zip')).toBe(digestA);
  });

  it('an empty/garbage manifest yields an empty map', () => {
    expect(parseSha256Sums('').size).toBe(0);
    expect(parseSha256Sums(null).size).toBe(0);
    expect(parseSha256Sums('   \n  ').size).toBe(0);
  });

  it('lookupDigest + digestMatches are fail-closed on null/empty', () => {
    const map = parseSha256Sums(`${digestA}  a.zip`);
    expect(lookupDigest(map, 'a.zip')).toBe(digestA);
    expect(lookupDigest(map, 'missing.zip')).toBeNull();
    expect(digestMatches(digestA, digestA.toUpperCase())).toBe(true);
    expect(digestMatches(digestA, digestB)).toBe(false);
    expect(digestMatches(null, digestA)).toBe(false);
    expect(digestMatches(digestA, '')).toBe(false);
  });
});

describe('server-source — verifyZipChecksum verdicts (fail-closed)', () => {
  const asset = 'gamedev-mcp-server-win-x64.zip';
  const zip = Buffer.from('the-real-server-zip-bytes');
  const digest = sha256Hex(zip);
  const manifest = `${digest}  ${asset}\n`;

  it('verified when the manifest parses, has the asset, and the digest matches', () => {
    expect(verifyZipChecksum(manifest, asset, digest)).toBe('verified');
  });

  it('manifest-unparsable when the manifest is empty/garbage', () => {
    expect(verifyZipChecksum('', asset, digest)).toBe('manifest-unparsable');
    expect(checksumFailureReason('manifest-unparsable', asset)).toMatch(/unparsable/);
  });

  it('missing-entry when the manifest has no line for this asset', () => {
    expect(verifyZipChecksum(`${'c'.repeat(64)}  other.zip`, asset, digest)).toBe('missing-entry');
    expect(checksumFailureReason('missing-entry', asset)).toContain(asset);
  });

  it('digest-mismatch when the recomputed SHA differs (tamper detection)', () => {
    const tampered = sha256Hex(Buffer.from('tampered-bytes'));
    expect(verifyZipChecksum(manifest, asset, tampered)).toBe('digest-mismatch');
    expect(checksumFailureReason('digest-mismatch', asset)).toContain(asset);
  });
});
