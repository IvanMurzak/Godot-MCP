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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera.Tests
{
    public class PhantomCameraRulesTests
    {
        [Theory]
        [InlineData("None", PhantomFollowMode.None)]
        [InlineData("simple", PhantomFollowMode.Simple)]
        [InlineData(" GLUED ", PhantomFollowMode.Glued)]
        [InlineData("Framed", PhantomFollowMode.Framed)]
        [InlineData("Third Person", PhantomFollowMode.ThirdPerson)]
        [InlineData("third_person", PhantomFollowMode.ThirdPerson)]
        [InlineData("ThirdPerson", PhantomFollowMode.ThirdPerson)]
        public void FollowModesParseByName(string name, PhantomFollowMode expected)
        {
            Assert.Null(PhantomCameraRules.FollowMode(name, is2D: false, out var mode));
            Assert.Equal(expected, mode);
        }

        [Theory]
        [InlineData("Sideways")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("2")] // names only: the int values belong to props["follow_mode"]
        public void UnknownFollowModesAreRejected(string? name) =>
            Assert.StartsWith("unknown follow mode", PhantomCameraRules.FollowMode(name, is2D: false, out _));

        [Fact]
        public void ThirdPersonIsA3DMode()
        {
            Assert.Contains("PhantomCamera3D follow mode", PhantomCameraRules.FollowMode("ThirdPerson", is2D: true, out _));
            Assert.Null(PhantomCameraRules.FollowMode("Framed", is2D: true, out _));
            Assert.DoesNotContain("ThirdPerson", PhantomCameraRules.FollowMode("x", is2D: true, out _));
        }

        [Theory]
        [InlineData("Mimic", PhantomLookAtMode.Mimic)]
        [InlineData("group", PhantomLookAtMode.Group)]
        public void LookAtModesParseByName(string name, PhantomLookAtMode expected)
        {
            Assert.Null(PhantomCameraRules.LookAtMode(name, is2D: false, out var mode));
            Assert.Equal(expected, mode);
        }

        [Fact]
        public void LookAtIsRejectedForUnknownModesAnd2DCameras()
        {
            Assert.StartsWith("unknown look-at mode", PhantomCameraRules.LookAtMode("Glued", is2D: false, out _));
            Assert.Contains("no look-at mode", PhantomCameraRules.LookAtMode("Simple", is2D: true, out _));
        }

        [Theory]
        [InlineData(PhantomFollowMode.Glued)]
        [InlineData(PhantomFollowMode.Simple)]
        [InlineData(PhantomFollowMode.Path)]
        [InlineData(PhantomFollowMode.Framed)]
        [InlineData(PhantomFollowMode.ThirdPerson)]
        public void SingleTargetModesTakeATarget(PhantomFollowMode mode) => Assert.Null(PhantomCameraRules.FollowTarget(mode));

        [Fact]
        public void NoneAndGroupRejectASingleTarget()
        {
            Assert.Contains("follows nothing", PhantomCameraRules.FollowTarget(PhantomFollowMode.None));
            Assert.Contains("follow_targets", PhantomCameraRules.FollowTarget(PhantomFollowMode.Group));
            Assert.Contains("looks at nothing", PhantomCameraRules.LookAtTarget(PhantomLookAtMode.None));
            Assert.Contains("look_at_targets", PhantomCameraRules.LookAtTarget(PhantomLookAtMode.Group));
            Assert.Null(PhantomCameraRules.LookAtTarget(PhantomLookAtMode.Mimic));
        }

        [Fact]
        public void TargetsMustMatchTheCameraDimension()
        {
            Assert.Null(PhantomCameraRules.TargetType(false, true, "Player", "Node3D"));
            Assert.Equal("target 'Hud' is a Control; a PhantomCamera3D needs a Node3D", PhantomCameraRules.TargetType(false, false, "Hud", "Control"));
            Assert.Contains("PhantomCamera2D needs a Node2D", PhantomCameraRules.TargetType(true, false, "Player", "Node3D"));
        }

        [Theory]
        [InlineData(0, "None")]
        [InlineData(2, "Simple")]
        [InlineData(6, "ThirdPerson")]
        [InlineData(9, "9")]
        public void FollowLabels(int value, string label) => Assert.Equal(label, PhantomCameraModes.Label<PhantomFollowMode>(value));

        [Theory]
        [InlineData(1, "Mimic")]
        [InlineData(3, "Group")]
        [InlineData(-1, "-1")]
        public void LookAtLabels(int value, string label) => Assert.Equal(label, PhantomCameraModes.Label<PhantomLookAtMode>(value));

        [Fact]
        public void TheInstallHintNamesTheAddonAndItsAutoload()
        {
            Assert.Contains("ramokz/phantom-camera v0.11.0.2", PhantomCameraNames.InstallHint);
            Assert.Contains("PhantomCameraManager", PhantomCameraNames.InstallHint);
        }
    }
}
