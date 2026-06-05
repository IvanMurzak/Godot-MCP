/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed feature list filter chain (<see cref="FeatureFilter"/>): the status×text matrix, the
    /// per-type text fields (resource URI match), case-insensitivity, empty-text = status-only, the empty result,
    /// the "Filtered: X, Total: Y" stat, and the JSON-schema argument parser. The editor window Control wiring
    /// (FeatureListWindow.cs, <c>#if TOOLS</c>) and the live-manager → <see cref="FeatureRowItem"/> mapping
    /// (FeatureManagerAdapters.cs) are verified via the headless Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class FeatureFilterTests
    {
        static FeatureRowItem Tool(string name, bool enabled, string? title = null, string? desc = null) =>
            new() { Name = name, Title = title, Description = desc, Enabled = enabled };

        static FeatureRowItem Resource(string name, bool enabled, string? uri = null) =>
            new() { Name = name, Enabled = enabled, Uri = uri };

        static List<FeatureRowItem> Sample() => new()
        {
            Tool("alpha-tool", enabled: true, title: "Alpha", desc: "first tool"),
            Tool("beta-tool", enabled: false, title: "Beta", desc: "second tool"),
            Tool("gamma-tool", enabled: true, title: null, desc: "third tool"),
        };

        // --- Status filter ------------------------------------------------------------------------------------

        [Fact]
        public void Apply_All_status_returns_every_item()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.All, null, GodotMcpFeatureKind.Tools);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Apply_Enabled_status_keeps_only_enabled()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.Enabled, null, GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "alpha-tool", "gamma-tool" }, result.Select(i => i.Name));
        }

        [Fact]
        public void Apply_Disabled_status_keeps_only_disabled()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.Disabled, null, GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "beta-tool" }, result.Select(i => i.Name));
        }

        // --- Text filter (Name / Title / Description) ---------------------------------------------------------

        [Fact]
        public void Apply_text_matches_name()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.All, "beta", GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "beta-tool" }, result.Select(i => i.Name));
        }

        [Fact]
        public void Apply_text_matches_title()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.All, "Alpha", GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "alpha-tool" }, result.Select(i => i.Name));
        }

        [Fact]
        public void Apply_text_matches_description()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.All, "third", GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "gamma-tool" }, result.Select(i => i.Name));
        }

        [Fact]
        public void Apply_text_is_case_insensitive()
        {
            var lower = FeatureFilter.Apply(Sample(), FeatureStatusFilter.All, "ALPHA", GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "alpha-tool" }, lower.Select(i => i.Name));
        }

        [Fact]
        public void Apply_empty_text_applies_status_only()
        {
            var blank = FeatureFilter.Apply(Sample(), FeatureStatusFilter.Enabled, "   ", GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "alpha-tool", "gamma-tool" }, blank.Select(i => i.Name));
        }

        [Fact]
        public void Apply_no_match_returns_empty()
        {
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.All, "zzz-nope", GodotMcpFeatureKind.Tools);
            Assert.Empty(result);
        }

        // --- Status THEN text combined ------------------------------------------------------------------------

        [Fact]
        public void Apply_status_and_text_combine_status_first()
        {
            // "tool" matches all three by name; Enabled status narrows to the two enabled ones.
            var result = FeatureFilter.Apply(Sample(), FeatureStatusFilter.Enabled, "tool", GodotMcpFeatureKind.Tools);
            Assert.Equal(new[] { "alpha-tool", "gamma-tool" }, result.Select(i => i.Name));
        }

        // --- Resource URI text match (per-type field) ---------------------------------------------------------

        [Fact]
        public void Apply_resource_text_matches_uri()
        {
            var items = new List<FeatureRowItem>
            {
                Resource("res-a", enabled: true, uri: "godot://scene/main"),
                Resource("res-b", enabled: true, uri: "godot://project/settings"),
            };
            var result = FeatureFilter.Apply(items, FeatureStatusFilter.All, "settings", GodotMcpFeatureKind.Resources);
            Assert.Equal(new[] { "res-b" }, result.Select(i => i.Name));
        }

        [Fact]
        public void Apply_non_resource_kind_does_not_match_uri()
        {
            // A tool-kind item with a Uri set (unusual) must NOT be matched by URI text — only resources match URI.
            var items = new List<FeatureRowItem>
            {
                new() { Name = "tool-x", Enabled = true, Uri = "godot://only-here" },
            };
            var asTool = FeatureFilter.Apply(items, FeatureStatusFilter.All, "only-here", GodotMcpFeatureKind.Tools);
            Assert.Empty(asTool);

            var asResource = FeatureFilter.Apply(items, FeatureStatusFilter.All, "only-here", GodotMcpFeatureKind.Resources);
            Assert.Single(asResource);
        }

        // --- Stats formatting ---------------------------------------------------------------------------------

        [Fact]
        public void FormatStats_renders_filtered_and_total()
        {
            Assert.Equal("Filtered: 2, Total: 7", FeatureFilter.FormatStats(2, 7));
            Assert.Equal("Filtered: 0, Total: 0", FeatureFilter.FormatStats(0, 0));
        }

        // --- Schema argument parsing --------------------------------------------------------------------------

        [Fact]
        public void ParseSchemaArguments_null_schema_is_empty()
        {
            Assert.Empty(FeatureFilter.ParseSchemaArguments(null));
        }

        [Fact]
        public void ParseSchemaArguments_no_properties_is_empty()
        {
            var schema = JsonNode.Parse("""{ "type": "object" }""");
            Assert.Empty(FeatureFilter.ParseSchemaArguments(schema));
        }

        [Fact]
        public void ParseSchemaArguments_extracts_names_and_descriptions()
        {
            var schema = JsonNode.Parse("""
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "the resource path" },
                "force": { "type": "boolean" }
              }
            }
            """);

            var args = FeatureFilter.ParseSchemaArguments(schema);
            Assert.Equal(2, args.Count);

            var path = args.Single(a => a.Name == "path");
            Assert.Equal("the resource path", path.Description);

            var force = args.Single(a => a.Name == "force");
            Assert.Null(force.Description);
        }

        // --- Row view-model display title ---------------------------------------------------------------------

        [Fact]
        public void DisplayTitle_prefers_title_else_falls_back_to_name()
        {
            Assert.Equal("Alpha", Tool("alpha-tool", true, title: "Alpha").DisplayTitle);
            Assert.Equal("gamma-tool", Tool("gamma-tool", true, title: null).DisplayTitle);
            Assert.Equal("blank-tool", Tool("blank-tool", true, title: "").DisplayTitle);
        }
    }
}
