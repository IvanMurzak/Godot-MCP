/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.GridMap;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.GridMap.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="GridMapDefaults"/> — the recommended starter config AND the
    /// value-clamping rules the editor tools reuse before writing to a live node. These are the testable core
    /// that backs the editor-only <c>gridmap-create</c>/<c>-set-cell</c> tools (which themselves need a live
    /// Godot editor, exercised by the E2E leg). No Godot binary required.
    /// </summary>
    public class GridMapDefaultsTests
    {
        [Fact]
        public void For_ReturnsPopulatedUnitCubeStarterConfig()
        {
            var info = GridMapDefaults.For();

            Assert.Equal("GridMap", info.Kind);
            Assert.Equal(string.Empty, info.NodePath);   // not bound to a node
            Assert.Equal(string.Empty, info.TypeName);
            Assert.Equal(GridMapDefaults.DefaultCellSize, info.CellSizeX);
            Assert.Equal(GridMapDefaults.DefaultCellSize, info.CellSizeY);
            Assert.Equal(GridMapDefaults.DefaultCellSize, info.CellSizeZ);
            Assert.Equal(GridMapDefaults.DefaultCellScale, info.CellScale);
            Assert.Equal(GridMapDefaults.DefaultCellOctantSize, info.CellOctantSize);
            Assert.Equal(string.Empty, info.MeshLibraryPath);
            Assert.Equal(0, info.CellCount);
        }

        [Theory]
        [InlineData(1.0, 1.0)]
        [InlineData(2.5, 2.5)]
        [InlineData(0.0, GridMapDefaults.MinCellSize)]
        [InlineData(-3.0, GridMapDefaults.MinCellSize)]
        [InlineData(double.NaN, GridMapDefaults.MinCellSize)]
        public void ClampCellSize_FloorsAtMinPositive(double input, double expected)
        {
            Assert.Equal(expected, GridMapDefaults.ClampCellSize(input));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(5, 5)]
        [InlineData(-1, 0)]
        [InlineData(-42, 0)]
        public void ClampItem_FloorsAtZero(int input, int expected)
        {
            Assert.Equal(expected, GridMapDefaults.ClampItem(input));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(23, 23)]
        [InlineData(-1, 0)]
        [InlineData(24, 23)]
        [InlineData(100, 23)]
        public void ClampOrientation_ClampsInto0To23(int input, int expected)
        {
            Assert.Equal(expected, GridMapDefaults.ClampOrientation(input));
        }
    }
}
