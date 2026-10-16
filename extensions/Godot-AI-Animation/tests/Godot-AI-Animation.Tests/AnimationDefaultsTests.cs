/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Animation;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Animation.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="AnimationDefaults"/> — the recommended starter config AND the
    /// value-clamping rules the editor tools reuse before writing to a live <c>Animation</c> resource. These
    /// are the testable core that backs the editor-only <c>animation-create</c>/<c>-insert-key</c> tools (which
    /// themselves need a live Godot editor, exercised by the E2E leg).
    /// </summary>
    public class AnimationDefaultsTests
    {
        [Theory]
        [InlineData(AnimationLoopMode.None, "none")]
        [InlineData(AnimationLoopMode.Linear, "linear")]
        [InlineData(AnimationLoopMode.PingPong, "pingpong")]
        public void For_ReturnsPopulatedStarterConfig(AnimationLoopMode mode, string expectedLabel)
        {
            var info = AnimationDefaults.For(mode);

            Assert.Equal(expectedLabel, info.LoopMode);
            Assert.Equal(string.Empty, info.Name);            // not bound to a library
            Assert.Equal(string.Empty, info.QualifiedName);
            Assert.Equal(AnimationDefaults.DefaultLibraryName, info.Library);
            Assert.Equal(AnimationDefaults.DefaultLength, info.Length);
            Assert.Equal(0, info.TrackCount);
            Assert.Empty(info.Tracks);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void ClampLength_NonPositive_BecomesMinPositive(double input)
        {
            Assert.Equal(AnimationDefaults.MinLength, AnimationDefaults.ClampLength(input));
        }

        [Fact]
        public void ClampLength_PositivePassesThrough()
        {
            Assert.Equal(2.5, AnimationDefaults.ClampLength(2.5));
        }

        [Theory]
        [InlineData(-1.0, 0.0)]
        [InlineData(double.NaN, 0.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(0.75, 0.75)]
        public void ClampTime_FloorsAtZero(double input, double expected)
        {
            Assert.Equal(expected, AnimationDefaults.ClampTime(input));
        }

        [Theory]
        [InlineData(-5, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 0)]
        [InlineData(3, 3)]
        public void ClampTrackIndex_FloorsAtZero(int input, int expected)
        {
            Assert.Equal(expected, AnimationDefaults.ClampTrackIndex(input));
        }
    }
}
