/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Tilemap;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tilemap.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="TileMapLayerInfo"/> — the structured result every <c>tilemap-*</c> tool
    /// returns. Pins the safe defaults (non-null empty strings + non-null empty cell list) the editor tools and
    /// ReflectorNet serialization rely on.
    /// </summary>
    public class TileMapLayerInfoTests
    {
        [Fact]
        public void Defaults_AreEmptyAndNonNull()
        {
            var info = new TileMapLayerInfo();

            Assert.Equal(string.Empty, info.NodePath);
            Assert.Equal(string.Empty, info.TypeName);
            Assert.Equal(string.Empty, info.TileSetPath);
            Assert.Equal(0, info.UsedCellCount);
            Assert.NotNull(info.UsedCells);
            Assert.Empty(info.UsedCells);
        }

        [Fact]
        public void UsedCells_AreAssignable()
        {
            var info = new TileMapLayerInfo
            {
                NodePath = "Root/Layer",
                TypeName = "TileMapLayer",
                TileSetPath = "res://tiles.tres",
                UsedCellCount = 1,
                UsedCells = { new TileCell(0, 0, 0, 1, 1) }
            };

            Assert.Single(info.UsedCells);
            Assert.Equal(1, info.UsedCellCount);
            Assert.Equal("res://tiles.tres", info.TileSetPath);
        }
    }
}
