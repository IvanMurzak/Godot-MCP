/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Terrain3D;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Terrain3D.Tests
{
    /// <summary>
    /// Pure-managed specs for the CLASS-B presence gate — the structured result every editor tool returns when
    /// the Terrain3D addon is absent. No Godot binary required: the gate shape + hint are pure-managed, which is
    /// exactly why they (not the editor-only <c>AddonInterop</c>) are the unit-tested contract.
    /// </summary>
    public class AddonGateTests
    {
        [Fact]
        public void NotInstalled_HasGateFieldsAndHint()
        {
            var gate = AddonGate.NotInstalled();
            Assert.False(gate.Installed);
            Assert.Equal("Terrain3D", gate.Addon);
            Assert.Equal("Terrain3D", gate.MissingClass);
            Assert.False(string.IsNullOrWhiteSpace(gate.Hint));
            Assert.Contains("Terrain3D", gate.Hint);
            Assert.Contains("addons/terrain_3d", gate.Hint);
        }

        [Fact]
        public void NotInstalled_WithExplicitClass_CarriesIt()
        {
            var gate = AddonGate.NotInstalled("Terrain3DMaterial");
            Assert.False(gate.Installed);
            Assert.Equal("Terrain3DMaterial", gate.MissingClass);
        }

        [Fact]
        public void Ok_IsInstalledWithNoMissingClass()
        {
            var gate = AddonGate.Ok();
            Assert.True(gate.Installed);
            Assert.Equal("Terrain3D", gate.Addon);
            Assert.Null(gate.MissingClass);
            Assert.Equal(string.Empty, gate.Hint);
        }

        [Fact]
        public void Constants_AreStable()
        {
            // The gate probes a ClassDB class (GDExtension), NEVER the GDScript global-class list.
            Assert.Equal("Terrain3D", AddonGate.AddonName);
            Assert.Equal("Terrain3D", AddonGate.PresenceClass);
        }

        [Fact]
        public void Terrain3DNodeInfo_FromGate_MirrorsGate()
        {
            var info = Terrain3DNodeInfo.FromGate(AddonGate.NotInstalled());
            Assert.False(info.Installed);
            Assert.Equal("Terrain3D", info.Addon);
            Assert.Equal("Terrain3D", info.MissingClass);
            Assert.Contains("addons/terrain_3d", info.Hint);
            // Node fields stay at their defaults on the gate-failure path.
            Assert.Equal(string.Empty, info.NodePath);
            Assert.Equal(0, info.RegionSize);
        }
    }
}
