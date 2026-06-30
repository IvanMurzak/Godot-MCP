import { describe, it, expect } from 'vitest';
import {
  planExtensionInstall,
  parsePackageReferences,
  compareVersions,
  CsprojParseError,
} from '../src/utils/extension-install.js';
import type { ExtensionDescriptor } from '../src/utils/extensions-catalog.js';

// This suite is the CLI half of the behavioral-parity contract with the dock's
// ExtensionInstaller / ExtensionInstallPlanner. The scenarios mirror
// `Godot-MCP.Tests/ExtensionInstallTests.cs` 1:1 so a divergence in either
// implementation fails a test (see addons/godot_mcp/extensions.catalog.md).

// The SAME representative SDK-style consumer .csproj the C# tests use.
const SampleCsproj = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.3.1" />
    <PackageReference Include="com.IvanMurzak.McpPlugin" Version="6.7.0" />
  </ItemGroup>
  <ItemGroup>
    <Compile Remove="Godot-MCP.Tests/**/*.cs" />
  </ItemGroup>
</Project>`;

function descriptor(
  packageId = 'com.IvanMurzak.Godot.MCP.ProBuilder',
  version: string | null = '1.2.0',
): ExtensionDescriptor {
  return {
    name: 'ProBuilder Tools',
    description: 'Mesh-editing MCP tools for Godot.',
    packageId,
    version,
    gitUrl: null,
    tools: [],
  };
}

describe('compareVersions — numeric + tolerant (parity with C# InstalledStateDetector)', () => {
  it.each([
    ['1.2.0', '1.2.0', 0],
    ['1.10.0', '1.2.0', 1], // numeric, not ordinal — 10 > 2
    ['1.2.0', '1.10.0', -1],
    ['2.0.0', '1.9.9', 1],
    ['1.0', '1.0.0', 0], // missing trailing component == 0
    ['1.0.0-rc1', '1.0.0', 0], // pre-release suffix tolerated (leading int only)
  ])('compareVersions(%s, %s) sign === %i', (a, b, expectedSign) => {
    expect(Math.sign(compareVersions(a, b))).toBe(expectedSign);
  });
});

describe('parsePackageReferences — attribute + child-element + unversioned forms', () => {
  it('parses all three MSBuild forms', () => {
    const mixed = `<Project>
  <ItemGroup>
    <PackageReference Include="A" Version="1.0.0" />
    <PackageReference Include="B"><Version>2.3.4</Version></PackageReference>
    <PackageReference Include="C" />
  </ItemGroup>
</Project>`;
    const refs = parsePackageReferences(mixed);
    expect(refs.get('a')).toBe('1.0.0');
    expect(refs.get('b')).toBe('2.3.4');
    expect(refs.get('c')).toBe('');
  });

  it.each([null, '', '   '])('returns empty for %j', (text) => {
    expect(parsePackageReferences(text as string).size).toBe(0);
  });

  it('package id match is case-insensitive', () => {
    const refs = parsePackageReferences(SampleCsproj);
    expect(refs.get('com.ivanmurzak.mcpplugin')).toBe('6.7.0');
  });
});

describe('planExtensionInstall — ADD', () => {
  it('adds the reference when absent', () => {
    const plan = planExtensionInstall(descriptor(), SampleCsproj);
    expect(plan.action).toBe('add');
    expect(plan.fromVersion).toBeNull();
    expect(plan.toVersion).toBe('1.2.0');
    expect(parsePackageReferences(plan.resultingCsproj).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
  });

  it('preserves existing PackageReferences and unrelated XML, joining the existing ItemGroup', () => {
    const plan = planExtensionInstall(descriptor(), SampleCsproj);
    const refs = parsePackageReferences(plan.resultingCsproj);
    expect(refs.get('com.ivanmurzak.reflectornet')).toBe('5.3.1');
    expect(refs.get('com.ivanmurzak.mcpplugin')).toBe('6.7.0');
    expect(refs.get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
    // Unrelated XML survives.
    expect(plan.resultingCsproj).toContain('<TargetFramework>net8.0</TargetFramework>');
    expect(plan.resultingCsproj).toContain('Compile Remove="Godot-MCP.Tests/**/*.cs"');
    // Joined the existing package ItemGroup (still exactly 2 ItemGroups).
    expect((plan.resultingCsproj.match(/<ItemGroup\b/g) ?? []).length).toBe(2);
  });

  it('indents the appended reference like its siblings and preserves the </ItemGroup> indent', () => {
    const plan = planExtensionInstall(descriptor(), SampleCsproj);
    // Regression guard for the appendReference split point: the new <PackageReference>
    // sits at the same 4-space indent as the existing refs, and the closing </ItemGroup>
    // keeps its own 2-space indent (slicing at `</ItemGroup>` itself used to absorb the
    // closing tag's indent into the appended element and strip it from the closing tag).
    // Normalize EOL so the assertion is checkout-agnostic (autocrlf).
    expect(plan.resultingCsproj.replace(/\r\n/g, '\n')).toContain(
      '    <PackageReference Include="com.IvanMurzak.McpPlugin" Version="6.7.0" />\n' +
        '    <PackageReference Include="com.IvanMurzak.Godot.MCP.ProBuilder" Version="1.2.0" />\n' +
        '  </ItemGroup>',
    );
  });

  it('writes Version="*" when the descriptor has no version (NU1015 float — #242)', () => {
    // Regression for #242: an unpinned (null-version) descriptor must NOT emit a versionless
    // <PackageReference> (which fails NuGet restore with NU1015) — it floats to "*".
    const plan = planExtensionInstall(descriptor('com.x', null), SampleCsproj);
    expect(plan.action).toBe('add');
    expect(plan.toVersion).toBe('*');
    const refs = parsePackageReferences(plan.resultingCsproj);
    expect(refs.get('com.x')).toBe('*');
    expect(plan.resultingCsproj).toContain('<PackageReference Include="com.x" Version="*" />');
  });

  it('creates an ItemGroup when the project has no PackageReferences', () => {
    const bare = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>`;
    const plan = planExtensionInstall(descriptor(), bare);
    expect(plan.action).toBe('add');
    expect(parsePackageReferences(plan.resultingCsproj).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
    expect(plan.resultingCsproj).toContain('</Project>');
  });
});

