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

namespace com.IvanMurzak.Godot.MCP.CSG.Tests
{
    public class CsgRulesTests
    {
        [Theory]
        [InlineData(0.001f)]
        [InlineData(1.5f)]
        public void PositiveDimensionsAreValid(float value) => Assert.Null(CsgRules.Positive("Radius", value));

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void ZeroNegativeOrNonFiniteDimensionsAreRejectedByName(float value)
        {
            var reason = CsgRules.Positive("Height", value);
            Assert.NotNull(reason);
            Assert.StartsWith("Height must be above 0", reason);
        }

        [Fact]
        public void BoxSizeNeedsEveryAxisAboveZero()
        {
            Assert.Null(CsgRules.Size(3f, 3f, 3f));
            Assert.Equal("Size must be above 0 on every axis (got (3, 0, 3))", CsgRules.Size(3f, 0f, 3f));
            Assert.Contains("above 0 on every axis", CsgRules.Size(-1f, 1f, 1f));
            Assert.Contains("above 0 on every axis", CsgRules.Size(1f, 1f, float.NaN));
        }

        [Theory]
        [InlineData(4, true)]
        [InlineData(16, true)]
        [InlineData(3, false)]
        [InlineData(0, false)]
        public void RadialSegmentsStartAtFour(int value, bool valid) =>
            Assert.Equal(valid, CsgRules.AtLeast("RadialSegments", value, CsgRules.MinRadialSegments) == null);

        [Fact]
        public void CountMessagesNameTheMemberAndTheFloor()
        {
            Assert.Equal("RadialSegments must be at least 4 (got 2)", CsgRules.AtLeast("RadialSegments", 2, CsgRules.MinRadialSegments));
            Assert.Equal("Sides must be at least 3 (got 2)", CsgRules.AtLeast("Sides", 2, CsgRules.MinSides));
            Assert.Equal("Rings must be at least 1 (got 0)", CsgRules.AtLeast("Rings", 0, CsgRules.MinRings));
            Assert.Null(CsgRules.AtLeast("Sides", 3, CsgRules.MinSides));
        }
    }
}
