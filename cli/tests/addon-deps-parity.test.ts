import { describe, it, expect } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { ADDON_PACKAGE_REFERENCES } from '../src/utils/addon-deps.js';

// The CLI's ADDON_PACKAGE_REFERENCES must single-source the addon's own reused
// NuGet pins (Godot-MCP.csproj). If the addon bumps a pin, this test fails until
// the CLI constant is updated — so a from-scratch terminal install can NEVER write
// a version the addon was not built against. This is the drift tripwire that lets
// the runtime stay a pure constant (no build-time csproj read).
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ADDON_CSPROJ = path.resolve(__dirname, '..', '..', 'Godot-MCP.csproj');

/** Parse `<PackageReference Include="id" Version="x" />` pairs from csproj text. */
function parsePins(text: string): Map<string, string> {
  const pins = new Map<string, string>();
  const re = /<PackageReference\b[^>]*?\bInclude\s*=\s*"([^"]+)"[^>]*?\bVersion\s*=\s*"([^"]+)"/gi;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text)) !== null) {
    pins.set(m[1], m[2]);
  }
  return pins;
}

describe('addon-deps parity with Godot-MCP.csproj', () => {
  it('the csproj is reachable from the test (relative layout sanity)', () => {
    expect(fs.existsSync(ADDON_CSPROJ)).toBe(true);
  });

  it('every CLI-written addon pin matches the addon csproj version exactly', () => {
    const text = fs.readFileSync(ADDON_CSPROJ, 'utf-8');
    const csprojPins = parsePins(text);

    for (const ref of ADDON_PACKAGE_REFERENCES) {
      const csprojVersion = csprojPins.get(ref.id);
      expect(
        csprojVersion,
        `Godot-MCP.csproj is missing a <PackageReference> for ${ref.id}`,
      ).toBeDefined();
      expect(
        ref.version,
        `CLI addon pin ${ref.id}@${ref.version} drifted from Godot-MCP.csproj ${ref.id}@${csprojVersion}. ` +
          'Update ADDON_PACKAGE_REFERENCES in cli/src/utils/addon-deps.ts to match the addon pin.',
      ).toBe(csprojVersion);
    }
  });
});
