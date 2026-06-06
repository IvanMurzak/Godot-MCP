/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;
using System.Xml.Linq;
using com.IvanMurzak.Godot.MCP.Extensions;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed Extensions install infrastructure: the descriptor + (empty) registry, the
    /// <see cref="ExtensionInstallPlanner"/> XML transform (add-when-absent, bump-when-lower, no-op-when-equal,
    /// preserve other PackageReferences + XML), the <see cref="InstalledStateDetector"/> (parse / missing /
    /// version-compare), and the <see cref="ExtensionInstaller"/> flow over an in-memory <see cref="IConsumerProjectFile"/>
    /// fake. The dock UI (<c>ExtensionsPanel.cs</c> / <c>ExtensionRow.cs</c> / <c>ConsumerProjectFile.cs</c>,
    /// <c>#if TOOLS</c>) is verified via the headless Godot smoke (test.md Suite 3) — NOT here. All logic is pure
    /// string/XML so the assertions are identical on Windows dev + Linux CI.
    /// </summary>
    public class ExtensionInstallTests
    {
        // A representative SDK-style consumer .csproj with two existing PackageReferences in one ItemGroup, plus an
        // unrelated ItemGroup — the things the planner must preserve.
        const string SampleCsproj =
@"<Project Sdk=""Godot.NET.Sdk/4.3.0"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""com.IvanMurzak.ReflectorNet"" Version=""5.3.1"" />
    <PackageReference Include=""com.IvanMurzak.McpPlugin"" Version=""6.7.0"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Remove=""Godot-MCP.Tests/**/*.cs"" />
  </ItemGroup>
</Project>";

        static GodotExtensionDescriptor Descriptor(string id = "com.IvanMurzak.Godot.MCP.ProBuilder", string? Version = "1.2.0")
            => new("ProBuilder Tools", "Mesh-editing MCP tools for Godot.", id, Version);

        // --- Descriptor + registry -----------------------------------------------------------------------------

        [Fact]
        public void Registry_ShipsEmpty()
        {
            Assert.True(GodotExtensionRegistry.IsEmpty);
            Assert.Empty(GodotExtensionRegistry.All);
            Assert.Null(GodotExtensionRegistry.GetByPackageId("anything"));
        }

        [Fact]
        public void Descriptor_DefaultsToEmptyTools_AndReportsVersionPresence()
        {
            var withVersion = Descriptor();
            Assert.True(withVersion.HasVersion);
            Assert.Empty(withVersion.Tools);

            var noVersion = new GodotExtensionDescriptor("X", "desc", "com.x", Version: null);
            Assert.False(noVersion.HasVersion);
        }

        // --- Planner: ADD --------------------------------------------------------------------------------------

        [Fact]
        public void Plan_AddsReference_WhenAbsent()
        {
            var plan = ExtensionInstallPlanner.Plan(Descriptor(), SampleCsproj);

            Assert.Equal(ExtensionInstallAction.Add, plan.Action);
            Assert.True(plan.RequiresWrite);
            Assert.Null(plan.FromVersion);
            Assert.Equal("1.2.0", plan.ToVersion);

            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);
            Assert.Equal("1.2.0", refs["com.IvanMurzak.Godot.MCP.ProBuilder"]);
        }

        [Fact]
        public void Plan_Add_PreservesExistingPackageReferences_AndUnrelatedXml()
        {
            var plan = ExtensionInstallPlanner.Plan(Descriptor(), SampleCsproj);
            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);

            // Both originals survive, unchanged.
            Assert.Equal("5.3.1", refs["com.IvanMurzak.ReflectorNet"]);
            Assert.Equal("6.7.0", refs["com.IvanMurzak.McpPlugin"]);
            // New one added.
            Assert.Equal("1.2.0", refs["com.IvanMurzak.Godot.MCP.ProBuilder"]);

            // The unrelated <Compile Remove> ItemGroup + PropertyGroup are still present.
            var doc = XDocument.Parse(plan.ResultingCsproj);
            Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "Compile");
            Assert.Contains(doc.Descendants(), e => e.Name.LocalName == "TargetFramework");

            // The new reference joined the EXISTING package ItemGroup (not a brand-new group): exactly 2 ItemGroups
            // remain (the package group now has 3 PackageReferences, the Compile group untouched).
            var itemGroups = new List<XElement>();
            foreach (var e in doc.Descendants())
                if (e.Name.LocalName == "ItemGroup")
                    itemGroups.Add(e);
            Assert.Equal(2, itemGroups.Count);
        }

        [Fact]
        public void Plan_Add_WithNoVersion_OmitsVersionAttribute()
        {
            var descriptor = new GodotExtensionDescriptor("X", "desc", "com.x", Version: null);
            var plan = ExtensionInstallPlanner.Plan(descriptor, SampleCsproj);

            Assert.Equal(ExtensionInstallAction.Add, plan.Action);
            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);
            Assert.True(refs.ContainsKey("com.x"));
            Assert.Equal(string.Empty, refs["com.x"]);
        }

        [Fact]
        public void Plan_Add_CreatesItemGroup_WhenProjectHasNoPackageReferences()
        {
            const string bare =
@"<Project Sdk=""Godot.NET.Sdk/4.3.0"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
            var plan = ExtensionInstallPlanner.Plan(Descriptor(), bare);

            Assert.Equal(ExtensionInstallAction.Add, plan.Action);
            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);
            Assert.Equal("1.2.0", refs["com.IvanMurzak.Godot.MCP.ProBuilder"]);
        }

        // --- Planner: UPDATE -----------------------------------------------------------------------------------

        [Fact]
        public void Plan_BumpsVersion_WhenInstalledIsLower()
        {
            // Install at 1.0.0, descriptor pins 1.2.0.
            var first = ExtensionInstallPlanner.Plan(
                Descriptor(Version: "1.0.0"), SampleCsproj).ResultingCsproj;

            var plan = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), first);

            Assert.Equal(ExtensionInstallAction.Update, plan.Action);
            Assert.Equal("1.0.0", plan.FromVersion);
            Assert.Equal("1.2.0", plan.ToVersion);

            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);
            Assert.Equal("1.2.0", refs["com.IvanMurzak.Godot.MCP.ProBuilder"]);
            // Siblings still intact after the bump.
            Assert.Equal("5.3.1", refs["com.IvanMurzak.ReflectorNet"]);
            Assert.Equal("6.7.0", refs["com.IvanMurzak.McpPlugin"]);
        }

        [Fact]
        public void Plan_Update_WhenInstalledHasNoVersion_ButDescriptorDoes()
        {
            const string unversioned =
@"<Project Sdk=""Godot.NET.Sdk/4.3.0"">
  <ItemGroup>
    <PackageReference Include=""com.IvanMurzak.Godot.MCP.ProBuilder"" />
  </ItemGroup>
</Project>";
            var plan = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), unversioned);

            Assert.Equal(ExtensionInstallAction.Update, plan.Action);
            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);
            Assert.Equal("1.2.0", refs["com.IvanMurzak.Godot.MCP.ProBuilder"]);
        }

        [Fact]
        public void Plan_UpdatesChildVersionElement_Form()
        {
            const string childElementForm =
@"<Project Sdk=""Godot.NET.Sdk/4.3.0"">
  <ItemGroup>
    <PackageReference Include=""com.IvanMurzak.Godot.MCP.ProBuilder"">
      <Version>1.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>";
            var plan = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), childElementForm);

            Assert.Equal(ExtensionInstallAction.Update, plan.Action);
            var refs = InstalledStateDetector.ParsePackageReferences(plan.ResultingCsproj);
            Assert.Equal("1.2.0", refs["com.IvanMurzak.Godot.MCP.ProBuilder"]);
            // Still the child-element form (no Version attribute introduced).
            Assert.DoesNotContain("Version=\"1.2.0\"", plan.ResultingCsproj);
            Assert.Contains("<Version>1.2.0</Version>", plan.ResultingCsproj);
        }

        // --- Planner: NO-OP ------------------------------------------------------------------------------------

        [Fact]
        public void Plan_NoOp_WhenVersionsEqual()
        {
            var installed = ExtensionInstallPlanner.Plan(
                Descriptor(Version: "1.2.0"), SampleCsproj).ResultingCsproj;

            var plan = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), installed);

            Assert.Equal(ExtensionInstallAction.NoOp, plan.Action);
            Assert.False(plan.RequiresWrite);
            // Resulting text is byte-identical to the input on a no-op (the IO can skip the write).
            Assert.Equal(installed, plan.ResultingCsproj);
        }

        [Fact]
        public void Plan_NoOp_WhenInstalledIsNewer()
        {
            var installed = ExtensionInstallPlanner.Plan(
                Descriptor(Version: "2.0.0"), SampleCsproj).ResultingCsproj;

            var plan = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), installed);
            Assert.Equal(ExtensionInstallAction.NoOp, plan.Action);
        }

        [Fact]
        public void Plan_NoOp_WhenDescriptorHasNoVersion_AndReferenceExists()
        {
            var installed = ExtensionInstallPlanner.Plan(
                Descriptor(Version: "1.0.0"), SampleCsproj).ResultingCsproj;

            var plan = ExtensionInstallPlanner.Plan(
                new GodotExtensionDescriptor("X", "d", "com.IvanMurzak.Godot.MCP.ProBuilder", Version: null), installed);
            Assert.Equal(ExtensionInstallAction.NoOp, plan.Action);
        }

        [Fact]
        public void Plan_Throws_OnUnparseableCsproj()
        {
            Assert.Throws<System.ArgumentException>(
                () => ExtensionInstallPlanner.Plan(Descriptor(), "<Project><not closed"));
        }

        // --- Detector: parse -----------------------------------------------------------------------------------

        [Fact]
        public void Detector_ParsesAttributeAndChildVersionForms()
        {
            const string mixed =
@"<Project>
  <ItemGroup>
    <PackageReference Include=""A"" Version=""1.0.0"" />
    <PackageReference Include=""B""><Version>2.3.4</Version></PackageReference>
    <PackageReference Include=""C"" />
  </ItemGroup>
</Project>";
            var refs = InstalledStateDetector.ParsePackageReferences(mixed);
            Assert.Equal("1.0.0", refs["A"]);
            Assert.Equal("2.3.4", refs["B"]);
            Assert.Equal(string.Empty, refs["C"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("<Project><not valid xml")]
        public void Detector_ReturnsEmpty_OnMissingOrInvalidCsproj(string? text)
        {
            Assert.Empty(InstalledStateDetector.ParsePackageReferences(text));
        }

        [Fact]
        public void Detector_PackageIdMatch_IsCaseInsensitive()
        {
            var refs = InstalledStateDetector.ParsePackageReferences(SampleCsproj);
            // NuGet ids are case-insensitive — the dictionary uses an ordinal-ignore-case comparer.
            Assert.Equal("6.7.0", refs["COM.ivanmurzak.MCPPLUGIN"]);
        }

        // --- Detector: per-descriptor state --------------------------------------------------------------------

        [Fact]
        public void Detector_State_NotInstalled_WhenAbsent()
        {
            Assert.Equal(
                ExtensionInstallState.NotInstalled,
                InstalledStateDetector.StateFor(Descriptor(), SampleCsproj));
        }

        [Fact]
        public void Detector_State_Installed_WhenEqualOrNewer()
        {
            var installed = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), SampleCsproj).ResultingCsproj;
            Assert.Equal(
                ExtensionInstallState.Installed,
                InstalledStateDetector.StateFor(Descriptor(Version: "1.2.0"), installed));
            Assert.Equal(
                ExtensionInstallState.Installed,
                InstalledStateDetector.StateFor(Descriptor(Version: "1.0.0"), installed));
        }

        [Fact]
        public void Detector_State_UpdateAvailable_WhenDescriptorIsNewer()
        {
            var installed = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.0.0"), SampleCsproj).ResultingCsproj;
            Assert.Equal(
                ExtensionInstallState.UpdateAvailable,
                InstalledStateDetector.StateFor(Descriptor(Version: "1.2.0"), installed));
        }

        // --- Version compare -----------------------------------------------------------------------------------

        [Theory]
        [InlineData("1.2.0", "1.2.0", 0)]
        [InlineData("1.10.0", "1.2.0", 1)]   // numeric, not ordinal — 10 > 2
        [InlineData("1.2.0", "1.10.0", -1)]
        [InlineData("2.0.0", "1.9.9", 1)]
        [InlineData("1.0", "1.0.0", 0)]      // missing trailing component == 0
        [InlineData("1.0.0-rc1", "1.0.0", 0)] // pre-release suffix tolerated (leading int only)
        public void CompareVersions_IsNumericAndTolerant(string a, string b, int expectedSign)
        {
            Assert.Equal(expectedSign, System.Math.Sign(InstalledStateDetector.CompareVersions(a, b)));
        }

        // --- Installer flow over an in-memory IConsumerProjectFile fake ----------------------------------------

        sealed class FakeProjectFile : IConsumerProjectFile
        {
            string? _text;
            public FakeProjectFile(string? text) => _text = text;
            public bool Exists => _text != null;
            public string? Path => Exists ? "/fake/project.csproj" : null;
            public int Writes { get; private set; }
            public string? Read() => _text;
            public bool Write(string csprojText) { _text = csprojText; Writes++; return true; }
        }

        [Fact]
        public void Installer_Add_WritesAndRequestsRebuild()
        {
            var file = new FakeProjectFile(SampleCsproj);
            var result = ExtensionInstaller.Install(Descriptor(), file);

            Assert.Equal(ExtensionInstallOutcome.Added, result.Outcome);
            Assert.True(result.RebuildRequired);
            Assert.Equal(1, file.Writes);
            Assert.Contains("Rebuild solutions", result.Message);

            // The fake now reports the extension as installed.
            Assert.Equal(ExtensionInstallState.Installed, InstalledStateDetector.StateFor(Descriptor(), file.Read()));
        }

        [Fact]
        public void Installer_NoOp_DoesNotWrite()
        {
            var installed = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.2.0"), SampleCsproj).ResultingCsproj;
            var file = new FakeProjectFile(installed);

            var result = ExtensionInstaller.Install(Descriptor(Version: "1.2.0"), file);

            Assert.Equal(ExtensionInstallOutcome.AlreadyUpToDate, result.Outcome);
            Assert.False(result.RebuildRequired);
            Assert.Equal(0, file.Writes);
        }

        [Fact]
        public void Installer_Update_BumpsAndRequestsRebuild()
        {
            var installed = ExtensionInstallPlanner.Plan(Descriptor(Version: "1.0.0"), SampleCsproj).ResultingCsproj;
            var file = new FakeProjectFile(installed);

            var result = ExtensionInstaller.Install(Descriptor(Version: "1.2.0"), file);

            Assert.Equal(ExtensionInstallOutcome.Updated, result.Outcome);
            Assert.True(result.RebuildRequired);
            Assert.Equal(1, file.Writes);
            Assert.Equal(ExtensionInstallState.Installed, InstalledStateDetector.StateFor(Descriptor(Version: "1.2.0"), file.Read()));
        }

        [Fact]
        public void Installer_NoProjectFile_Fails_Gracefully()
        {
            var file = new FakeProjectFile(null);
            var result = ExtensionInstaller.Install(Descriptor(), file);

            Assert.Equal(ExtensionInstallOutcome.NoProjectFile, result.Outcome);
            Assert.False(result.RebuildRequired);
            Assert.Equal(0, file.Writes);
        }
    }
}
