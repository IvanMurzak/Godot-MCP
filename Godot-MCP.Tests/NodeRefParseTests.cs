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
using com.IvanMurzak.Godot.MCP.Data;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>An RQL $ref an agent copies into a node argument (screenshot-camera nodeRef "Camera3D#2990639514705").</summary>
    public class NodeRefParseTests
    {
        [Theory]
        [InlineData("Camera3D#2990639514705", "Camera3D", 2990639514705UL)]
        [InlineData("GPUParticles3D#42", "GPUParticles3D", 42UL)]
        [InlineData("My_Node#7", "My_Node", 7UL)]
        public void A_ref_gives_class_and_instance_id(string text, string cls, ulong id)
        {
            Assert.True(NodeRef.TryParseRef(text, out var c, out var i));
            Assert.Equal(cls, c);
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
        public void Paths_and_malformed_refs_are_not_refs(string? text) => Assert.False(NodeRef.TryParseRef(text, out _, out _));
    }
}
