/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Animation.Tests
{
    public class AnimationRulesTests
    {
        [Theory]
        [InlineData(0.001)]
        [InlineData(1.0)]
        [InlineData(120.0)]
        public void LengthOfAtLeastAMillisecondIsValid(double length) => Assert.Null(AnimationRules.Length(length));

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.0005)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void ShorterOrNonFiniteLengthIsRejected(double length) => Assert.StartsWith("Length must be", AnimationRules.Length(length));

        [Theory]
        [InlineData(0.0)]
        [InlineData(2.5)]
        public void KeyTimeOfZeroOrMoreIsValid(double time) => Assert.Null(AnimationRules.KeyTime(time));

        [Theory]
        [InlineData(-0.1)]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        public void NegativeOrNonFiniteKeyTimeIsRejected(double time) => Assert.Contains("at least 0", AnimationRules.KeyTime(time));

        [Theory]
        [InlineData("Idle")]
        [InlineData("walk cycle 2")]
        public void PlainAnimationNamesAreValid(string name) => Assert.Null(AnimationRules.AnimationName(name));

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Extra/Walk")]
        [InlineData("a:b")]
        [InlineData("a,b")]
        [InlineData("a[0]")]
        public void EmptyOrSeparatorAnimationNamesAreRejected(string? name) => Assert.NotNull(AnimationRules.AnimationName(name));

        [Theory]
        [InlineData("")]
        [InlineData("Extra")]
        public void GlobalAndPlainLibraryNamesAreValid(string name) => Assert.Null(AnimationRules.LibraryName(name));

        [Theory]
        [InlineData(null)]
        [InlineData("a/b")]
        [InlineData("a:b")]
        public void NullOrSeparatorLibraryNamesAreRejected(string? name) => Assert.NotNull(AnimationRules.LibraryName(name));

        [Theory]
        [InlineData(0, 1)]
        [InlineData(2, 3)]
        public void ExistingTrackIndexIsValid(int index, int count) => Assert.Null(AnimationRules.TrackIndex(index, count));

        [Theory]
        [InlineData(-1, 3)]
        [InlineData(3, 3)]
        [InlineData(0, 0)]
        public void MissingTrackIndexIsRejected(int index, int count) => Assert.Contains($"track {index} does not exist", AnimationRules.TrackIndex(index, count));

        [Theory]
        [InlineData(AnimationTrackKind.Value, "Mover:position:x")]
        [InlineData(AnimationTrackKind.Position3D, "Mover")]
        [InlineData(AnimationTrackKind.Rotation3D, ".")]
        public void TrackPathsAreValid(AnimationTrackKind kind, string path) => Assert.Null(AnimationRules.TrackPath(kind, path));

        [Fact]
        public void ValueTrackPathNeedsAProperty() =>
            Assert.Contains("\"node:property\"", AnimationRules.TrackPath(AnimationTrackKind.Value, "Mover"));

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public void EmptyTrackPathIsRejected(string? path) => Assert.Contains("cannot be empty", AnimationRules.TrackPath(AnimationTrackKind.Scale3D, path));
    }
}
