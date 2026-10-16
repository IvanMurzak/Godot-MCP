/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Beehave;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>beehave-defaults</c> tool — constructs the tool family and invokes the
    /// method directly (no Godot binary, no MCP server). The editor-only tools (<c>beehave-tree-create</c>,
    /// <c>-add-composite</c>, <c>-add-decorator</c>, <c>-add-leaf</c>, <c>-get</c>) touch a live editor + drive
    /// the Beehave addon, so they are verified by the headless-Godot e2e leg instead; their tool-id constants
    /// are pinned here so the ids the dock / godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_Beehave_DefaultsTests
    {
        [Fact]
        public void Defaults_ReturnsRecommendedSkeleton()
        {
            var tool = new Tool_Beehave();
            var skeleton = tool.Defaults();

            Assert.Equal("BeehaveTree", skeleton.RootClass);
            Assert.Equal(BeehaveDefaults.DefaultTickRate, skeleton.TickRate);
            Assert.Equal("selector", skeleton.CompositeKind);
            Assert.Equal("SelectorComposite", skeleton.CompositeClass);
            Assert.Equal(new[] { "ConditionLeaf", "ActionLeaf" }, skeleton.Leaves);
            Assert.False(string.IsNullOrWhiteSpace(skeleton.Note));
        }

        [Fact]
        public void Defaults_Compact_OmitsNote()
        {
            var tool = new Tool_Beehave();
            var skeleton = tool.Defaults(compact: true);
            Assert.Equal(string.Empty, skeleton.Note);
            // The structured fields are still present.
            Assert.Equal("BeehaveTree", skeleton.RootClass);
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("beehave-defaults", Tool_Beehave.DefaultsToolId);
            Assert.Equal("beehave-tree-create", Tool_Beehave.TreeCreateToolId);
            Assert.Equal("beehave-add-composite", Tool_Beehave.AddCompositeToolId);
            Assert.Equal("beehave-add-decorator", Tool_Beehave.AddDecoratorToolId);
            Assert.Equal("beehave-add-leaf", Tool_Beehave.AddLeafToolId);
            Assert.Equal("beehave-get", Tool_Beehave.GetToolId);
        }
    }
}
