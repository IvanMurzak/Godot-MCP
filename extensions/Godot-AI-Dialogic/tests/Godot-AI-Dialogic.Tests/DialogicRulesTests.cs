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

namespace com.IvanMurzak.Godot.MCP.Dialogic.Tests
{
    public class DialogicRulesTests
    {
        [Theory]
        [InlineData("res://e2e_intro.dtl", "res://e2e_intro.dtl")]
        [InlineData("res://e2e_intro", "res://e2e_intro.dtl")]
        [InlineData("dialogue/intro", "res://dialogue/intro.dtl")]
        [InlineData("/dialogue/intro.DTL", "res://dialogue/intro.DTL")]
        [InlineData("dialogue\\intro.dtl", "res://dialogue/intro.dtl")]
        public void TimelinePathsAreNormalized(string path, string expected)
        {
            Assert.Null(DialogicRules.ResourcePath(path, "dtl", out var normalized));
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(null, "is required")]
        [InlineData("  ", "is required")]
        [InlineData("res://hero.dtl", "must be a .dch file")]
        [InlineData("res://characters/", "names a folder")]
        [InlineData("user://hero.dch", "not a res:// path")]
        [InlineData("res://../outside/hero.dch", "'..' segment")]
        [InlineData("characters/../../hero", "'..' segment")]
        public void BadCharacterPathsAreRejected(string? path, string reason) =>
            Assert.Contains(reason, DialogicRules.ResourcePath(path, "dch", out _));

        [Theory]
        [InlineData(null, "3aa0ff")]
        [InlineData("", "3aa0ff")]
        [InlineData("#E91E63", "e91e63")]
        [InlineData("e91e63", "e91e63")]
        [InlineData("#e91e63ff", "e91e63")]
        public void ColorsAreHexOrTheDefault(string? value, string hex)
        {
            Assert.Null(DialogicRules.Color(value, out var normalized));
            Assert.Equal(hex, normalized);
        }

        [Theory]
        [InlineData("red")]
        [InlineData("#zzz")]
        [InlineData("#12345")]
        public void OtherColorsAreRejected(string value) =>
            Assert.StartsWith("color must be a hex color", DialogicRules.Color(value, out _));

        [Fact]
        public void ACharacterNeedsADisplayName()
        {
            Assert.Null(DialogicRules.DisplayName("Hero"));
            Assert.NotNull(DialogicRules.DisplayName(" "));
            Assert.NotNull(DialogicRules.DisplayName(null));
        }

        [Fact]
        public void TheStarterTimelineHasTwoLines()
        {
            Assert.Equal(2, DialogicTimelineText.CountEvents(DialogicNames.StarterTimelineText()));
            Assert.StartsWith("Narrator: ", DialogicNames.StarterTimelineText());
        }

        [Fact]
        public void TheInstallHintNamesTheAddonAndItsAutoload()
        {
            Assert.Contains("dialogic-godot/dialogic", DialogicNames.InstallHint);
            Assert.Contains("Dialogic autoload", DialogicNames.InstallHint);
        }
    }
}
