import { describe, it, expect } from 'vitest';
import { isValidVersion, isNewerVersion } from '../src/utils/semver.js';

describe('semver — isValidVersion', () => {
  it('accepts plain numeric versions', () => {
    expect(isValidVersion('1')).toBe(true);
    expect(isValidVersion('1.2')).toBe(true);
    expect(isValidVersion('1.2.3')).toBe(true);
    expect(isValidVersion('10.20.30')).toBe(true);
  });

  it('rejects prerelease / build-metadata / non-numeric forms', () => {
    expect(isValidVersion('1.2.3-beta')).toBe(false);
    expect(isValidVersion('1.2.3+build')).toBe(false);
    expect(isValidVersion('v1.2.3')).toBe(false);
    expect(isValidVersion('')).toBe(false);
    expect(isValidVersion('abc')).toBe(false);
  });
});

describe('semver — isNewerVersion', () => {
  it('detects a strictly newer latest version', () => {
    expect(isNewerVersion('1.0.0', '1.0.1')).toBe(true);
    expect(isNewerVersion('1.0.0', '1.1.0')).toBe(true);
    expect(isNewerVersion('1.0.0', '2.0.0')).toBe(true);
  });

  it('returns false for an equal version', () => {
    expect(isNewerVersion('1.2.3', '1.2.3')).toBe(false);
  });

  it('returns false when the latest is older', () => {
    expect(isNewerVersion('2.0.0', '1.9.9')).toBe(false);
    expect(isNewerVersion('1.2.3', '1.2.2')).toBe(false);
  });

  it('compares versions of differing segment counts (missing segments are 0)', () => {
    expect(isNewerVersion('1.2', '1.2.0')).toBe(false);
    expect(isNewerVersion('1.2', '1.2.1')).toBe(true);
    expect(isNewerVersion('1.2.0', '1.2')).toBe(false);
  });

  it('does NOT compare numbers lexicographically (10 > 9)', () => {
    expect(isNewerVersion('0.9.0', '0.10.0')).toBe(true);
    expect(isNewerVersion('0.10.0', '0.9.0')).toBe(false);
  });
});
