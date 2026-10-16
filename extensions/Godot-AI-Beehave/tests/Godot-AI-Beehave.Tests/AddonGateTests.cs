/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Beehave;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    /// <summary>
    /// Pure-managed specs for the Class-B presence gate — the structured <c>Installed = false</c> shape every
    /// editor tool returns when Beehave is not installed (instead of crashing). Pins the shape + the hint text
    /// the LLM sees. No Godot binary required.
    /// </summary>
    public class AddonGateTests
    {
        [Fact]
        public void NotInstalled_HasGateShapeAndHint()
        {
            var gate = AddonGate.NotInstalled("BeehaveTree");
            Assert.False(gate.Installed);
            Assert.Equal("Beehave", gate.Addon);
            Assert.Equal("BeehaveTree", gate.MissingClass);
            Assert.Contains("Beehave", gate.Hint);
            Assert.Contains("Plugins", gate.Hint);
        }

        [Fact]
        public void Ok_IsInstalledWithNoMissingClass()
        {
            var gate = AddonGate.Ok();
            Assert.True(gate.Installed);
            Assert.Equal("Beehave", gate.Addon);
            Assert.Null(gate.MissingClass);
            Assert.Equal(string.Empty, gate.Hint);
        }

        [Fact]
        public void BeehaveNodeInfo_NotInstalled_EmbedsGateFields()
        {
            var info = BeehaveNodeInfo.NotInstalled("BeehaveTree");
            Assert.False(info.Installed);
            Assert.Equal("Beehave", info.Addon);
            Assert.Equal("BeehaveTree", info.MissingClass);
            Assert.Equal(AddonGate.InstallHint, info.Hint);
            // The node payload is left unset on a gate failure.
            Assert.Equal(string.Empty, info.NodePath);
            Assert.Equal(string.Empty, info.ClassName);
            Assert.Empty(info.Children);
        }
    }
}
