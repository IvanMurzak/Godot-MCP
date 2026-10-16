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
    /// Pure-managed specs for the serializable result DTOs (<see cref="AnimationPlayerInfo"/>,
    /// <see cref="AnimationClipInfo"/>, <see cref="AnimationTrackInfo"/>). These are built inside the editor
    /// tools' <c>MainThread.Instance.Run(...)</c> delegates and returned across the tool boundary, so their
    /// default shape (non-null collections, empty strings) must be stable — a null list would break
    /// ReflectorNet serialization on the live MCP path. No Godot binary required.
    /// </summary>
    public class AnimationInfoTests
    {
        [Fact]
        public void AnimationPlayerInfo_DefaultShape_HasNonNullCollections_AndNoSelectedAnimation()
        {
            var info = new AnimationPlayerInfo();

            Assert.Equal(string.Empty, info.NodePath);
            Assert.Equal(string.Empty, info.TypeName);
            Assert.NotNull(info.Libraries);
            Assert.Empty(info.Libraries);
            Assert.Equal(0, info.LibraryCount);
            Assert.NotNull(info.Animations);
            Assert.Empty(info.Animations);
            Assert.Equal(0, info.AnimationCount);
            Assert.Null(info.SelectedAnimation);
        }

        [Fact]
        public void AnimationClipInfo_DefaultShape_HasNonNullTrackList()
        {
            var info = new AnimationClipInfo();

            Assert.Equal(string.Empty, info.Name);
            Assert.Equal(string.Empty, info.Library);
            Assert.Equal(string.Empty, info.QualifiedName);
            Assert.Equal(string.Empty, info.LoopMode);
            Assert.Equal(0, info.TrackCount);
            Assert.NotNull(info.Tracks);
            Assert.Empty(info.Tracks);
        }

        [Fact]
        public void AnimationTrackInfo_RoundTripsItsFields()
        {
            var track = new AnimationTrackInfo
            {
                Index = 2,
                Kind = "value",
                Path = "Sprite:modulate",
                KeyCount = 3
            };

            Assert.Equal(2, track.Index);
            Assert.Equal("value", track.Kind);
            Assert.Equal("Sprite:modulate", track.Path);
            Assert.Equal(3, track.KeyCount);
        }
    }
}
