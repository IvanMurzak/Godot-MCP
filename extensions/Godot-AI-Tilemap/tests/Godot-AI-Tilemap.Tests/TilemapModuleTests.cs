/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Linq;
using Godot;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tilemap.Tests
{
    /// <summary>
    /// The module configures cleanly and shapes TileMapLayer: an RQL engine discovers it, registers its guide, and the member
    /// table of TileMapLayer carries the cell rows and the cell calls. Metadata only: no Godot object is created.
    /// </summary>
    public class TilemapModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(TilemapModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(TilemapModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == TilemapGuide.Topic);
            Assert.Equal(TilemapGuide.Markdown, guide.Value);
        }

        [Fact]
        public void LayerHasCellRowsAndCalls()
        {
            var info = Engine().Types.Info(typeof(TileMapLayer));
            Assert.Contains(info.Members, m => m.Name == "usedCells" && !m.CanWrite && !string.IsNullOrEmpty(m.Description));
            Assert.Contains(info.Members, m => m.Name == "usedCellCount" && m.Type == typeof(int) && !m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "TileSet" && m.CanWrite);
            var set = Assert.Single(info.Callable, c => c.Name == "SetCell");
            Assert.Equal(new[] { "x", "y", "sourceId", "atlasX", "atlasY", "alternative" }, set.Parameters.Select(p => p.Name));
            Assert.Equal(new[] { false, false, false, false, false, true }, set.Parameters.Select(p => p.HasDefault));
            Assert.Contains(info.Callable, c => c.Name == "EraseCell" && c.Parameters.Count == 2);
            Assert.Contains(info.Callable, c => c.Name == "Clear" && c.Parameters.Count == 0);
        }

        [Fact]
        public void CellRowsAreReadOnlyColumns()
        {
            var info = Engine().Types.Info(typeof(TilemapCellRow));
            Assert.Equal(new[] { "x", "y", "sourceId", "atlasX", "atlasY", "alternative" }, info.Members.Select(m => m.Name));
            Assert.All(info.Members, m => Assert.False(m.CanWrite));
        }
    }
}