describe('planExtensionInstall — UPDATE', () => {
  it('bumps the version when installed is lower', () => {
    const installed = planExtensionInstall(descriptor(undefined, '1.0.0'), SampleCsproj).resultingCsproj;
    const plan = planExtensionInstall(descriptor(undefined, '1.2.0'), installed);
    expect(plan.action).toBe('update');
    expect(plan.fromVersion).toBe('1.0.0');
    expect(plan.toVersion).toBe('1.2.0');
    const refs = parsePackageReferences(plan.resultingCsproj);
    expect(refs.get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
    expect(refs.get('com.ivanmurzak.reflectornet')).toBe('5.3.1');
    expect(refs.get('com.ivanmurzak.mcpplugin')).toBe('6.7.0');
  });

  it('updates when installed has no version but the descriptor does', () => {
    const unversioned = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <PackageReference Include="com.IvanMurzak.Godot.MCP.ProBuilder" />
  </ItemGroup>
</Project>`;
    const plan = planExtensionInstall(descriptor(undefined, '1.2.0'), unversioned);
    expect(plan.action).toBe('update');
    expect(parsePackageReferences(plan.resultingCsproj).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
  });

  it('self-heals a versionless reference to Version="*" when the descriptor is unpinned (#242)', () => {
    const versionless = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <PackageReference Include="com.IvanMurzak.Godot.MCP.ProBuilder" />
  </ItemGroup>
</Project>`;
    const plan = planExtensionInstall(descriptor(undefined, null), versionless);
    expect(plan.action).toBe('update');
    expect(plan.fromVersion).toBe('');
    expect(plan.toVersion).toBe('*');
    expect(parsePackageReferences(plan.resultingCsproj).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('*');
  });

  it('updates the child <Version> element form in place (no new attribute)', () => {
    const childForm = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <ItemGroup>
    <PackageReference Include="com.IvanMurzak.Godot.MCP.ProBuilder">
      <Version>1.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>`;
    const plan = planExtensionInstall(descriptor(undefined, '1.2.0'), childForm);
    expect(plan.action).toBe('update');
    expect(parsePackageReferences(plan.resultingCsproj).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.2.0');
    expect(plan.resultingCsproj).not.toContain('Version="1.2.0"');
    expect(plan.resultingCsproj).toContain('<Version>1.2.0</Version>');
  });
});

describe('planExtensionInstall — NO-OP', () => {
  it('no-ops when versions are equal (byte-identical result)', () => {
    const installed = planExtensionInstall(descriptor(undefined, '1.2.0'), SampleCsproj).resultingCsproj;
    const plan = planExtensionInstall(descriptor(undefined, '1.2.0'), installed);
    expect(plan.action).toBe('noop');
    expect(plan.resultingCsproj).toBe(installed);
  });

  it('no-ops when installed is newer', () => {
    const installed = planExtensionInstall(descriptor(undefined, '2.0.0'), SampleCsproj).resultingCsproj;
    const plan = planExtensionInstall(descriptor(undefined, '1.2.0'), installed);
    expect(plan.action).toBe('noop');
  });

  it('no-ops when the descriptor has no version and a concrete pin exists (never downgrades to "*" — #242)', () => {
    const installed = planExtensionInstall(descriptor(undefined, '1.0.0'), SampleCsproj).resultingCsproj;
    const plan = planExtensionInstall(descriptor(undefined, null), installed);
    expect(plan.action).toBe('noop');
    // An unpinned descriptor must NEVER downgrade a concrete consumer pin to "*".
    expect(parsePackageReferences(plan.resultingCsproj).get('com.ivanmurzak.godot.mcp.probuilder')).toBe('1.0.0');
  });

  it('throws CsprojParseError on an unparseable csproj', () => {
    expect(() => planExtensionInstall(descriptor(), '<Project><not closed')).toThrow(CsprojParseError);
  });
});
