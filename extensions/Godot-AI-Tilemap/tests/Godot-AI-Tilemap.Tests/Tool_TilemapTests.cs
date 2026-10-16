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
    /// Pins the <c>[AiToolType] Tool_Tilemap</c> family's pure-managed surface: the tool-id consts (the ids the
    /// dock / godot-cli / shared catalog reference, which must not drift silently from the editor files'
    /// <c>[AiTool(...)]</c> ids) and that the family is constructible with no Godot binary. Every tilemap-* tool
    /// itself is editor-only (<c>#if TOOLS</c>) and is verified by the headless-Godot E2E leg instead.
    /// </summary>
    public class Tool_TilemapTests
    {
        [Fact]
        public void Family_IsConstructible()
        {
            var tool = new Tool_Tilemap();
            Assert.NotNull(tool);
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("tilemap-create", Tool_Tilemap.CreateToolId);
            Assert.Equal("tilemap-set-tileset", Tool_Tilemap.SetTileSetToolId);
            Assert.Equal("tilemap-set-cell", Tool_Tilemap.SetCellToolId);
            Assert.Equal("tilemap-erase-cell", Tool_Tilemap.EraseCellToolId);
            Assert.Equal("tilemap-get-used-cells", Tool_Tilemap.GetUsedCellsToolId);
            Assert.Equal("tilemap-clear", Tool_Tilemap.ClearToolId);
        }
    }
}
