/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Terrain3D;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Terrain3D.Tests
{
    /// <summary>
    /// Unit spec for the PURE-MANAGED <c>terrain3d-defaults</c> tool — constructs the tool family and invokes
    /// the method directly (no Godot binary, no MCP server). The editor-only tools (<c>terrain3d-create</c>,
    /// <c>-set-data-directory</c>, <c>-set-region-size</c>, <c>-set-material</c>, <c>-get</c>) touch a live editor
    /// + the Terrain3D addon and are verified by the headless-Godot E2E leg instead; their tool-id constants are
    /// pinned here so the ids the dock / godot-cli / catalog reference cannot drift silently.
    /// </summary>
    public class Tool_Terrain3D_DefaultsTests
    {
        [Fact]
        public void Defaults_DefaultPreset_IsMedium1024()
        {
            var tool = new Tool_Terrain3D();
            var config = tool.Defaults();
            Assert.Equal("medium", config.Preset);
            Assert.Equal(1024, config.RegionSize);
        }

        [Theory]
        [InlineData("small", 256)]
        [InlineData("large", 2048)]
        public void Defaults_ParsesPreset(string preset, int expectedRegionSize)
        {
            var tool = new Tool_Terrain3D();
            Assert.Equal(expectedRegionSize, tool.Defaults(preset).RegionSize);
        }

        [Fact]
        public void Defaults_InvalidPreset_Throws()
        {
            var tool = new Tool_Terrain3D();
            Assert.Throws<ArgumentException>(() => tool.Defaults("colossal"));
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("terrain3d-defaults", Tool_Terrain3D.DefaultsToolId);
            Assert.Equal("terrain3d-create", Tool_Terrain3D.CreateToolId);
            Assert.Equal("terrain3d-set-data-directory", Tool_Terrain3D.SetDataDirectoryToolId);
            Assert.Equal("terrain3d-set-region-size", Tool_Terrain3D.SetRegionSizeToolId);
            Assert.Equal("terrain3d-set-material", Tool_Terrain3D.SetMaterialToolId);
            Assert.Equal("terrain3d-get", Tool_Terrain3D.GetToolId);
        }
    }
}
