import { describe, it, expect } from 'vitest';
import { addAddonPackageReferences, addAddonEmbeddedResources } from '../src/utils/csproj-deps.js';
import {
  ADDON_PACKAGE_REFERENCES,
  ADDON_EMBEDDED_RESOURCES,
  CODE_EXEC_IN_GAME_CONDITION,
  EDITOR_ONLY_CONDITION,
} from '../src/utils/addon-deps.js';

const REFLECTOR = ADDON_PACKAGE_REFERENCES[0];
const MCP = ADDON_PACKAGE_REFERENCES[1];
const CATALOG = ADDON_EMBEDDED_RESOURCES[0];

const FRESH_CSPROJ = `<Project Sdk="Godot.NET.Sdk/4.3.0">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Nullable>enable</Nullable>
    <RootNamespace>MyGame</RootNamespace>
  </PropertyGroup>

</Project>
`;

describe('addAddonPackageReferences — fresh csproj (no ItemGroup)', () => {
  it('appends a new ItemGroup with both pins before </Project>', () => {
    const { text, changed, changes } = addAddonPackageReferences(FRESH_CSPROJ);
    expect(changed).toBe(true);
    expect(text).toContain(`<PackageReference Include="${REFLECTOR.id}" Version="${REFLECTOR.version}" />`);
    expect(text).toContain(`<PackageReference Include="${MCP.id}" Version="${MCP.version}" />`);
    // The new ItemGroup is inside the Project, before the closing tag.
    expect(text.indexOf('<ItemGroup>')).toBeLessThan(text.indexOf('</Project>'));
    expect(changes.every((c) => c.action === 'added')).toBe(true);
    // Still valid-looking XML: one </Project>, balanced ItemGroup.
    expect(text.match(/<\/Project>/g)?.length).toBe(1);
  });

  it('is idempotent — patching an already-patched csproj makes no change', () => {
    const once = addAddonPackageReferences(FRESH_CSPROJ).text;
    const twice = addAddonPackageReferences(once);
    expect(twice.changed).toBe(false);
    expect(twice.text).toBe(once);
    expect(twice.changes.every((c) => c.action === 'unchanged')).toBe(true);
  });
});

describe('addAddonPackageReferences — both pins already present at the right version', () => {
  it('reports unchanged and does not duplicate', () => {
    const withPins = addAddonPackageReferences(FRESH_CSPROJ).text;
    const { text, changed } = addAddonPackageReferences(withPins);
    expect(changed).toBe(false);
    // No duplicate references.
    expect(text.match(new RegExp(REFLECTOR.id, 'g'))?.length).toBe(1);
    expect(text.match(new RegExp(MCP.id, 'g'))?.length).toBe(1);
  });
});

