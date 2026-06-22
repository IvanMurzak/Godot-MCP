import { describe, it, expect } from 'vitest';
import { addAddonPackageReferences } from '../src/utils/csproj-deps.js';
import { ADDON_PACKAGE_REFERENCES } from '../src/utils/addon-deps.js';

const REFLECTOR = ADDON_PACKAGE_REFERENCES[0];
const MCP = ADDON_PACKAGE_REFERENCES[1];

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
