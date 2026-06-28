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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Extensions;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the shared-catalog parser <see cref="GodotExtensionCatalog"/>: the JSON → <see cref="GodotExtensionDescriptor"/>
    /// transform (pure, tolerant of malformed/partial input) AND the embedded-resource round-trip
    /// (<see cref="GodotExtensionCatalog.LoadEmbedded"/> reads the SAME <c>extensions.catalog.json</c> that ships in the
    /// addon assembly — embedded into this test assembly under the same LogicalName by Godot-MCP.Tests.csproj). The
    /// catalog is the single source of truth the dock + CLI both consume; this is the dock half.
    /// </summary>
    public class GodotExtensionCatalogTests
    {
        const string PopulatedCatalog =
@"{
  ""schemaVersion"": 1,
  ""extensions"": [
    {
      ""name"": ""ProBuilder Tools"",
      ""description"": ""Mesh-editing MCP tools for Godot."",
      ""packageId"": ""com.IvanMurzak.Godot.MCP.ProBuilder"",
      ""version"": ""1.2.0"",
      ""gitUrl"": ""https://github.com/IvanMurzak/Godot-MCP"",
      ""tools"": [
        { ""name"": ""probuilder-extrude"", ""description"": ""Extrude faces."" }
      ],
      ""addonRequired"": {
        ""name"": ""PhantomCamera"",
        ""assetLibId"": ""1822"",
        ""repo"": ""ramokz/phantom-camera"",
        ""license"": ""MIT""
      }
    },
    {
      ""name"": ""Floating Ext"",
      ""description"": ""No version pin."",
      ""packageId"": ""com.x.Floating""
    }
  ]
}";

        [Fact]
        public void Parse_PopulatedCatalog_MapsAllFields()
        {
            var list = GodotExtensionCatalog.Parse(PopulatedCatalog);
            Assert.Equal(2, list.Count);

            var pb = list[0];
            Assert.Equal("ProBuilder Tools", pb.Name);
            Assert.Equal("Mesh-editing MCP tools for Godot.", pb.Description);
            Assert.Equal("com.IvanMurzak.Godot.MCP.ProBuilder", pb.PackageId);
            Assert.Equal("1.2.0", pb.Version);
            Assert.True(pb.HasVersion);
            Assert.Equal("https://github.com/IvanMurzak/Godot-MCP", pb.GitUrl);
            Assert.Single(pb.Tools);
            Assert.Equal("probuilder-extrude", pb.Tools[0].Name);
            Assert.Equal("Extrude faces.", pb.Tools[0].Description);
        }

        [Fact]
        public void Parse_ClassBEntry_MapsAddonRequiredBlock()
        {
            // CLASS-B: the optional addonRequired block round-trips its four fields and flags the descriptor.
            var pb = GodotExtensionCatalog.Parse(PopulatedCatalog)[0];
            Assert.True(pb.RequiresAddon);
            Assert.NotNull(pb.AddonRequired);
            Assert.Equal("PhantomCamera", pb.AddonRequired!.Name);
            Assert.Equal("1822", pb.AddonRequired.AssetLibId);
            Assert.Equal("ramokz/phantom-camera", pb.AddonRequired.Repo);
            Assert.Equal("MIT", pb.AddonRequired.License);
        }

        [Fact]
        public void Parse_OmittedVersion_YieldsNullVersion()
        {
            var list = GodotExtensionCatalog.Parse(PopulatedCatalog);
            var floating = list[1];
            Assert.Equal("com.x.Floating", floating.PackageId);
            Assert.Null(floating.Version);
            Assert.False(floating.HasVersion);
            Assert.Empty(floating.Tools);
        }

        [Fact]
        public void Parse_ClassAEntry_HasNullAddonRequired()
        {
            // CLASS-A (no addonRequired block) → AddonRequired is null and RequiresAddon is false.
            var floating = GodotExtensionCatalog.Parse(PopulatedCatalog)[1];
            Assert.Null(floating.AddonRequired);
            Assert.False(floating.RequiresAddon);
        }

        [Fact]
        public void Parse_DropsEntries_MissingNameOrPackageId()
        {
            const string partial =
@"{ ""extensions"": [
    { ""name"": """", ""packageId"": ""com.a"" },
    { ""name"": ""B"", ""packageId"": """" },
    { ""description"": ""no name or id"" },
    { ""name"": ""Good"", ""packageId"": ""com.good"" }
] }";
            var list = GodotExtensionCatalog.Parse(partial);
            Assert.Single(list);
            Assert.Equal("com.good", list[0].PackageId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{ not valid json")]
        [InlineData(@"{ ""extensions"": [] }")]
        [InlineData("{}")]
        public void Parse_ReturnsEmpty_OnMissingEmptyOrInvalid(string? json)
        {
            Assert.Empty(GodotExtensionCatalog.Parse(json));
        }

        [Fact]
        public void LoadEmbedded_RoundTripsTheShippedCatalog()
        {
            // This proves the embedded resource resolves + parses (an unresolved resource would return empty, so we
            // additionally assert the resource is actually present — independent of how many entries it ships).
            var assembly = typeof(GodotExtensionCatalog).Assembly;
            Assert.Contains(GodotExtensionCatalog.EmbeddedResourceName, assembly.GetManifestResourceNames());

            var list = GodotExtensionCatalog.LoadEmbedded();
            Assert.NotNull(list);
            // Registry binds to exactly this — they must agree.
            Assert.Equal(GodotExtensionRegistry.All.Count, list.Count);
        }
    }
}
