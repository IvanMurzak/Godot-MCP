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
using com.IvanMurzak.Godot.MCP.Navigation;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Navigation.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="NavigationDimensions"/> + <see cref="NavigationClassNames"/> — the
    /// dimension parsing and (kind, dimension) → Godot class-name mapping every navigation tool uses to map an
    /// LLM-supplied '2D'/'3D' string onto the right Godot node type. No Godot binary required.
    /// </summary>
    public class NavigationDimensionTests
    {
        [Theory]
        [InlineData("2D", NavigationDimension.Two)]
        [InlineData("2d", NavigationDimension.Two)]
        [InlineData(" 2 ", NavigationDimension.Two)]
        [InlineData("two", NavigationDimension.Two)]
        [InlineData("3D", NavigationDimension.Three)]
        [InlineData("3d", NavigationDimension.Three)]
        [InlineData("3", NavigationDimension.Three)]
        [InlineData("THREE", NavigationDimension.Three)]
        public void TryParse_KnownValues_ParseToExpected(string input, NavigationDimension expected)
        {
            Assert.True(NavigationDimensions.TryParse(input, out var dimension));
            Assert.Equal(expected, dimension);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("4D")]
        [InlineData("flat")]
        public void TryParse_UnknownOrEmpty_ReturnsFalse(string? input)
        {
            Assert.False(NavigationDimensions.TryParse(input, out _));
        }

        [Fact]
        public void Parse_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => NavigationDimensions.Parse("hexagonal"));
        }

        [Fact]
        public void ToLabel_IsStable()
        {
            Assert.Equal("2D", NavigationDimension.Two.ToLabel());
            Assert.Equal("3D", NavigationDimension.Three.ToLabel());
        }

        [Theory]
        [InlineData(NavigationKind.Region, NavigationDimension.Two, "NavigationRegion2D")]
        [InlineData(NavigationKind.Region, NavigationDimension.Three, "NavigationRegion3D")]
        [InlineData(NavigationKind.Agent, NavigationDimension.Two, "NavigationAgent2D")]
        [InlineData(NavigationKind.Agent, NavigationDimension.Three, "NavigationAgent3D")]
        [InlineData(NavigationKind.Link, NavigationDimension.Two, "NavigationLink2D")]
        [InlineData(NavigationKind.Link, NavigationDimension.Three, "NavigationLink3D")]
        public void ClassNames_MapEveryKindAndDimension(NavigationKind kind, NavigationDimension dim, string expected)
        {
            Assert.Equal(expected, NavigationClassNames.For(kind, dim));
        }

        [Fact]
        public void KindLabels_AreStable()
        {
            Assert.Equal("Region", NavigationKind.Region.ToLabel());
            Assert.Equal("Agent", NavigationKind.Agent.ToLabel());
            Assert.Equal("Link", NavigationKind.Link.ToLabel());
        }
    }
}
