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
    /// Pure-managed specs for <see cref="AnimationTrackKinds"/> — the track-kind parsing the
    /// <c>animation-add-track</c> / <c>-insert-key</c> tools use to map an LLM-supplied string onto Godot's
    /// <c>Animation.TrackType</c>. No Godot binary required.
    /// </summary>
    public class AnimationTrackKindTests
    {
        [Theory]
        [InlineData("value", AnimationTrackKind.Value)]
        [InlineData("VAL", AnimationTrackKind.Value)]
        [InlineData("position-3d", AnimationTrackKind.Position3D)]
        [InlineData("position3d", AnimationTrackKind.Position3D)]
        [InlineData("pos", AnimationTrackKind.Position3D)]
        [InlineData("rotation-3d", AnimationTrackKind.Rotation3D)]
        [InlineData("rot3d", AnimationTrackKind.Rotation3D)]
        [InlineData("scale-3d", AnimationTrackKind.Scale3D)]
        [InlineData(" scale ", AnimationTrackKind.Scale3D)]
        public void TryParse_KnownValues_ParseToExpected(string input, AnimationTrackKind expected)
        {
            Assert.True(AnimationTrackKinds.TryParse(input, out var kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("bezier")]
        [InlineData("method")]
        public void TryParse_UnknownOrEmpty_ReturnsFalse(string? input)
        {
            Assert.False(AnimationTrackKinds.TryParse(input, out _));
        }

        [Fact]
        public void Parse_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => AnimationTrackKinds.Parse("audio"));
        }

        [Theory]
        [InlineData(AnimationTrackKind.Value, "value", 0)]
        [InlineData(AnimationTrackKind.Position3D, "position-3d", 1)]
        [InlineData(AnimationTrackKind.Rotation3D, "rotation-3d", 2)]
        [InlineData(AnimationTrackKind.Scale3D, "scale-3d", 3)]
        public void Label_And_GodotTrackType_AreStable(AnimationTrackKind kind, string label, int trackType)
        {
            Assert.Equal(label, kind.ToLabel());
            Assert.Equal(trackType, kind.ToGodotTrackType());
        }

        [Theory]
        [InlineData(0, AnimationTrackKind.Value)]
        [InlineData(1, AnimationTrackKind.Position3D)]
        [InlineData(2, AnimationTrackKind.Rotation3D)]
        [InlineData(3, AnimationTrackKind.Scale3D)]
        public void FromGodotTrackType_KnownTypes_MapBack(int trackType, AnimationTrackKind expected)
        {
            Assert.Equal(expected, AnimationTrackKinds.FromGodotTrackType(trackType));
        }

        [Theory]
        [InlineData(4)] // blend-shape
        [InlineData(5)] // method
        [InlineData(6)] // bezier
        public void FromGodotTrackType_UnmodeledTypes_ReturnNull(int trackType)
        {
            Assert.Null(AnimationTrackKinds.FromGodotTrackType(trackType));
        }

        [Theory]
        [InlineData(AnimationTrackKind.Value, false)]
        [InlineData(AnimationTrackKind.Position3D, true)]
        [InlineData(AnimationTrackKind.Rotation3D, true)]
        [InlineData(AnimationTrackKind.Scale3D, true)]
        public void IsTransform3D_TrueForTransformTracks(AnimationTrackKind kind, bool expected)
        {
            Assert.Equal(expected, kind.IsTransform3D());
        }
    }
}
