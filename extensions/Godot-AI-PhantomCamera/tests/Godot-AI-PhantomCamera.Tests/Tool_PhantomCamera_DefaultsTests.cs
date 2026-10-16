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
    /// Unit spec for the PURE-MANAGED <c>phantomcamera-defaults</c> tool — constructs the tool family and
    /// invokes the method directly (no Godot binary, no MCP server). The editor-only tools
    /// (<c>phantomcamera-host-create</c>, <c>-create</c>, <c>-set-follow</c>, <c>-set-look-at</c>,
    /// <c>-set-priority</c>, <c>-get</c>) touch a live editor + the addon and are verified by the headless-Godot
    /// E2E leg instead; their tool-id constants are pinned here so the ids the dock / godot-cli / catalog
    /// reference cannot drift silently.
    /// </summary>
    public class Tool_PhantomCamera_DefaultsTests
    {
        [Fact]
        public void Defaults_ReturnsRecommendedStarterConfig()
        {
            var tool = new Tool_PhantomCamera();
            var info = tool.Defaults();

            Assert.True(info.Installed);
            Assert.Equal("PhantomCamera3D", info.TypeName);
            Assert.Equal(PhantomCameraDefaults.DefaultPriority, info.Priority);
            Assert.Equal("Simple", info.FollowMode);
        }

        [Theory]
        [InlineData("2D", "PhantomCamera2D")]
        [InlineData("3D", "PhantomCamera3D")]
        public void Defaults_DimensionSelectsType(string dimension, string expectedType)
        {
            var tool = new Tool_PhantomCamera();
            Assert.Equal(expectedType, tool.Defaults(dimension).TypeName);
        }

        [Fact]
        public void ToolIds_AreStable()
        {
            Assert.Equal("phantomcamera-defaults", Tool_PhantomCamera.DefaultsToolId);
            Assert.Equal("phantomcamera-host-create", Tool_PhantomCamera.HostCreateToolId);
            Assert.Equal("phantomcamera-create", Tool_PhantomCamera.CreateToolId);
            Assert.Equal("phantomcamera-set-follow", Tool_PhantomCamera.SetFollowToolId);
            Assert.Equal("phantomcamera-set-look-at", Tool_PhantomCamera.SetLookAtToolId);
            Assert.Equal("phantomcamera-set-priority", Tool_PhantomCamera.SetPriorityToolId);
            Assert.Equal("phantomcamera-get", Tool_PhantomCamera.GetToolId);
        }
    }
}
