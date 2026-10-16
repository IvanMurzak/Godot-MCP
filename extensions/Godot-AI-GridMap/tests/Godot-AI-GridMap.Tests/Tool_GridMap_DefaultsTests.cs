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
    /// Unit spec for the PURE-MANAGED <c>gridmap-defaults</c> tool — constructs the tool family and invokes
    /// the method directly (no Godot binary, no MCP server). The editor-only tools (<c>gridmap-create</c>,
    /// <c>-set-mesh-library</c>, <c>-set-cell</c>, <c>-clear-cell</c>, <c>-clear</c>, <c>-get</c>) touch a live
    /// editor and are verified by the headless-Godot E2E leg instead; their tool-id constants are pinned here
    /// so the ids the dock / godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_GridMap_DefaultsTests
    {
        [Fact]
        public void Defaults_NoOverride_ReturnsUnitCube()
        {
            var tool = new Tool_GridMap();
            var info = tool.Defaults();

            Assert.Equal("GridMap", info.Kind);
            Assert.Equal(GridMapDefaults.DefaultCellSize, info.CellSizeX);
            Assert.Equal(GridMapDefaults.DefaultCellSize, info.CellSizeY);
            Assert.Equal(GridMapDefaults.DefaultCellSize, info.CellSizeZ);
        }

        [Theory]
        [InlineData(4.0, 4.0)]
        [InlineData(0.5, 0.5)]
        [InlineData(0.0, GridMapDefaults.MinCellSize)]   // non-positive override is clamped
        [InlineData(-2.0, GridMapDefaults.MinCellSize)]
        public void Defaults_CellSizeOverride_AppliesClampedUniformly(double input, double expected)
        {
            var tool = new Tool_GridMap();
            var info = tool.Defaults(input);

            Assert.Equal(expected, info.CellSizeX);
            Assert.Equal(expected, info.CellSizeY);
            Assert.Equal(expected, info.CellSizeZ);
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            // The ids the dock / godot-cli / catalog reference must not drift silently.
            Assert.Equal("gridmap-defaults", Tool_GridMap.DefaultsToolId);
            Assert.Equal("gridmap-create", Tool_GridMap.CreateToolId);
            Assert.Equal("gridmap-set-mesh-library", Tool_GridMap.SetMeshLibraryToolId);
            Assert.Equal("gridmap-set-cell", Tool_GridMap.SetCellToolId);
            Assert.Equal("gridmap-clear-cell", Tool_GridMap.ClearCellToolId);
            Assert.Equal("gridmap-clear", Tool_GridMap.ClearToolId);
            Assert.Equal("gridmap-get", Tool_GridMap.GetToolId);
        }
    }
}
