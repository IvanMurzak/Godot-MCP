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
using com.IvanMurzak.Godot.MCP.Tilemap;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tilemap.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="TilemapValidation"/> — the argument-validation rules the editor-only
    /// <c>tilemap-set-cell</c> / <c>tilemap-set-tileset</c> tools reuse before touching a live TileMapLayer.
    /// These are the testable core that backs those editor tools (which themselves need a live Godot editor,
    /// exercised by the E2E leg). No Godot binary required.
    /// </summary>
    public class TilemapValidationTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(42)]
        public void ValidateSourceId_NonNegative_DoesNotThrow(int sourceId)
        {
            TilemapValidation.ValidateSourceId(sourceId); // no throw
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-5)]
        public void ValidateSourceId_Negative_Throws(int sourceId)
        {
            Assert.Throws<ArgumentException>(() => TilemapValidation.ValidateSourceId(sourceId));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 2)]
        [InlineData(10, 0)]
        public void ValidateAtlasCoords_NonNegative_DoesNotThrow(int atlasX, int atlasY)
        {
            TilemapValidation.ValidateAtlasCoords(atlasX, atlasY); // no throw
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(-2, -3)]
        public void ValidateAtlasCoords_Negative_Throws(int atlasX, int atlasY)
        {
            Assert.Throws<ArgumentException>(() => TilemapValidation.ValidateAtlasCoords(atlasX, atlasY));
        }

        [Theory]
        [InlineData("res://tiles.tres", "res://tiles.tres")]
        [InlineData("  res://a/b/tiles.tres  ", "res://a/b/tiles.tres")]
        public void ValidateResourcePath_Valid_ReturnsTrimmed(string input, string expected)
        {
            Assert.Equal(expected, TilemapValidation.ValidateResourcePath(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("tiles.tres")]
        [InlineData("C:/abs/tiles.tres")]
        [InlineData("user://tiles.tres")]
        public void ValidateResourcePath_InvalidOrBlank_Throws(string? input)
        {
            Assert.Throws<ArgumentException>(() => TilemapValidation.ValidateResourcePath(input));
        }

        [Fact]
        public void NoSourceId_IsMinusOne()
        {
            Assert.Equal(-1, TilemapValidation.NoSourceId);
        }
    }
}
