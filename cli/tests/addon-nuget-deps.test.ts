import { describe, it, expect, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';
import { ADDON_PACKAGE_REFERENCES } from '../src/utils/addon-deps.js';
import { parseAddonNugetDeps, parseCsprojPackageReferences } from '../src/utils/addon-nuget-deps.js';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const GENERATOR = path.join(REPO_ROOT, 'scripts', 'write-nuget-deps.mjs');

describe('parseAddonNugetDeps', () => {
  it('reads the packageReferences list in order', () => {
    const text = JSON.stringify({
      schemaVersion: 1,
      packageReferences: [
        { id: 'com.IvanMurzak.ReflectorNet', version: '5.4.1' },
        { id: 'com.IvanMurzak.McpPlugin', version: '8.7.0-rc.1' },
      ],
    });
    expect(parseAddonNugetDeps(text)).toEqual([
      { id: 'com.IvanMurzak.ReflectorNet', version: '5.4.1' },
      { id: 'com.IvanMurzak.McpPlugin', version: '8.7.0-rc.1' },
    ]);
  });

  it.each([
    ['not JSON', '{'],
    ['null', 'null'],
    ['missing list', '{}'],
    ['empty list', '{"packageReferences":[]}'],
    ['non-string version', '{"packageReferences":[{"id":"a","version":1}]}'],
    ['XML-breaking version', '{"packageReferences":[{"id":"a","version":"1\\" X=\\""}]}'],
    ['XML-breaking id', '{"packageReferences":[{"id":"a<b","version":"1.0.0"}]}'],
  ])('rejects %s', (_label, text) => {
    expect(() => parseAddonNugetDeps(text)).toThrow();
  });
});

describe('parseCsprojPackageReferences', () => {
  it('reads every PackageReference in either attribute order and skips version-less ones', () => {
    const csproj = `<Project>
  <ItemGroup>
    <PackageReference Include="A.B" Version="1.2.3" />
    <PackageReference Version="4.5.6" Include="C.D"></PackageReference>
    <PackageReference Include="NoVersion" />
  </ItemGroup>
</Project>`;
    expect(parseCsprojPackageReferences(csproj)).toEqual([
      { id: 'A.B', version: '1.2.3' },
      { id: 'C.D', version: '4.5.6' },
    ]);
  });
});

describe('scripts/write-nuget-deps.mjs (release-time manifest generator)', () => {
  let out: string | undefined;
  afterEach(() => {
    if (out) fs.rmSync(path.dirname(out), { recursive: true, force: true });
    out = undefined;
  });

  it('writes a manifest the CLI reads back as exactly the Godot-MCP.csproj pins', () => {
    out = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-nuget-deps-')), 'nuget-deps.json');
    execFileSync(process.execPath, [GENERATOR, out], { stdio: 'pipe' });

    const fromManifest = parseAddonNugetDeps(fs.readFileSync(out, 'utf-8'));
    const fromCsproj = parseCsprojPackageReferences(fs.readFileSync(path.join(REPO_ROOT, 'Godot-MCP.csproj'), 'utf-8'));
    expect(fromManifest).toEqual(fromCsproj);
    // ...and those are what the CLI's built-in fallback carries (addon-deps parity).
    expect(fromManifest).toEqual([...ADDON_PACKAGE_REFERENCES]);
  });
});
