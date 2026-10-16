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
    /// Pure-managed specs for <see cref="GridMapCellInfo"/> — the cell-snapshot the editor-only
    /// <c>gridmap-set-cell</c> / <c>gridmap-clear-cell</c> tools return. The <see cref="GridMapCellInfo.IsEmpty"/>
    /// invariant and the <see cref="GridMapCellInfo.Empty"/> factory carry the only managed logic and are
    /// verified here with no Godot binary.
    /// </summary>
    public class GridMapCellInfoTests
    {
        [Theory]
        [InlineData(-1, true)]
        [InlineData(-5, true)]
        [InlineData(0, false)]
        [InlineData(3, false)]
        public void IsEmpty_TracksNegativeItem(int item, bool expectedEmpty)
        {
            var cell = new GridMapCellInfo { Item = item };
            Assert.Equal(expectedEmpty, cell.IsEmpty);
        }

        [Fact]
        public void Empty_BuildsAnEmptyCellAtCoordinates()
        {
            var cell = GridMapCellInfo.Empty("Root/Grid", 1, 2, 3);

            Assert.Equal("Root/Grid", cell.NodePath);
            Assert.Equal(1, cell.CellX);
            Assert.Equal(2, cell.CellY);
            Assert.Equal(3, cell.CellZ);
            Assert.Equal(GridMapDefaults.InvalidItem, cell.Item);
            Assert.Equal(GridMapDefaults.InvalidItem, cell.Orientation);
            Assert.True(cell.IsEmpty);
        }
    }
}
