import { describe, it, expect } from 'vitest';
import {
  TRUSTED_DOWNLOAD_HOST,
  ADDON_RELEASE_REPO,
  addonAssetName,
  releaseTag,
  stripLeadingV,
  addonDownloadUrl,
  assertTrustedDownloadUrl,
} from '../src/utils/addon-source.js';

describe('addon-source — pure URL/tag helpers', () => {
  it('addonAssetName builds godot-mcp-addon-<version>.zip (no leading v)', () => {
    expect(addonAssetName('0.11.1')).toBe('godot-mcp-addon-0.11.1.zip');
    expect(addonAssetName('v0.11.1')).toBe('godot-mcp-addon-0.11.1.zip');
  });

  it('releaseTag v-prefixes the version, idempotently', () => {
    expect(releaseTag('0.11.1')).toBe('v0.11.1');
    expect(releaseTag('v0.11.1')).toBe('v0.11.1');
  });

  it('stripLeadingV removes a single leading v/V', () => {
    expect(stripLeadingV('v1.2.3')).toBe('1.2.3');
    expect(stripLeadingV('V1.2.3')).toBe('1.2.3');
    expect(stripLeadingV('1.2.3')).toBe('1.2.3');
  });

  it('addonDownloadUrl targets the IvanMurzak/Godot-MCP github release asset', () => {
    const url = addonDownloadUrl('0.11.1');
    expect(url).toBe(
      `https://${TRUSTED_DOWNLOAD_HOST}/${ADDON_RELEASE_REPO}/releases/download/v0.11.1/godot-mcp-addon-0.11.1.zip`,
    );
    // The constructed URL is, by construction, on the trusted host.
    expect(() => assertTrustedDownloadUrl(url)).not.toThrow();
  });
});

describe('assertTrustedDownloadUrl — fail-closed host trust', () => {
  it('accepts an https github.com URL', () => {
    expect(() => assertTrustedDownloadUrl('https://github.com/IvanMurzak/Godot-MCP/releases/download/v1/x.zip')).not.toThrow();
  });

  it('rejects a non-github host', () => {
    expect(() => assertTrustedDownloadUrl('https://evil.test/x.zip')).toThrowError(/untrusted host/i);
  });

  it('rejects a look-alike subdomain attack (github.com.evil.test)', () => {
    expect(() => assertTrustedDownloadUrl('https://github.com.evil.test/x.zip')).toThrowError(/untrusted host/i);
  });

  it('rejects a github.com subdomain (raw.github.com is not the release host)', () => {
    expect(() => assertTrustedDownloadUrl('https://raw.github.com/x.zip')).toThrowError(/untrusted host/i);
  });

  it('rejects plain http', () => {
    expect(() => assertTrustedDownloadUrl('http://github.com/x.zip')).toThrowError(/only https/i);
  });

  it('rejects a malformed URL', () => {
    expect(() => assertTrustedDownloadUrl('not a url')).toThrowError(/malformed URL/i);
  });
});
