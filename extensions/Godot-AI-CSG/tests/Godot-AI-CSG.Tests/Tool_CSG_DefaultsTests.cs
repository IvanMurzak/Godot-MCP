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
using com.IvanMurzak.Godot.MCP.CSG;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.CSG.Tests
{
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>csg-defaults</c> tool — constructs the tool family and invokes the
    /// method directly (no Godot binary, no MCP server). The editor-only tools (<c>csg-box-create</c>,
    /// <c>-sphere-create</c>, <c>-cylinder-create</c>, <c>-combiner-create</c>, <c>-set-operation</c>,
    /// <c>-get</c>) touch a live editor and are verified by the headless-Godot E2E leg instead; their tool-id
    /// constants are pinned here so the ids the dock / godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_CSG_DefaultsTests
    {
        [Fact]
        public void Defaults_DefaultKind_IsBox()
        {
            var tool = new Tool_CSG();
            var info = tool.Defaults();
            Assert.Equal("Box", info.Kind);
            Assert.Equal(CsgDefaults.DefaultBoxSize, info.SizeX);
        }

        [Theory]
        [InlineData("box", "Box")]
        [InlineData("sphere", "Sphere")]
        [InlineData("CYLINDER", "Cylinder")]
        [InlineData("combiner", "Combiner")]
        public void Defaults_ParsesKind(string input, string expectedKind)
        {
            var tool = new Tool_CSG();
            Assert.Equal(expectedKind, tool.Defaults(input).Kind);
        }

        [Fact]
        public void Defaults_InvalidKind_Throws()
        {
            var tool = new Tool_CSG();
            Assert.Throws<ArgumentException>(() => tool.Defaults("torus"));
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("csg-defaults", Tool_CSG.DefaultsToolId);
            Assert.Equal("csg-box-create", Tool_CSG.BoxCreateToolId);
            Assert.Equal("csg-sphere-create", Tool_CSG.SphereCreateToolId);
            Assert.Equal("csg-cylinder-create", Tool_CSG.CylinderCreateToolId);
            Assert.Equal("csg-combiner-create", Tool_CSG.CombinerCreateToolId);
            Assert.Equal("csg-set-operation", Tool_CSG.SetOperationToolId);
            Assert.Equal("csg-get", Tool_CSG.GetToolId);
        }
    }
}
