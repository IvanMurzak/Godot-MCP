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
    /// <summary>Unit spec for the pure-managed Dialogic plain-text timeline-format helper.</summary>
    public class DialogicTimelineTextTests
    {
        [Fact]
        public void BuildTextLine_PlainText_NoSpeaker()
        {
            Assert.Equal("Hello world", DialogicTimelineText.BuildTextLine(null, "Hello world"));
            Assert.Equal("Hello world", DialogicTimelineText.BuildTextLine("  ", "Hello world"));
        }

        [Fact]
        public void BuildTextLine_WithSpeaker_PrependsName()
        {
            Assert.Equal("Hero: Hello world", DialogicTimelineText.BuildTextLine("Hero", "Hello world"));
        }

        [Fact]
        public void BuildTextLine_SpeakerWithSpaces_IsQuoted()
        {
            Assert.Equal("\"Dark Knight\": Hello", DialogicTimelineText.BuildTextLine("Dark Knight", "Hello"));
        }

        [Fact]
        public void BuildTextLine_EscapesColonsInBody()
        {
            // The body's colons are escaped (':' -> '\:'); the speaker separator colon is NOT escaped.
            Assert.Equal("Hero: Time\\: 10\\:00", DialogicTimelineText.BuildTextLine("Hero", "Time: 10:00"));
            Assert.Equal("Note\\: hi", DialogicTimelineText.BuildTextLine(null, "Note: hi"));
        }

        [Fact]
        public void BuildTextLine_EmptyBody_UsesPlaceholder()
        {
            Assert.Equal(DialogicTimelineText.EmptyEventBody, DialogicTimelineText.BuildTextLine(null, ""));
            Assert.Equal("Hero: " + DialogicTimelineText.EmptyEventBody, DialogicTimelineText.BuildTextLine("Hero", "   "));
        }

        [Fact]
        public void Append_ToEmpty_ReturnsLineAlone()
        {
            Assert.Equal("line", DialogicTimelineText.Append(null, "line"));
            Assert.Equal("line", DialogicTimelineText.Append("", "line"));
        }

        [Fact]
        public void Append_ToExisting_JoinsWithNewline()
        {
            Assert.Equal("a\nb", DialogicTimelineText.Append("a", "b"));
            Assert.Equal("a\nb", DialogicTimelineText.Append("a\n", "b"));
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("one", 1)]
        [InlineData("one\ntwo", 2)]
        [InlineData("one\n\n  \ntwo\n", 2)]
        public void CountEvents_CountsNonEmptyLines(string? text, int expected)
        {
            Assert.Equal(expected, DialogicTimelineText.CountEvents(text));
        }
    }
}
