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
using com.IvanMurzak.Godot.MCP.Particles;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Particles.Tests
{
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>particles-defaults</c> tool — constructs the tool family and invokes
    /// the method directly (no Godot binary, no MCP server). The editor-only tools (<c>particles-create</c>,
    /// <c>-configure</c>, <c>-set-emitting</c>, <c>-get</c>) touch a live editor and are verified by the
    /// headless-Godot E2E leg instead; their tool-id constants are pinned here so the ids the dock / godot-cli
    /// / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_Particles_DefaultsTests
    {
        [Fact]
        public void Defaults_DefaultDimension_Is3D()
        {
            var tool = new Tool_Particles();
            var info = tool.Defaults();
            Assert.Equal("3D", info.Dimension);
            Assert.Equal(ParticlesDefaults.DefaultAmount, info.Amount);
        }

        [Theory]
        [InlineData("2D", "2D")]
        [InlineData("3d", "3D")]
        [InlineData("two", "2D")]
        public void Defaults_ParsesDimension(string input, string expectedLabel)
        {
            var tool = new Tool_Particles();
            Assert.Equal(expectedLabel, tool.Defaults(input).Dimension);
        }

        [Fact]
        public void Defaults_InvalidDimension_Throws()
        {
            var tool = new Tool_Particles();
            Assert.Throws<ArgumentException>(() => tool.Defaults("octahedral"));
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("particles-defaults", Tool_Particles.DefaultsToolId);
            Assert.Equal("particles-create", Tool_Particles.CreateToolId);
            Assert.Equal("particles-configure", Tool_Particles.ConfigureToolId);
            Assert.Equal("particles-set-emitting", Tool_Particles.SetEmittingToolId);
            Assert.Equal("particles-get", Tool_Particles.GetToolId);
        }
    }
}
