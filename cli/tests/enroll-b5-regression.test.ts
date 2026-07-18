import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { enrollPlugin } from '../src/lib/enroll.js';
import { readProjectMarker } from '../src/utils/project-marker.js';
import { derivePin, derivePinV2, derivePortV2 } from '../src/utils/project-identity.js';

/**
 * Regression coverage for defect **B5** (auth-fixes 01 §5 / 07 §7): the Godot CLI's `enroll` wrote
 * a routing pin derived from a Windows `path.resolve` **backslash** root, but the Godot plugin
 * reports its project root with **forward** slashes (`GlobalizePath("res://")`). Under the legacy
 * separator-sensitive v1 hash the two forms produced DIFFERENT pins, so the enroll-written pin never
 * matched the plugin's hash and cloud routing silently broke on Windows.
 *
 * The fix (task i1-godot-cli-migration): `enroll` now derives the pin with the shared
 * `@baizor/gamedev-cli-core` **v2** algorithm, which converts `\`→`/` before hashing so both forms
 * collapse to one pin. These assertions run identically on every OS because they hash literal path
 * STRINGS — the Windows-only defect is reproducible on the Linux CI host.
 */
const WIN_ROOT = 'C:\\Users\\dev\\my-game';
const POSIX_ROOT = 'C:/Users/dev/my-game';

describe('B5 — v2 pin normalizes Windows backslashes (derivation level)', () => {
  it('v2 pin + port are identical for the backslash and forward-slash forms of one root (the fix)', () => {
    expect(derivePinV2(WIN_ROOT)).toBe(derivePinV2(POSIX_ROOT));
    expect(derivePortV2(WIN_ROOT)).toBe(derivePortV2(POSIX_ROOT));
  });

  it('the legacy v1 pin DIFFERS across the two forms (the bug the v2 pin closes)', () => {
    // Proves the regression is real: v1 is separator-sensitive, so it emitted a mismatched pin.
    expect(derivePin(WIN_ROOT)).not.toBe(derivePin(POSIX_ROOT));
  });

  it('a trailing separator never changes the v2 pin', () => {
    expect(derivePinV2('C:\\Users\\dev\\my-game\\')).toBe(derivePinV2(WIN_ROOT));
    expect(derivePinV2('/home/u/g/')).toBe(derivePinV2('/home/u/g'));
  });

  it('golden-anchored: both slash forms of C:\\Users\\user\\my-game hash to the v1 forward-slash pin', () => {
    // The committed ProjectIdentity golden vectors pin the FORWARD-slash form
    // `C:/Users/user/my-game` → `5a87324e`. Under v2, the backslash form normalizes to the same
    // string, so BOTH forms derive `5a87324e` — the C#↔TS golden-gated value cli-core guarantees.
    expect(derivePinV2('C:\\Users\\user\\my-game')).toBe('5a87324e');
    expect(derivePinV2('C:/Users/user/my-game')).toBe('5a87324e');
    // Sanity: the v1 backslash pin is the DIFFERENT golden value `8ef72cf7` (the pre-fix behavior).
    expect(derivePin('C:\\Users\\user\\my-game')).toBe('8ef72cf7');
  });
});

describe('B5 — enroll writes the v2 pin into the project marker', () => {
  let project: string;
  let store: string;
  beforeEach(() => {
    project = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-b5-proj-'));
    store = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-b5-store-'));
  });
  afterEach(() => {
    fs.rmSync(project, { recursive: true, force: true });
    fs.rmSync(store, { recursive: true, force: true });
  });

  it('records derivePinV2(projectRoot) — the v2 pin, matching the plugin hash', async () => {
    const redeem: typeof fetch = (async () =>
      ({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: async () => ({
          access_token: 'plugin.jwt',
          token_type: 'Bearer',
          expires_in: 3600,
          refresh_token: 'refresh.jwt',
          scope: 'mcp:plugin',
          server_url: 'https://ai-game.dev',
        }),
      }) as unknown as Response) as unknown as typeof fetch;

    const result = await enrollPlugin({
      godotProjectPath: project,
      code: 'CODE-1',
      storeBaseDir: store,
      fetchImpl: redeem,
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    // `enrollPlugin` resolves the project path with `path.resolve`, so the pin it writes is the v2
    // pin of the resolved root — the same algorithm the addon's GodotProjectIdentity uses.
    const resolved = path.resolve(project);
    expect(result.pin).toBe(derivePinV2(resolved));
    expect(readProjectMarker(project)?.pin).toBe(derivePinV2(resolved));
  });
});