describe('addAddonPackageReferences — partial (only one pin present)', () => {
  it('adds the missing pin and leaves the present one untouched', () => {
    const partial = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <PackageReference Include="${REFLECTOR.id}" Version="${REFLECTOR.version}" />
  </ItemGroup>
</Project>
`;
    const { text, changed, changes } = addAddonPackageReferences(partial);
    expect(changed).toBe(true);
    expect(text).toContain(`<PackageReference Include="${MCP.id}" Version="${MCP.version}" />`);
    // ReflectorNet was unchanged; McpPlugin was added.
    expect(changes.find((c) => c.id === REFLECTOR.id)?.action).toBe('unchanged');
    expect(changes.find((c) => c.id === MCP.id)?.action).toBe('added');
    // Both should sit in the same existing ItemGroup (only one ItemGroup).
    expect(text.match(/<ItemGroup>/g)?.length).toBe(1);
  });
});

describe('addAddonPackageReferences — different version present (reconcile)', () => {
  it('rewrites a stale version in place to the addon pin', () => {
    const stale = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <PackageReference Include="${REFLECTOR.id}" Version="1.0.0" />
    <PackageReference Include="${MCP.id}" Version="6.10.0" />
  </ItemGroup>
</Project>
`;
    const { text, changed, changes } = addAddonPackageReferences(stale);
    expect(changed).toBe(true);
    expect(text).not.toContain('Version="1.0.0"');
    expect(text).toContain(`<PackageReference Include="${REFLECTOR.id}" Version="${REFLECTOR.version}" />`);
    const reflectorChange = changes.find((c) => c.id === REFLECTOR.id);
    expect(reflectorChange?.action).toBe('updated');
    if (reflectorChange?.action === 'updated') {
      expect(reflectorChange.from).toBe('1.0.0');
    }
    // No duplicate entries.
    expect(text.match(new RegExp(REFLECTOR.id, 'g'))?.length).toBe(1);
  });

  it('handles Version-before-Include attribute order', () => {
    const reordered = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <PackageReference Version="2.0.0" Include="${REFLECTOR.id}" />
  </ItemGroup>
</Project>
`;
    const { text, changed } = addAddonPackageReferences(reordered);
    expect(changed).toBe(true);
    expect(text).not.toContain('Version="2.0.0"');
    expect(text.match(new RegExp(REFLECTOR.id, 'g'))?.length).toBe(1);
  });
});

describe('addAddonPackageReferences — existing ItemGroup with package refs', () => {
  it('groups the added pins into the existing PackageReference ItemGroup', () => {
    const existing = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SomeOther.Package" Version="1.2.3" />
  </ItemGroup>
</Project>
`;
    const { text, changed } = addAddonPackageReferences(existing);
    expect(changed).toBe(true);
    // Only one ItemGroup — the addon pins joined the existing package group.
    expect(text.match(/<ItemGroup>/g)?.length).toBe(1);
    expect(text).toContain('SomeOther.Package');
    expect(text).toContain(REFLECTOR.id);
    expect(text).toContain(MCP.id);
  });
});

const FRESH_CSPROJ_EMBED = `<Project Sdk="Godot.NET.Sdk/4.3.0">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>MyGame</RootNamespace>
  </PropertyGroup>

</Project>
`;

const EXPECTED_EMBED = `<EmbeddedResource Include="${CATALOG.include}" LogicalName="${CATALOG.logicalName}" />`;

describe('addAddonEmbeddedResources — fresh csproj (no EmbeddedResource)', () => {
  it('appends a new ItemGroup with the catalog embed before </Project>', () => {
    const { text, changed, changes } = addAddonEmbeddedResources(FRESH_CSPROJ_EMBED);
    expect(changed).toBe(true);
    expect(text).toContain(EXPECTED_EMBED);
    expect(text.indexOf('<EmbeddedResource')).toBeLessThan(text.indexOf('</Project>'));
    expect(changes.every((c) => c.action === 'added')).toBe(true);
    // Still valid-looking XML: exactly one </Project>.
    expect(text.match(/<\/Project>/g)?.length).toBe(1);
  });

  it('is idempotent — re-running does not duplicate the <EmbeddedResource>', () => {
    const once = addAddonEmbeddedResources(FRESH_CSPROJ_EMBED).text;
    const twice = addAddonEmbeddedResources(once);
    expect(twice.changed).toBe(false);
    expect(twice.text).toBe(once);
    expect(twice.changes.every((c) => c.action === 'unchanged')).toBe(true);
    // Exactly one occurrence of the embed Include — no duplication.
    expect(once.match(new RegExp(escapeRe(CATALOG.include), 'g'))?.length).toBe(1);
  });
});

