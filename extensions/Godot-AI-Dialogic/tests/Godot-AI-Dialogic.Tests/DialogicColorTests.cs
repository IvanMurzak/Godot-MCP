/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Dialogic;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Dialogic.Tests
{
    /// <summary>Unit spec for the pure-managed color-normalization helper.</summary>
    public class DialogicColorTests
    {
        [Theory]
        [InlineData("#e91e63", "e91e63")]
        [InlineData("e91e63", "e91e63")]
        [InlineData("#E91E63", "e91e63")]
        [InlineData("#e91e63ff", "e91e63")] // alpha dropped
        public void NormalizeHex_AcceptsValid(string input, string expected)
        {
            Assert.Equal(expected, DialogicColor.NormalizeHex(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("nothex")]
        [InlineData("#12")]
        [InlineData("#1234567")] // 7 digits, neither 6 nor 8
        [InlineData("#zzzzzz")]
        public void NormalizeHex_FallsBackToDefault(string? input)
        {
            Assert.Equal(DialogicColor.Default, DialogicColor.NormalizeHex(input));
        }

        [Fact]
        public void ToDisplay_PrefixesHash()
        {
            Assert.Equal("#e91e63", DialogicColor.ToDisplay("e91e63"));
        }
    }
}
