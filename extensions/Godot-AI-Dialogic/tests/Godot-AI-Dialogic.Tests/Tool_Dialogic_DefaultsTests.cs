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
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>dialogic-defaults</c> tool — constructs the tool family and invokes the
    /// method directly (no Godot binary, no MCP server). The editor-only tools (<c>dialogic-timeline-create</c>,
    /// <c>-character-create</c>, <c>-timeline-add-text</c>, <c>-get</c>) touch a live editor + the addon and are
    /// verified by the headless-Godot E2E; their tool-id constants are pinned here so the ids the dock /
    /// godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_Dialogic_DefaultsTests
    {
        [Fact]
        public void Defaults_NoFilter_ReturnsBothInstalled()
        {
            var tool = new Tool_Dialogic();
            var info = tool.Defaults();

            Assert.True(info.Installed);
            Assert.Equal(DialogicResourceKind.Defaults, info.ResourceKind);
            Assert.Equal(DialogicDefaults.DefaultCharacterName, info.DisplayName);
            Assert.Equal(DialogicDefaults.DefaultCharacterColor, info.Color);
            Assert.True(info.EventCount >= 1);
            Assert.NotEqual(string.Empty, info.TimelineText);
        }

        [Fact]
        public void Defaults_TimelineFilter_OmitsCharacterFields()
        {
            var info = new Tool_Dialogic().Defaults("timeline");
            Assert.NotEqual(string.Empty, info.TimelineText);
            Assert.Equal(string.Empty, info.DisplayName);
            Assert.Equal(string.Empty, info.Color);
        }

        [Fact]
        public void Defaults_CharacterFilter_OmitsTimeline()
        {
            var info = new Tool_Dialogic().Defaults("character");
            Assert.Equal(string.Empty, info.TimelineText);
            Assert.Equal(0, info.EventCount);
            Assert.Equal(DialogicDefaults.DefaultCharacterName, info.DisplayName);
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("dialogic-defaults", Tool_Dialogic.DefaultsToolId);
            Assert.Equal("dialogic-timeline-create", Tool_Dialogic.TimelineCreateToolId);
            Assert.Equal("dialogic-character-create", Tool_Dialogic.CharacterCreateToolId);
            Assert.Equal("dialogic-timeline-add-text", Tool_Dialogic.TimelineAddTextToolId);
            Assert.Equal("dialogic-get", Tool_Dialogic.GetToolId);
        }
    }
}