describe('addAddonEmbeddedResources — different LogicalName present (reconcile)', () => {
  it('rewrites a wrong LogicalName in place to the canonical one', () => {
    const wrong = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <EmbeddedResource Include="${CATALOG.include}" LogicalName="Stale.Name.json" />
  </ItemGroup>
</Project>
`;
    const { text, changed, changes } = addAddonEmbeddedResources(wrong);
    expect(changed).toBe(true);
    expect(text).not.toContain('Stale.Name.json');
    expect(text).toContain(EXPECTED_EMBED);
    const change = changes.find((c) => c.include === CATALOG.include);
    expect(change?.action).toBe('updated');
    if (change?.action === 'updated') {
      expect(change.from).toBe('Stale.Name.json');
    }
    // No duplicate Include.
    expect(text.match(new RegExp(escapeRe(CATALOG.include), 'g'))?.length).toBe(1);
  });
});

describe('addAddonEmbeddedResources — existing EmbeddedResource ItemGroup', () => {
  it('groups the catalog embed into the existing EmbeddedResource ItemGroup', () => {
    const existing = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <EmbeddedResource Include="data/other.json" LogicalName="Other.json" />
  </ItemGroup>
</Project>
`;
    const { text, changed } = addAddonEmbeddedResources(existing);
    expect(changed).toBe(true);
    // Only one ItemGroup — the catalog embed joined the existing embed group.
    expect(text.match(/<ItemGroup>/g)?.length).toBe(1);
    expect(text).toContain('data/other.json');
    expect(text).toContain(EXPECTED_EMBED);
  });
});

describe('addAddonPackageReferences + addAddonEmbeddedResources — combined scaffold', () => {
  it('produces a csproj with both the pins and the embed in separate ItemGroups', () => {
    const pkg = addAddonPackageReferences(FRESH_CSPROJ_EMBED);
    const both = addAddonEmbeddedResources(pkg.text);
    expect(both.text).toContain(REFLECTOR.id);
    expect(both.text).toContain(MCP.id);
    expect(both.text).toContain(EXPECTED_EMBED);
    // Two ItemGroups: one for the pins, one for the embed (matching the addon csproj layout).
    expect(both.text.match(/<ItemGroup>/g)?.length).toBe(2);
    expect(both.text.match(/<\/Project>/g)?.length).toBe(1);
    // Re-running both passes is a no-op.
    const pkg2 = addAddonPackageReferences(both.text);
    const both2 = addAddonEmbeddedResources(pkg2.text);
    expect(pkg2.changed).toBe(false);
    expect(both2.changed).toBe(false);
    expect(both2.text).toBe(both.text);
  });
});

