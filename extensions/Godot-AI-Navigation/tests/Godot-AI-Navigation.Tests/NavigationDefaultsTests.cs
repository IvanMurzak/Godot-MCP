/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Navigation;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Navigation.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="NavigationDefaults"/> — the recommended starter agent config AND the
    /// value-clamping rules the <c>navigation-agent-configure</c> editor tool reuses before writing to a live
    /// node. These are the testable core that backs the editor-only agent tools (which themselves need a live
    /// Godot editor, exercised by the E2E leg).
    /// </summary>
    public class NavigationDefaultsTests
    {
        [Fact]
        public void ForAgent_3D_ReturnsMetreScaledStarterConfig()
        {
            var info = NavigationDefaults.ForAgent(NavigationDimension.Three);

            Assert.Equal("3D", info.Dimension);
            Assert.Equal("Agent", info.Kind);
            Assert.Equal(string.Empty, info.NodePath);   // not bound to a node
            Assert.Equal(NavigationDefaults.Default3DRadius, info.Radius);
            Assert.Equal(NavigationDefaults.Default3DPathDesiredDistance, info.PathDesiredDistance);
            Assert.Equal(NavigationDefaults.Default3DTargetDesiredDistance, info.TargetDesiredDistance);
            Assert.Equal(NavigationDefaults.Default3DMaxSpeed, info.MaxSpeed);
            Assert.False(info.AvoidanceEnabled);
        }

        [Fact]
        public void ForAgent_2D_ReturnsPixelScaledStarterConfig()
        {
            var info = NavigationDefaults.ForAgent(NavigationDimension.Two);

            Assert.Equal("2D", info.Dimension);
            Assert.Equal("Agent", info.Kind);
            Assert.Equal(NavigationDefaults.Default2DRadius, info.Radius);
            Assert.Equal(NavigationDefaults.Default2DPathDesiredDistance, info.PathDesiredDistance);
            Assert.Equal(NavigationDefaults.Default2DTargetDesiredDistance, info.TargetDesiredDistance);
            Assert.Equal(NavigationDefaults.Default2DMaxSpeed, info.MaxSpeed);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-3f)]
        [InlineData(float.NaN)]
        public void ClampRadius_NonPositive_BecomesMinPositive(float input)
        {
            Assert.Equal(NavigationDefaults.MinRadius, NavigationDefaults.ClampRadius(input));
        }

        [Fact]
        public void ClampRadius_PositivePassesThrough()
        {
            Assert.Equal(0.75f, NavigationDefaults.ClampRadius(0.75f));
        }

        [Theory]
        [InlineData(-1f, 0f)]
        [InlineData(0f, 0f)]
        [InlineData(2.5f, 2.5f)]
        public void ClampDistance_FloorsAtZero(float input, float expected)
        {
            Assert.Equal(expected, NavigationDefaults.ClampDistance(input));
        }

        [Fact]
        public void ClampDistance_NaN_BecomesZero()
        {
            Assert.Equal(0f, NavigationDefaults.ClampDistance(float.NaN));
        }

        [Theory]
        [InlineData(-5f, 0f)]
        [InlineData(0f, 0f)]
        [InlineData(12f, 12f)]
        public void ClampMaxSpeed_FloorsAtZero(float input, float expected)
        {
            Assert.Equal(expected, NavigationDefaults.ClampMaxSpeed(input));
        }
    }
}
