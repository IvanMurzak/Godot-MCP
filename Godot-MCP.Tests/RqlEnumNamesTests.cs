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
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// <see cref="GodotEnumNames"/>: an enum-hinted int property (grow_horizontal) or a class-enum method argument
    /// (Control.LayoutPreset) takes the name an agent writes — the inspector's, the C++ / C# constant, or a short form.
    /// </summary>
    public class RqlEnumNamesTests
    {
        // grow_horizontal's real hint labels; its setter's enum constants (Control.GrowDirection) differ from them.
        private static readonly List<(string, long)> Grow = GodotEnumNames.ParseHint("Left,Right,Both");

        private static readonly List<(string, long)> GrowConstants = new List<(string, long)>
        {
            ("Left", 0), ("Right", 1), ("Both", 2), ("GROW_DIRECTION_BEGIN", 0), ("GROW_DIRECTION_END", 1), ("GROW_DIRECTION_BOTH", 2),
        };

        private static readonly List<(string, long)> Preset = new List<(string, long)>
        {
            ("PRESET_TOP_LEFT", 0), ("PRESET_TOP_RIGHT", 1), ("PRESET_CENTER_LEFT", 4), ("PRESET_CENTER", 8),
            ("PRESET_LEFT_WIDE", 9), ("PRESET_FULL_RECT", 15),
        };

        [Fact]
        public void ParsesImplicitAndExplicitValues()
        {
            Assert.Equal(new List<(string, long)> { ("Left", 0), ("Right", 1), ("Both", 2) }, Grow);
            Assert.Equal(new List<(string, long)> { ("Off", 0), ("On", 5), ("Auto", 6), ("Max", 10) }, GodotEnumNames.ParseHint("Off,On:5,Auto, Max:10"));
            Assert.Empty(GodotEnumNames.ParseHint(""));
        }

        [Theory]
        [InlineData("Both", 2)]
        [InlineData("both", 2)]
        [InlineData("RIGHT", 1)]
        [InlineData("GROW_DIRECTION_BOTH", 2)] // ends with the label's word
        public void MatchesHintNames(string text, long expected)
        {
            Assert.True(GodotEnumNames.TryMatch(Grow, text, out var value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("GROW_DIRECTION_BEGIN", 0)]
        [InlineData("GrowDirectionEnd", 1)]
        [InlineData("Begin", 0)]
        [InlineData("Left", 0)]
        public void MatchesSetterConstantsBesideTheLabels(string text, long expected)
        {
            Assert.True(GodotEnumNames.TryMatch(GrowConstants, text, out var value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("GROW_DIRECTION_BEGIN")] // the labels alone do not know the constant (Godot 4.3 has no setter lookup)
        [InlineData("h")]
        [InlineData("Rend")]
        [InlineData("oth")]
        public void SuffixesMatchWholeWordsOnly(string text) => Assert.False(GodotEnumNames.TryMatch(Grow, text, out _));

        [Theory]
        [InlineData("PRESET_FULL_RECT", 15)]
        [InlineData("PresetFullRect", 15)]
        [InlineData("FullRect", 15)]
        [InlineData("full_rect", 15)]
        [InlineData("Center", 8)]
        [InlineData("LeftWide", 9)]
        public void MatchesClassConstants(string text, long expected)
        {
            Assert.True(GodotEnumNames.TryMatch(Preset, text, out var value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("Sideways")]
        [InlineData("")]
        [InlineData("__")]
        [InlineData("Left")] // ends PRESET_TOP_LEFT and PRESET_CENTER_LEFT: ambiguous, refused rather than guessed
        public void RefusesUnknownOrAmbiguous(string text)
        {
            var options = text == "Left" ? Preset : Grow;
            Assert.False(GodotEnumNames.TryMatch(options, text, out _));
        }

        [Fact]
        public void DescribesTheOptions() =>
            Assert.Equal("one of: Left, Right, Both (or its number)", GodotEnumNames.Describe(Grow));
    }
}