/** Escape a string for use inside a RegExp (test-local helper). */
function escapeRe(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

describe('addAddonPackageReferences — third-party pins are a floor', () => {
  const refs = [
    { id: 'com.IvanMurzak.Rql', version: '0.1.1' },
    { id: 'Microsoft.CodeAnalysis.CSharp', version: '4.12.0' },
  ];
  const csproj = (rql: string, roslyn: string) =>
    `<Project Sdk="Godot.NET.Sdk/4.5.1">\n  <ItemGroup>\n    <PackageReference Include="com.IvanMurzak.Rql" Version="${rql}" />\n    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="${roslyn}" />\n  </ItemGroup>\n</Project>\n`;

  it('keeps a newer Roslyn the project already uses, but still reconciles IvanMurzak pins exactly', () => {
    const { text, changes } = addAddonPackageReferences(csproj('0.2.0', '4.14.0'), refs);
    expect(text).toContain('Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0"');
    expect(text).toContain('Include="com.IvanMurzak.Rql" Version="0.1.1"');
    expect(changes).toContainEqual({ id: 'Microsoft.CodeAnalysis.CSharp', action: 'unchanged', version: '4.14.0' });
  });

  it('raises an older Roslyn to the pin', () => {
    const { text } = addAddonPackageReferences(csproj('0.1.1', '4.8.0'), refs);
    expect(text).toContain('Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0"');
  });
});

describe('addAddonPackageReferences — editor-only packages', () => {
  const cond = `Condition="${EDITOR_ONLY_CONDITION}"`;
  const refs = [
    { id: 'com.IvanMurzak.McpPlugin', version: '8.9.0' },
    { id: 'com.IvanMurzak.CodeExec', version: '0.2.1', editorOnly: true },
  ];
  const csproj = (...elements: string[]) =>
    `<Project Sdk="Godot.NET.Sdk/4.5.1">\n  <ItemGroup>\n${elements.map((e) => `    ${e}\n`).join('')}  </ItemGroup>\n</Project>\n`;

  it('writes an editor-only package with the export-excluding Condition, the others without one', () => {
    const { text } = addAddonPackageReferences(FRESH_CSPROJ, refs);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${cond} />`);
    expect(text).toContain('<PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.9.0" />');
    expect(addAddonPackageReferences(text, refs).changed).toBe(false);
  });

  it('migrates an unconditioned editor-only package at the right version (an older install)', () => {
    const before = csproj(
      '<PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.9.0" />',
      '<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" />',
    );
    const { text, changes } = addAddonPackageReferences(before, refs);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${cond} />`);
    expect(changes).toEqual([
      { id: 'com.IvanMurzak.McpPlugin', action: 'unchanged', version: '8.9.0' },
      { id: 'com.IvanMurzak.CodeExec', action: 'updated', from: '0.2.1', version: '0.2.1' },
    ]);
  });

  it("keeps the project's own Condition on an editor-only package, also across a version bump", () => {
    const own = `Condition="'$(Configuration)' == 'Debug'"`;
    const before = csproj(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.0" ${own} />`);
    const { text } = addAddonPackageReferences(before, refs);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${own} />`);
    expect(addAddonPackageReferences(text, refs).changed).toBe(false);
  });

  it('drops the editor-only Condition when the installed addon needs the package in export builds', () => {
    const before = csproj(`<PackageReference Include="com.IvanMurzak.Rql" Version="0.1.1" ${cond} />`);
    const { text } = addAddonPackageReferences(before, [{ id: 'com.IvanMurzak.Rql', version: '0.1.1' }]);
    expect(text).toContain('<PackageReference Include="com.IvanMurzak.Rql" Version="0.1.1" />');
  });

  it('keeps the Condition a dev-only project put on a runtime package (McpPlugin)', () => {
    const before = csproj(`<PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.9.0" ${cond} />`);
    expect(addAddonPackageReferences(before, [refs[0]]).changed).toBe(false);
  });

  it("keeps the element's other attributes when it adds the Condition or bumps the version", () => {
    const before = csproj(
      '<PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.0.0" PrivateAssets="all" />',
      '<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" NoWarn="NU1603"></PackageReference>',
    );
    const { text } = addAddonPackageReferences(before, refs);
    expect(text).toContain('<PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.9.0" PrivateAssets="all" />');
    expect(text).toContain(
      `<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" NoWarn="NU1603" ${cond}></PackageReference>`,
    );
    expect(addAddonPackageReferences(text, refs).changed).toBe(false);
  });

  it('leaves a newer third-party package the project uses as it is (no Condition added)', () => {
    const roslyn = { id: 'Microsoft.CodeAnalysis.CSharp', version: '4.12.0', editorOnly: true };
    const before = csproj('<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" />');
    expect(addAddonPackageReferences(before, [roslyn]).changed).toBe(false);
  });
});

describe('addAddonPackageReferences — in-game code execution opt-in (GodotMcpCodeExecInGame)', () => {
  const editorOnly = `Condition="${EDITOR_ONLY_CONDITION}"`;
  const inGame = `Condition="${CODE_EXEC_IN_GAME_CONDITION}"`;
  const codeExec = { id: 'com.IvanMurzak.CodeExec', version: '0.2.1', editorOnly: true, inGameOptIn: true };
  const rql = { id: 'com.IvanMurzak.Rql', version: '0.1.1', editorOnly: true };
  const csproj = (...elements: string[]) =>
    `<Project Sdk="Godot.NET.Sdk/4.5.1">\n  <ItemGroup>\n${elements.map((e) => `    ${e}\n`).join('')}  </ItemGroup>\n</Project>\n`;

  it('is the editor-only condition widened by the MSBuild property', () => {
    expect(CODE_EXEC_IN_GAME_CONDITION).toBe(
      "'$(GodotMcpCodeExecInGame)' == 'true' or ('$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease')",
    );
  });

  it('writes the in-game condition on Code-Exec packages and the editor-only one on RQL', () => {
    const { text } = addAddonPackageReferences(FRESH_CSPROJ, [rql, codeExec]);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${inGame} />`);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.Rql" Version="0.1.1" ${editorOnly} />`);
    expect(addAddonPackageReferences(text, [rql, codeExec]).changed).toBe(false);
  });

  it('upgrades a Code-Exec package an older install wrote editor-only', () => {
    const before = csproj(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${editorOnly} />`);
    const { text, changes } = addAddonPackageReferences(before, [codeExec]);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${inGame} />`);
    expect(changes).toEqual([{ id: 'com.IvanMurzak.CodeExec', action: 'updated', from: '0.2.1', version: '0.2.1' }]);
  });

  it("keeps the project's own Condition on a Code-Exec package", () => {
    const own = `Condition="'$(Configuration)' == 'Debug'"`;
    const before = csproj(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${own} />`);
    expect(addAddonPackageReferences(before, [codeExec]).changed).toBe(false);
  });

  it('moves a newer Roslyn the project pins from the editor-only to the in-game condition, keeping its version', () => {
    const roslyn = { id: 'Microsoft.CodeAnalysis.CSharp', version: '4.12.0', editorOnly: true, inGameOptIn: true };
    const before = csproj(`<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" ${editorOnly} />`);
    const { text, changes } = addAddonPackageReferences(before, [roslyn]);
    expect(text).toContain(`<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" ${inGame} />`);
    expect(changes).toEqual([{ id: 'Microsoft.CodeAnalysis.CSharp', action: 'updated', from: '4.14.0', version: '4.14.0' }]);
    expect(addAddonPackageReferences(text, [roslyn]).changed).toBe(false);
    // Unconditioned (the project's own Roslyn): left alone.
    const own = csproj('<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" />');
    expect(addAddonPackageReferences(own, [roslyn]).changed).toBe(false);
  });

  it('goes back to editor-only for an addon without the in-game opt-in (an older addon)', () => {
    const before = csproj(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${inGame} />`);
    const { text } = addAddonPackageReferences(before, [{ id: 'com.IvanMurzak.CodeExec', version: '0.2.1', editorOnly: true }]);
    expect(text).toContain(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${editorOnly} />`);
  });

  it('drops the in-game condition when the installed addon needs the package in every export build', () => {
    const before = csproj(`<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" ${inGame} />`);
    const { text } = addAddonPackageReferences(before, [{ id: 'com.IvanMurzak.CodeExec', version: '0.2.1' }]);
    expect(text).toContain('<PackageReference Include="com.IvanMurzak.CodeExec" Version="0.2.1" />');
  });

  it('the built-in pins write Code-Exec and Roslyn with the in-game condition, RQL editor-only', () => {
    const { text } = addAddonPackageReferences(FRESH_CSPROJ);
    const inGameRefs = ADDON_PACKAGE_REFERENCES.filter((r) => r.inGameOptIn);
    expect(inGameRefs.map((r) => r.id)).toEqual(['com.IvanMurzak.CodeExec', 'com.IvanMurzak.CodeExec.Roslyn', 'Microsoft.CodeAnalysis.CSharp']);
    for (const ref of inGameRefs) {
      expect(text).toContain(`<PackageReference Include="${ref.id}" Version="${ref.version}" ${inGame} />`);
    }
    const rql = ADDON_PACKAGE_REFERENCES.find((r) => r.id === 'com.IvanMurzak.Rql');
    expect(text).toContain(`Include="com.IvanMurzak.Rql" Version="${rql?.version}" ${editorOnly}`);
  });
});
