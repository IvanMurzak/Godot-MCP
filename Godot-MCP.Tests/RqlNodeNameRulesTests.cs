/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// <see cref="NodeNameRules"/>: a rename Godot would silently change (an invalid character, a sibling's name) is refused, so
    /// the rql-mutate receipt never shows a name the node does not have.
    /// </summary>
    public class RqlNodeNameRulesTests
    {
        private static readonly System.Func<string, bool> Siblings = name => name == "Player" || name == "Enemy";

        [Theory]
        [InlineData("Boss")]
        [InlineData("Узел_🎮")]
        [InlineData("with space")]
        [InlineData("player")] // names are case-sensitive in Godot
        public void ValidNamesPass(string name) => Assert.Null(NodeNameRules.Problem(name, Siblings));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void EmptyNamesFail(string? name) => Assert.Equal("a node name cannot be empty", NodeNameRules.Problem(name, Siblings));

        [Theory]
        [InlineData("x.y", "'.'")]
        [InlineData("a:b", "':'")]
        [InlineData("a@b", "'@'")]
        [InlineData("a/b", "'/'")]
        [InlineData("a\"b", "'\"'")]
        [InlineData("%Unique", "'%'")]
        [InlineData("x.y:z@", "'.' ':' '@'")]
        public void InvalidCharactersFailNamingThem(string name, string named)
        {
            var problem = NodeNameRules.Problem(name, Siblings);
            Assert.NotNull(problem);
            Assert.Contains("cannot contain " + named, problem);
        }

        [Fact]
        public void ASiblingsNameFails()
        {
            var problem = NodeNameRules.Problem("Enemy", Siblings);
            Assert.Equal("a sibling is already named 'Enemy' (node names are unique among siblings)", problem);
        }
    }
}
