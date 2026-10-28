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
using System.Linq;
using Rql;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// What an MCP-only agent can discover: the installed extensions' guides (rql-schema overview and rql-schema "&lt;topic&gt;")
    /// and every tool including hidden ones (the tools source).
    /// </summary>
    public class RqlDiscoveryTests
    {
        private static readonly List<KeyValuePair<string, string>> Guides = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Particles", "GPU particle emitters are nodes: GPUParticles3D and GPUParticles2D. Add one with `AddChild`.\n\n```\nroot | call AddChild(\"GPUParticles3D\", \"FX\")\n```"),
            new KeyValuePair<string, string>("Phantom Camera", "Phantom Camera (the ramokz/phantom-camera addon) adds virtual cameras. More text."),
        };

        [Fact]
        public void Overview_lists_each_topic_with_its_first_sentence()
        {
            var text = RqlExtensionGuides.Overview(Guides);
            Assert.Contains("installed extensions", text);
            Assert.Contains("  Particles — GPU particle emitters are nodes: GPUParticles3D and GPUParticles2D.\n", text);
            Assert.Contains("  Phantom Camera — Phantom Camera (the ramokz/phantom-camera addon) adds virtual cameras.\n", text);
            Assert.DoesNotContain("AddChild", text);
        }

        [Fact]
        public void Overview_is_empty_without_guides() => Assert.Equal("", RqlExtensionGuides.Overview(new List<KeyValuePair<string, string>>()));

        [Theory]
        [InlineData("Particles")]
        [InlineData("particles")]
        [InlineData("PhantomCamera")]
        [InlineData("phantom camera")]
        public void Find_matches_a_topic_ignoring_case_and_spaces(string name) => Assert.NotNull(RqlExtensionGuides.Find(Guides, name));

        [Fact]
        public void Find_returns_the_whole_guide_or_null()
        {
            Assert.Contains("root | call AddChild(\"GPUParticles3D\", \"FX\")", RqlExtensionGuides.Find(Guides, "Particles"));
            Assert.Null(RqlExtensionGuides.Find(Guides, "Node3D"));
        }

        [Fact]
        public void Summary_is_shortened()
        {
            var summary = RqlExtensionGuides.Summary(new string('a', 400) + ". tail");
            Assert.True(summary.Length <= 180);
            Assert.EndsWith("…", summary);
        }

        [Fact]
        public void Summary_does_not_stop_at_an_abbreviation() =>
            Assert.Equal("Adds nodes, e.g. a camera, i.e. Camera3D.", RqlExtensionGuides.Summary("Adds nodes, e.g. a camera, i.e. Camera3D. More."));

        [Fact]
        public void A_tool_row_keeps_the_first_sentence_and_the_visibility()
        {
            var row = new RqlToolRow("godot-skill-generate", "Skills / Generate", "Regenerate every SKILL.md. Writes files.", visible: false);
            Assert.Equal("Regenerate every SKILL.md.", row.description);
            Assert.False(row.visible);
        }

        [Fact]
        public void The_tools_source_lists_rows_through_RQL()
        {
            var engine = new RqlEngine();
            var rows = new List<RqlToolRow>
            {
                new RqlToolRow("rql-query", "RQL / Query", "Run a query.", true),
                new RqlToolRow("hidden-one", null, "Hidden. Still callable.", false),
            };
            engine.AddSource(RqlTools.SourceName, () => rows, RqlTools.Description);
            var result = engine.Execute("tools | where visible == false | select name");
            var text = AgentFormat.Render(result, OutputFormat.Table);
            Assert.True(result.Ok, text);
            Assert.Equal("1 result\nname\nhidden-one", text.Trim().Replace("\r", ""));
        }

        [Fact]
        public void Without_a_connected_plugin_the_tools_source_is_empty()
        {
            RqlTools.Manager = null;
            Assert.Empty(RqlTools.All());
        }
    }
}
