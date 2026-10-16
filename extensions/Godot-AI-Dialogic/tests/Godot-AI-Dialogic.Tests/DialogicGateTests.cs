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
    /// Unit spec for the Class-B presence-gate shape: the pure-managed <see cref="AddonGateResult"/> + the
    /// <see cref="DialogicResourceInfo.NotInstalled"/> mapping (the editor tools' graceful "addon missing" path).
    /// </summary>
    public class DialogicGateTests
    {
        [Fact]
        public void AddonGateResult_NotInstalled_CarriesFields()
        {
            var gate = AddonGateResult.NotInstalled("Dialogic", "DialogicTimeline", "install me");
            Assert.False(gate.Installed);
            Assert.Equal("Dialogic", gate.Addon);
            Assert.Equal("DialogicTimeline", gate.MissingClass);
            Assert.Equal("install me", gate.Hint);
        }

        [Fact]
        public void AddonGateResult_Ok_IsInstalledWithNoMissingClass()
        {
            var gate = AddonGateResult.Ok("Dialogic");
            Assert.True(gate.Installed);
            Assert.Equal("Dialogic", gate.Addon);
            Assert.Null(gate.MissingClass);
            Assert.Equal(string.Empty, gate.Hint);
        }

        [Fact]
        public void DialogicResourceInfo_NotInstalled_MapsGate()
        {
            var gate = AddonGateResult.NotInstalled(DialogicAddon.AddonName, DialogicAddon.PresenceClass, DialogicAddon.InstallHint);
            var info = DialogicResourceInfo.NotInstalled(gate);

            Assert.False(info.Installed);
            Assert.Equal(DialogicAddon.AddonName, info.Addon);
            Assert.Equal(DialogicAddon.PresenceClass, info.MissingClass);
            Assert.Equal(DialogicAddon.InstallHint, info.Hint);
        }

        [Fact]
        public void DialogicAddon_Constants_AreTheContract()
        {
            // The addon's class/member names are referenced by STRING only, so pin them.
            Assert.Equal("Dialogic", DialogicAddon.AddonName);
            Assert.Equal("DialogicTimeline", DialogicAddon.PresenceClass);
            Assert.Equal("DialogicTimeline", DialogicAddon.TimelineClass);
            Assert.Equal("DialogicCharacter", DialogicAddon.CharacterClass);
            Assert.Equal("dtl", DialogicAddon.TimelineExtension);
            Assert.Equal("dch", DialogicAddon.CharacterExtension);
            Assert.Equal("display_name", DialogicAddon.DisplayNameMember);
            Assert.Equal("color", DialogicAddon.ColorMember);
            Assert.Equal("description", DialogicAddon.DescriptionMember);
            Assert.Equal("from_text", DialogicAddon.FromTextMethod);
            Assert.Equal("as_text", DialogicAddon.AsTextMethod);
            Assert.Equal("timeline_not_saved", DialogicAddon.TimelineNotSavedMeta);
        }
    }
}
