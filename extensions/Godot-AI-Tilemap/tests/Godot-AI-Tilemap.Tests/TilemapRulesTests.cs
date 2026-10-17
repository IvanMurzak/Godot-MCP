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

namespace com.IvanMurzak.Godot.MCP.Tilemap.Tests
{
    public class TilemapRulesTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void SourceIdsOfZeroOrMoreAreValid(int sourceId) => Assert.Null(TilemapRules.SourceId(sourceId));

        [Theory]
        [InlineData(TilemapRules.NoSourceId)]
        [InlineData(-5)]
        public void NegativeSourceIdsAreRejectedPointingToEraseCell(int sourceId)
        {
            var reason = TilemapRules.SourceId(sourceId);
            Assert.NotNull(reason);
            Assert.Contains("EraseCell", reason);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(2, 7)]
        public void NonNegativeAtlasCoordsAreValid(int x, int y) => Assert.Null(TilemapRules.AtlasCoords(x, y));

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        public void NegativeAtlasCoordsAreRejected(int x, int y) => Assert.Contains("at least 0", TilemapRules.AtlasCoords(x, y));

        [Fact]
        public void AlternativeMustBeZeroOrMore()
        {
            Assert.Null(TilemapRules.Alternative(0));
            Assert.Null(TilemapRules.Alternative(2));
            Assert.Contains("at least 0", TilemapRules.Alternative(-1));
        }
    }
}
