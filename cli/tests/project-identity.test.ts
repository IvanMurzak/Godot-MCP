import { describe, it, expect } from 'vitest';
import {
  MIN_PORT,
  MAX_PORT,
  PORT_RANGE,
  PIN_LENGTH,
  normalize,
  toLowerInvariant,
  derivePin,
  derivePort,
  deriveProjectPathHash,
  deriveProjectIdentity,
} from '../src/utils/project-identity.js';

/**
 * Golden vectors copied VERBATIM (inline, no runtime file dependency) from the committed
 * `MCP-Plugin-dotnet/McpPlugin/src/AgentConfig/ProjectIdentity.GoldenVectors.json`. The C# reference
 * implementation is the origin; this TS port MUST reproduce every pin/port byte-for-byte, including
 * the trailing-separator, no-separator-conversion, and U+0130 ToLowerInvariant behaviors.
 */
const GOLDEN_VECTORS: ReadonlyArray<{ path: string; pin: string; port: number; note: string }> = [
  { path: '/home/user/my-game', pin: '34ea75f2', port: 23940, note: 'POSIX typical project path.' },
  { path: '/home/user/my-game/', pin: '34ea75f2', port: 23940, note: 'Trailing slash trimmed.' },
  { path: '/home/USER/My-Game', pin: '34ea75f2', port: 23940, note: 'Case-folded.' },
  { path: 'C:\\Users\\user\\my-game', pin: '8ef72cf7', port: 29310, note: 'Windows backslash form.' },
  { path: 'C:\\Users\\user\\my-game\\', pin: '8ef72cf7', port: 29310, note: 'Trailing backslash trimmed.' },
  { path: 'C:/Users/user/my-game', pin: '5a87324e', port: 24298, note: 'Forward-slash form DIFFERS (no separator normalization).' },
  { path: '/home/İstanbul/game', pin: '672d80a7', port: 25303, note: 'U+0130 — C# ToLowerInvariant leaves it unchanged.' },
  { path: '/srv/games/space sim', pin: '08c6cbb6', port: 27816, note: 'Path containing a space.' },
];

// From the golden file's `unicodeDivergence`: a NAIVE JS toLowerCase() port would produce THIS
// (incorrect) value for the U+0130 path. Pinned here so the special-casing can never silently regress.
const U0130_PATH = '/home/İstanbul/game';
const U0130_NAIVE_JS_PIN = '77300275';
const U0130_NAIVE_JS_PORT = 27751;

describe('ProjectIdentity — golden-vector parity (byte-for-byte with the C# reference)', () => {
  for (const vector of GOLDEN_VECTORS) {
    it(`pin+port match for ${JSON.stringify(vector.path)} (${vector.note})`, () => {
      expect(derivePin(vector.path)).toBe(vector.pin);
      expect(derivePort(vector.path)).toBe(vector.port);
      const identity = deriveProjectIdentity(vector.path);
      expect(identity.pin).toBe(vector.pin);
      expect(identity.port).toBe(vector.port);
      expect(identity.portIsOverridden).toBe(false);
    });
  }

  it('trailing separators do not change the identity (a project and its slashed form match)', () => {
    expect(derivePin('/home/user/my-game/')).toBe(derivePin('/home/user/my-game'));
    expect(derivePort('/home/user/my-game/')).toBe(derivePort('/home/user/my-game'));
  });

  it('separators are NOT normalized: the backslash and forward-slash Windows forms differ', () => {
    expect(derivePin('C:\\Users\\user\\my-game')).not.toBe(derivePin('C:/Users/user/my-game'));
    expect(derivePort('C:\\Users\\user\\my-game')).not.toBe(derivePort('C:/Users/user/my-game'));
  });

  it('reproduces the canonical U+0130 value and NOT the naive JS toLowerCase() value', () => {
    // Canonical (ToLowerInvariant leaves U+0130 unchanged):
    expect(derivePin(U0130_PATH)).toBe('672d80a7');
    expect(derivePort(U0130_PATH)).toBe(25303);
    // Must differ from the documented naive-toLowerCase divergence:
    expect(derivePin(U0130_PATH)).not.toBe(U0130_NAIVE_JS_PIN);
    expect(derivePort(U0130_PATH)).not.toBe(U0130_NAIVE_JS_PORT);
  });
});

describe('ProjectIdentity — normalization', () => {
  it('trims trailing separators (both / and \\) but keeps at least one char', () => {
    expect(normalize('/a/b/')).toBe('/a/b');
    expect(normalize('/a/b\\')).toBe('/a/b');
    expect(normalize('/a/b///')).toBe('/a/b');
    expect(normalize('/')).toBe('/');
  });

  it('lowercases invariantly (ASCII folds; U+0130 is preserved)', () => {
    expect(normalize('/Home/USER/My-Game')).toBe('/home/user/my-game');
    // toLowerInvariant keeps U+0130 (unlike a naive toLowerCase which folds it to 'i' + U+0307).
    expect(toLowerInvariant('İ')).toBe('İ');
    expect('İ'.toLowerCase()).not.toBe('İ'); // sanity: the naive fold DOES change it
  });

  it('does not convert separators during normalization', () => {
    expect(normalize('C:\\A')).toBe('c:\\a');
    expect(normalize('C:/A')).toBe('c:/a');
    expect(normalize('C:\\A')).not.toBe(normalize('C:/A'));
  });
});

describe('ProjectIdentity — derivation surface', () => {
  it('an explicit port override wins for port but never for pin', () => {
    const base = deriveProjectIdentity('/home/user/my-game');
    const overridden = deriveProjectIdentity('/home/user/my-game', 51234);
    expect(overridden.port).toBe(51234);
    expect(overridden.portIsOverridden).toBe(true);
    expect(overridden.pin).toBe(base.pin); // pin is always hash-derived
  });

  it('null/undefined override yields the hash-derived port', () => {
    expect(deriveProjectIdentity('/home/user/my-game', null).port).toBe(23940);
    expect(deriveProjectIdentity('/home/user/my-game', undefined).port).toBe(23940);
    expect(deriveProjectIdentity('/home/user/my-game', null).portIsOverridden).toBe(false);
  });

  it('the pin is the lowercase-hex prefix of the full 64-char project path hash', () => {
    const full = deriveProjectPathHash('/home/user/my-game');
    expect(full).toHaveLength(64);
    expect(full).toMatch(/^[0-9a-f]{64}$/);
    expect(full.startsWith(derivePin('/home/user/my-game'))).toBe(true);
    expect(derivePin('/home/user/my-game')).toHaveLength(PIN_LENGTH);
  });

  it('derived ports always fall inside the 20000-29999 range', () => {
    for (const p of ['/a', '/b', '/c/d/e', 'C:\\x\\y', '/srv/games/space sim', '/home/İstanbul/game']) {
      const port = derivePort(p);
      expect(port).toBeGreaterThanOrEqual(MIN_PORT);
      expect(port).toBeLessThanOrEqual(MAX_PORT);
    }
    expect(PORT_RANGE).toBe(10000);
    expect(MAX_PORT - MIN_PORT + 1).toBe(PORT_RANGE);
  });
});
