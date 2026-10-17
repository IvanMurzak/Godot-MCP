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
using GdGridMap = Godot.GridMap;

namespace com.IvanMurzak.Godot.MCP.GridMap.Tests
{
    /// <summary>
    /// The module configures cleanly and shapes GridMap: an RQL engine discovers it, registers its guide, and the member
    /// table of GridMap carries the validated settings, the cell rows and the cell calls. Metadata only: no Godot object is
    /// created.
    /// </summary>
    public class GridMapModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(GridMapModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(GridMapModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == GridMapGuide.Topic);
            Assert.Equal(GridMapGuide.Markdown, guide.Value);
        }

        [Fact]
        public void GridMapHasValidatedSettings()
        {
            var info = Engine().Types.Info(typeof(GdGridMap));
            foreach (var (name, type) in new[] { ("CellSize", typeof(Vector3)), ("CellOctantSize", typeof(int)) })
            {
                var member = Assert.Single(info.Members, m => m.Name == name);
                Assert.Null(member.Member); // the adapter's validated member replaced the C# property
                Assert.Equal(type, member.Type);
                Assert.True(member.CanWrite);
                Assert.False(string.IsNullOrEmpty(member.Description));
            }
            Assert.Contains(info.Members, m => m.Name == "MeshLibrary" && m.CanWrite);
        }

        [Fact]
        public void GridMapHasCellRowsAndCalls()
        {
            var info = Engine().Types.Info(typeof(GdGridMap));
            Assert.Contains(info.Members, m => m.Name == "cells" && !m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "cellCount" && m.Type == typeof(int) && !m.CanWrite);
            var set = Assert.Single(info.Callable, c => c.Name == "SetCell");
            Assert.Equal(new[] { "x", "y", "z", "item", "orientation" }, set.Parameters.Select(p => p.Name));
            Assert.Equal(new[] { false, false, false, false, true }, set.Parameters.Select(p => p.HasDefault));
            Assert.Contains(info.Callable, c => c.Name == "ClearCell" && c.Parameters.Count == 3);
            Assert.Contains(info.Callable, c => c.Name == "Clear" && c.Parameters.Count == 0);
        }

        [Fact]
        public void CellRowsAreReadOnlyColumns()
        {
            var info = Engine().Types.Info(typeof(GridMapCellRow));
            Assert.Equal(new[] { "x", "y", "z", "item", "itemName", "orientation" }, info.Members.Select(m => m.Name));
            Assert.All(info.Members, m => Assert.False(m.CanWrite));
        }
    }
}
