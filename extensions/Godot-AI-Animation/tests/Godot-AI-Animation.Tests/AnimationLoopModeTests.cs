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
    /// Pure-managed specs for <see cref="AnimationLoopModes"/> — the loop-mode parsing every animation tool
    /// uses to map an LLM-supplied string onto Godot's <c>Animation.LoopModeEnum</c>. No Godot binary required.
    /// </summary>
    public class AnimationLoopModeTests
    {
        [Theory]
        [InlineData("none", AnimationLoopMode.None)]
        [InlineData("NONE", AnimationLoopMode.None)]
        [InlineData("once", AnimationLoopMode.None)]
        [InlineData("0", AnimationLoopMode.None)]
        [InlineData("linear", AnimationLoopMode.Linear)]
        [InlineData("loop", AnimationLoopMode.Linear)]
        [InlineData(" 1 ", AnimationLoopMode.Linear)]
        [InlineData("pingpong", AnimationLoopMode.PingPong)]
        [InlineData("ping-pong", AnimationLoopMode.PingPong)]
        [InlineData("2", AnimationLoopMode.PingPong)]
        public void TryParse_KnownValues_ParseToExpected(string input, AnimationLoopMode expected)
        {
            Assert.True(AnimationLoopModes.TryParse(input, out var mode));
            Assert.Equal(expected, mode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("bounce")]
        [InlineData("3")]
        public void TryParse_UnknownOrEmpty_ReturnsFalse(string? input)
        {
            Assert.False(AnimationLoopModes.TryParse(input, out _));
        }

        [Fact]
        public void Parse_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => AnimationLoopModes.Parse("spiral"));
        }

        [Theory]
        [InlineData(AnimationLoopMode.None, "none", 0)]
        [InlineData(AnimationLoopMode.Linear, "linear", 1)]
        [InlineData(AnimationLoopMode.PingPong, "pingpong", 2)]
        public void Label_And_GodotValue_AreStable(AnimationLoopMode mode, string label, int godotValue)
        {
            Assert.Equal(label, mode.ToLabel());
            Assert.Equal(godotValue, mode.ToGodotValue());
        }

        [Theory]
        [InlineData(0, AnimationLoopMode.None)]
        [InlineData(1, AnimationLoopMode.Linear)]
        [InlineData(2, AnimationLoopMode.PingPong)]
        [InlineData(99, AnimationLoopMode.None)]
        public void FromGodotValue_MapsBack(int value, AnimationLoopMode expected)
        {
            Assert.Equal(expected, AnimationLoopModes.FromGodotValue(value));
        }
    }
}
