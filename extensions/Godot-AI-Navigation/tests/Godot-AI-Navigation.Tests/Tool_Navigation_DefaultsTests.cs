/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Navigation;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Navigation.Tests
{
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>navigation-defaults</c> tool — constructs the tool family and invokes
    /// the method directly (no Godot binary, no MCP server). The editor-only tools (<c>navigation-region-create</c>,
    /// <c>-region-set-mesh</c>, <c>-agent-create</c>, <c>-agent-configure</c>, <c>-link-create</c>, <c>-get</c>)
    /// touch a live editor and are verified by the headless-Godot E2E leg instead; their tool-id constants are
    /// pinned here so the ids the dock / godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_Navigation_DefaultsTests
    {
        [Fact]
        public void Defaults_DefaultDimension_Is3D()
        {
            var tool = new Tool_Navigation();
            var info = tool.Defaults();
            Assert.Equal("3D", info.Dimension);
            Assert.Equal("Agent", info.Kind);
            Assert.Equal(NavigationDefaults.Default3DRadius, info.Radius);
        }

        [Theory]
        [InlineData("2D", "2D")]
        [InlineData("3d", "3D")]
        [InlineData("two", "2D")]
        public void Defaults_ParsesDimension(string input, string expectedLabel)
        {
            var tool = new Tool_Navigation();
            Assert.Equal(expectedLabel, tool.Defaults(input).Dimension);
        }

        [Fact]
        public void Defaults_InvalidDimension_Throws()
        {
            var tool = new Tool_Navigation();
            Assert.Throws<ArgumentException>(() => tool.Defaults("octahedral"));
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("navigation-defaults", Tool_Navigation.DefaultsToolId);
            Assert.Equal("navigation-region-create", Tool_Navigation.RegionCreateToolId);
            Assert.Equal("navigation-region-set-mesh", Tool_Navigation.RegionSetMeshToolId);
            Assert.Equal("navigation-agent-create", Tool_Navigation.AgentCreateToolId);
            Assert.Equal("navigation-agent-configure", Tool_Navigation.AgentConfigureToolId);
            Assert.Equal("navigation-link-create", Tool_Navigation.LinkCreateToolId);
            Assert.Equal("navigation-get", Tool_Navigation.GetToolId);
        }
    }
}
