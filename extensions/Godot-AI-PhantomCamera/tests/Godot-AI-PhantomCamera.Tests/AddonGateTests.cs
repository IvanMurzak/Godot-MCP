/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.PhantomCamera;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera.Tests
{
    /// <summary>
    /// Specs for the Class-B presence gate (the structured installed:false path every editor tool returns
    /// when the Phantom Camera addon is absent). Pins the gate shape + hint so the LLM-facing "what to install"
    /// message never silently regresses.
    /// </summary>
    public class AddonGateTests
    {
        [Fact]
        public void NotInstalled_ProducesStructuredResult()
        {
            var gate = AddonGate.NotInstalled();
            Assert.False(gate.Installed);
            Assert.Equal("Phantom Camera", gate.Addon);
            Assert.Equal("PhantomCamera3D", gate.MissingClass);
            Assert.Contains("Asset Library", gate.Hint);
            Assert.Contains("Project Settings", gate.Hint);
        }

        [Fact]
        public void Ok_ProducesInstalledResult()
        {
            var gate = AddonGate.Ok();
            Assert.True(gate.Installed);
            Assert.Equal("Phantom Camera", gate.Addon);
            Assert.Null(gate.MissingClass);
            Assert.Equal(string.Empty, gate.Hint);
        }

        [Fact]
        public void PresenceClass_IsTheProbeClass()
        {
            Assert.Equal("PhantomCamera3D", AddonGate.PresenceClass);
            Assert.Equal(PhantomCameraNames.PhantomCamera3D, AddonGate.PresenceClass);
        }

        [Fact]
        public void PhantomCameraInfo_NotInstalled_MirrorsTheGate()
        {
            // The Option-A presence gate embedded on the tool's own result record (what the e2e driver sees).
            var info = PhantomCameraInfo.NotInstalled();
            Assert.False(info.Installed);
            Assert.Equal("Phantom Camera", info.Addon);
            Assert.Equal("PhantomCamera3D", info.MissingClass);
            Assert.Equal(AddonGate.InstallHint, info.Hint);
        }

        [Fact]
        public void PhantomCameraInfo_DefaultsToInstalled()
        {
            // A freshly-built result (the happy path) reports installed:true so a tool that populates real
            // fields never accidentally looks like the not-installed gate.
            var info = new PhantomCameraInfo();
            Assert.True(info.Installed);
        }
    }
}
