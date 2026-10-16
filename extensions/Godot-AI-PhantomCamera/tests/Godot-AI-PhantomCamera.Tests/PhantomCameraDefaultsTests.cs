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
    /// Pure-managed specs for <see cref="PhantomCameraDefaults"/> — the recommended starter config the
    /// <c>phantomcamera-defaults</c> tool returns. This is the always-runnable, addon-independent core, so it
    /// is the never-vacuous heart of both the unit-test path and the no-scene e2e entry.
    /// </summary>
    public class PhantomCameraDefaultsTests
    {
        [Fact]
        public void For_Default_Is3DStarterConfig()
        {
            var info = PhantomCameraDefaults.For();

            Assert.True(info.Installed);
            Assert.Equal("PhantomCamera3D", info.TypeName);
            Assert.Equal(string.Empty, info.NodePath);          // not bound to a node
            Assert.Equal(PhantomCameraDefaults.DefaultPriority, info.Priority);
            Assert.Equal("Simple", info.FollowMode);
            Assert.Equal("Simple", info.LookAtMode);
            Assert.True(info.FollowDamping);
            Assert.True(info.LookAtDamping);
        }

        [Theory]
        [InlineData("2D", "PhantomCamera2D")]
        [InlineData("2", "PhantomCamera2D")]
        [InlineData("two", "PhantomCamera2D")]
        [InlineData("3D", "PhantomCamera3D")]
        [InlineData(null, "PhantomCamera3D")]
        public void For_Dimension_SelectsReportedTypeName(string? dimension, string expectedType)
        {
            Assert.Equal(expectedType, PhantomCameraDefaults.For(dimension).TypeName);
        }

        [Fact]
        public void DefaultConstants_AreStable()
        {
            Assert.Equal(10, PhantomCameraDefaults.DefaultPriority);
            Assert.Equal(PhantomFollowMode.Simple, PhantomCameraDefaults.DefaultFollowMode);
            Assert.Equal(PhantomLookAtMode.Simple, PhantomCameraDefaults.DefaultLookAtMode);
        }
    }
}
