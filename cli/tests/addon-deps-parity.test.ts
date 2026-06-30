import { describe, it, expect } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { ADDON_PACKAGE_REFERENCES, ADDON_EMBEDDED_RESOURCES } from '../src/utils/addon-deps.js';

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

/** Parse `<EmbeddedResource Include="path" LogicalName="name" />` pairs from csproj text. */
function parseEmbeds(text: string): Map<string, string> {
  const embeds = new Map<string, string>();
  // Match each <EmbeddedResource> element, then extract Include + LogicalName from the
  // captured element text separately — mirrors the production `embeddedResourceRegex`
  // approach (cli/src/utils/csproj-deps.ts) so the parser stays order-agnostic
  // (Include / LogicalName in either attribute order).
  const re = /<EmbeddedResource\b[^>]*?(?:\/>|><\/EmbeddedResource>)/gi;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text)) !== null) {
    const include = m[0].match(/\bInclude\s*=\s*"([^"]+)"/i);
    const logicalName = m[0].match(/\bLogicalName\s*=\s*"([^"]+)"/i);
    if (include && logicalName) {
      embeds.set(include[1], logicalName[1]);
    }
  }
  return embeds;
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

  it('every CLI-written EmbeddedResource matches the addon csproj Include + LogicalName exactly', () => {
    const text = fs.readFileSync(ADDON_CSPROJ, 'utf-8');
    const csprojEmbeds = parseEmbeds(text);

    for (const res of ADDON_EMBEDDED_RESOURCES) {
      const csprojLogical = csprojEmbeds.get(res.include);
      expect(
        csprojLogical,
        `Godot-MCP.csproj is missing an <EmbeddedResource> with Include="${res.include}"`,
      ).toBeDefined();
      expect(
        res.logicalName,
        `CLI addon embed ${res.include} (LogicalName="${res.logicalName}") drifted from ` +
          `Godot-MCP.csproj LogicalName="${csprojLogical}". ` +
          'Update ADDON_EMBEDDED_RESOURCES in cli/src/utils/addon-deps.ts to match the addon csproj.',
      ).toBe(csprojLogical);
    }
  });
});
