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

namespace com.IvanMurzak.Godot.MCP.GridMap.Tests
{
    public class GridMapRulesTests
    {
        [Theory]
        [InlineData(1f, 1f, 1f)]
        [InlineData(0.001f, 2f, 0.5f)]
        public void CellSizeOfAtLeastAMillimetreIsValid(float x, float y, float z) => Assert.Null(GridMapRules.CellSize(x, y, z));

        [Theory]
        [InlineData(0f, 1f, 1f)]
        [InlineData(1f, -2f, 1f)]
        [InlineData(1f, 1f, 0.0005f)]
        [InlineData(float.NaN, 1f, 1f)]
        [InlineData(1f, float.PositiveInfinity, 1f)]
        public void SmallerOrNonFiniteCellSizeIsRejected(float x, float y, float z) =>
            Assert.StartsWith("CellSize must be at least 0.001", GridMapRules.CellSize(x, y, z));

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        public void OctantSizeOfOneOrMoreIsValid(int size) => Assert.Null(GridMapRules.OctantSize(size));

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        public void OctantSizeBelowOneIsRejected(int size) => Assert.Contains("at least 1", GridMapRules.OctantSize(size));

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(-32767, 32767, 7)]
        public void CellsInsideTheGridAreValid(int x, int y, int z) => Assert.Null(GridMapRules.Cell(x, y, z));

        [Theory]
        [InlineData(32768, 0, 0)]
        [InlineData(0, -32768, 0)]
        [InlineData(1048575, 0, 0)]
        [InlineData(0, 0, int.MinValue)]
        public void CellsOutsideTheGridAreRejected(int x, int y, int z) => Assert.Contains("outside the grid", GridMapRules.Cell(x, y, z));

        [Theory]
        [InlineData(0)]
        [InlineData(42)]
        public void ItemIdsOfZeroOrMoreAreValid(int item) => Assert.Null(GridMapRules.Item(item));

        [Theory]
        [InlineData(-1)]
        [InlineData(-7)]
        public void NegativeItemIdsAreRejectedPointingToClearCell(int item) => Assert.Contains("ClearCell", GridMapRules.Item(item));

        [Theory]
        [InlineData(0)]
        [InlineData(23)]
        public void OrientationsFromZeroTo23AreValid(int orientation) => Assert.Null(GridMapRules.Orientation(orientation));

        [Theory]
        [InlineData(-1)]
        [InlineData(24)]
        public void OrientationsOutsideZeroTo23AreRejected(int orientation) => Assert.Contains("from 0 to 23", GridMapRules.Orientation(orientation));
    }
}
