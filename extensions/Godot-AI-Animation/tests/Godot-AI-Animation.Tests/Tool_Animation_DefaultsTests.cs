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
using com.IvanMurzak.Godot.MCP.Animation;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Animation.Tests
{
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>animation-defaults</c> tool — constructs the tool family and invokes
    /// the method directly (no Godot binary, no MCP server). The editor-only tools (<c>animation-player-create</c>,
    /// <c>-library-add</c>, <c>-create</c>, <c>-add-track</c>, <c>-insert-key</c>, <c>-get</c>) touch a live
    /// editor and are verified by the headless-Godot E2E leg instead; their tool-id constants are pinned here so
    /// the ids the dock / godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_Animation_DefaultsTests
    {
        [Fact]
        public void Defaults_DefaultLoopMode_IsNone()
        {
            var tool = new Tool_Animation();
            var info = tool.Defaults();
            Assert.Equal("none", info.LoopMode);
            Assert.Equal(AnimationDefaults.DefaultLength, info.Length);
            Assert.Empty(info.Tracks);
        }

        [Theory]
        [InlineData("linear", "linear")]
        [InlineData("PINGPONG", "pingpong")]
        [InlineData("none", "none")]
        public void Defaults_ParsesLoopMode(string input, string expectedLabel)
        {
            var tool = new Tool_Animation();
            Assert.Equal(expectedLabel, tool.Defaults(input).LoopMode);
        }

        [Fact]
        public void Defaults_InvalidLoopMode_Throws()
        {
            var tool = new Tool_Animation();
            Assert.Throws<ArgumentException>(() => tool.Defaults("boomerang"));
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("animation-defaults", Tool_Animation.DefaultsToolId);
            Assert.Equal("animation-player-create", Tool_Animation.PlayerCreateToolId);
            Assert.Equal("animation-library-add", Tool_Animation.LibraryAddToolId);
            Assert.Equal("animation-create", Tool_Animation.CreateToolId);
            Assert.Equal("animation-add-track", Tool_Animation.AddTrackToolId);
            Assert.Equal("animation-insert-key", Tool_Animation.InsertKeyToolId);
            Assert.Equal("animation-get", Tool_Animation.GetToolId);
        }
    }
}
