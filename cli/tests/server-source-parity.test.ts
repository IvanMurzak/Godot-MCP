import { describe, it, expect } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { DEFAULT_SERVER_VERSION } from '../src/utils/server-source.js';

// The CLI's DEFAULT_SERVER_VERSION must single-source the addon's own pinned
// `GodotMcpServerView.ServerVersion` constant. If the addon bumps the consumed
// server, this test fails until the CLI constant is updated — so `--with-server`
// can NEVER download a server version the addon was not built to pin. This is the
// drift tripwire that lets the CLI runtime stay a pure constant (no build-time
// C#-source read), mirroring addon-deps-parity.test.ts.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SERVER_VIEW_CS = path.resolve(
  __dirname,
  '..',
  '..',
  'addons',
  'godot_mcp',
  'Runtime',
  'Connection',
  'GodotMcpServerView.cs',
);

/** Extract `public const string ServerVersion = "x.y.z";` from the addon source. */
function parseAddonServerVersion(text: string): string | null {
  const m = text.match(/public\s+const\s+string\s+ServerVersion\s*=\s*"([^"]+)"\s*;/);
  return m ? m[1] : null;
}

describe('server-source parity with GodotMcpServerView.cs', () => {
  it('the addon ServerView source is reachable (relative layout sanity)', () => {
    expect(fs.existsSync(SERVER_VIEW_CS)).toBe(true);
  });

  it('DEFAULT_SERVER_VERSION matches the addon ServerVersion constant exactly', () => {
    const text = fs.readFileSync(SERVER_VIEW_CS, 'utf-8');
    const addonVersion = parseAddonServerVersion(text);
    expect(addonVersion, 'GodotMcpServerView.cs is missing a `public const string ServerVersion = "…"`').not.toBeNull();
    expect(
      DEFAULT_SERVER_VERSION,
      `CLI DEFAULT_SERVER_VERSION (${DEFAULT_SERVER_VERSION}) drifted from the addon ServerVersion (${addonVersion}). ` +
        'Update DEFAULT_SERVER_VERSION in cli/src/utils/server-source.ts to match the addon pin.',
    ).toBe(addonVersion);
  });
});
