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

namespace com.IvanMurzak.Godot.MCP.Navigation.Tests
{
    public class NavigationRulesTests
    {
        [Theory]
        [InlineData(0.01f)]
        [InlineData(0.75f)]
        public void PositiveRadiusIsValid(float radius) => Assert.Null(NavigationRules.Radius(radius));

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void ZeroNegativeOrNaNRadiusIsRejected(float radius) => Assert.StartsWith("Radius must be above 0", NavigationRules.Radius(radius));

        [Theory]
        [InlineData(0f)]
        [InlineData(8f)]
        public void ZeroOrMoreIsValid(float value) => Assert.Null(NavigationRules.NonNegative("MaxSpeed", value));

        [Theory]
        [InlineData(-0.5f)]
        [InlineData(float.NaN)]
        [InlineData(float.NegativeInfinity)]
        public void NegativeOrNonFiniteIsRejectedByName(float value) =>
            Assert.StartsWith("TravelCost must be 0 or more", NavigationRules.NonNegative("TravelCost", value));

        [Theory]
        [InlineData(0f, true)]
        [InlineData(1f, true)]
        [InlineData(0.5f, true)]
        [InlineData(-0.1f, false)]
        [InlineData(1.5f, false)]
        [InlineData(float.NaN, false)]
        public void PriorityIsZeroToOne(float value, bool valid) => Assert.Equal(valid, NavigationRules.Priority(value) == null);

        [Fact]
        public void OutlinesNeedThreePoints()
        {
            Assert.Null(NavigationRules.Outline(3));
            Assert.Equal("an outline needs at least 3 points (got 2)", NavigationRules.Outline(2));
        }

        [Fact]
        public void BakeNeedsTheResourceAndNoRunningBake()
        {
            Assert.Null(NavigationRules.CanBake("NavigationRegion3D 'R'", "NavigationMesh", hasResource: true, baking: false));
            Assert.Equal("NavigationRegion3D 'R' has no NavigationMesh to bake into: set NavigationMesh = {\"$type\": \"NavigationMesh\"} first",
                NavigationRules.CanBake("NavigationRegion3D 'R'", "NavigationMesh", hasResource: false, baking: false));
            Assert.Equal("NavigationRegion2D 'R' is already baking",
                NavigationRules.CanBake("NavigationRegion2D 'R'", "NavigationPolygon", hasResource: true, baking: true));
        }
    }
}
