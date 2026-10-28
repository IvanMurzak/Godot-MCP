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
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>An RQL $ref an agent copies into a node argument (screenshot-camera nodeRef "Camera3D#2990639514705").</summary>
    public class NodeRefParseTests
    {
        [Theory]
        [InlineData("Camera3D#2990639514705", 2990639514705UL)]
        [InlineData("GPUParticles3D#42", 42UL)]
        [InlineData("CoinRush.Player#7", 7UL)]
        public void A_ref_gives_its_instance_id(string text, ulong id)
        {
            Assert.True(CallableText.TryParseRef(text, out var i));
            Assert.Equal(id, i);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Player")]
        [InlineData("Level/Player")]
        [InlineData("#12")]
        [InlineData("Camera3D#")]
        [InlineData("Camera3D#0")]
        [InlineData("Camera3D#-5")]
        [InlineData("Level/Cam#12")]
        [InlineData("3D#12")]
        public void Paths_and_malformed_refs_are_not_refs(string? text) => Assert.False(CallableText.TryParseRef(text, out _));
    }
}
