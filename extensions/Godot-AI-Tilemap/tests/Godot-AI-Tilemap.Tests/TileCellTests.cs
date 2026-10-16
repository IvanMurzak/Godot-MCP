/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Tilemap;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tilemap.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="TileCell"/> — the serializable per-cell snapshot the editor tools
    /// build (inside their main-thread delegate) from a live TileMapLayer and return across the tool boundary.
    /// </summary>
    public class TileCellTests
    {
        [Fact]
        public void Constructor_PopulatesAllFields()
        {
            var cell = new TileCell(3, -2, 1, 4, 5);

            Assert.Equal(3, cell.X);
            Assert.Equal(-2, cell.Y);
            Assert.Equal(1, cell.SourceId);
            Assert.Equal(4, cell.AtlasX);
            Assert.Equal(5, cell.AtlasY);
        }

        [Fact]
        public void DefaultConstructor_ZeroesFields()
        {
            var cell = new TileCell();

            Assert.Equal(0, cell.X);
            Assert.Equal(0, cell.Y);
            Assert.Equal(0, cell.SourceId);
            Assert.Equal(0, cell.AtlasX);
            Assert.Equal(0, cell.AtlasY);
        }

        [Fact]
        public void ToString_IsHumanReadable()
        {
            var cell = new TileCell(1, 2, 0, 3, 4);
            Assert.Equal("(1,2) source=0 atlas=(3,4)", cell.ToString());
        }
    }
}
