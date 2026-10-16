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
using com.IvanMurzak.Godot.MCP.PhantomCamera;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera.Tests
{
    /// <summary>
    /// Class-B contract specs: there are NO compile-time Phantom Camera enum types, so these pure-managed
    /// constants ARE the contract. They are pinned here against the addon's real values (ramokz/phantom-camera
    /// v0.11.0.2). If an upstream rename/renumber happens, these tests turn red — exactly the early-warning the
    /// Class-B guide demands.
    /// </summary>
    public class PhantomCameraEnumsTests
    {
        [Theory]
        [InlineData(PhantomFollowMode.None, 0)]
        [InlineData(PhantomFollowMode.Glued, 1)]
        [InlineData(PhantomFollowMode.Simple, 2)]
        [InlineData(PhantomFollowMode.Group, 3)]
        [InlineData(PhantomFollowMode.Path, 4)]
        [InlineData(PhantomFollowMode.Framed, 5)]
        [InlineData(PhantomFollowMode.ThirdPerson, 6)]
        public void FollowMode_IntValues_MatchAddon(PhantomFollowMode mode, int expected)
        {
            Assert.Equal(expected, mode.ToInt());
        }

        [Theory]
        [InlineData(PhantomLookAtMode.None, 0)]
        [InlineData(PhantomLookAtMode.Mimic, 1)]
        [InlineData(PhantomLookAtMode.Simple, 2)]
        [InlineData(PhantomLookAtMode.Group, 3)]
        public void LookAtMode_IntValues_MatchAddon(PhantomLookAtMode mode, int expected)
        {
            Assert.Equal(expected, mode.ToInt());
        }

        [Fact]
        public void ClassNames_AreTheAddonSnakeAndPascalContract()
        {
            Assert.Equal("PhantomCamera3D", PhantomCameraNames.PhantomCamera3D);
            Assert.Equal("PhantomCamera2D", PhantomCameraNames.PhantomCamera2D);
            Assert.Equal("PhantomCameraHost", PhantomCameraNames.PhantomCameraHost);
        }

        [Fact]
        public void MemberNames_AreGdScriptSnakeCase()
        {
            // GDScript snake_case — NOT C# PascalCase. The #1 Class-B reflex error.
            Assert.Equal("priority", PhantomCameraNames.Priority);
            Assert.Equal("follow_mode", PhantomCameraNames.FollowMode);
            Assert.Equal("follow_target", PhantomCameraNames.FollowTarget);
            Assert.Equal("look_at_mode", PhantomCameraNames.LookAtMode);
            Assert.Equal("look_at_target", PhantomCameraNames.LookAtTarget);
            Assert.Equal("follow_damping", PhantomCameraNames.FollowDamping);
            Assert.Equal("follow_damping_value", PhantomCameraNames.FollowDampingValue);
            Assert.Equal("look_at_damping", PhantomCameraNames.LookAtDamping);
            Assert.Equal("look_at_damping_value", PhantomCameraNames.LookAtDampingValue);
        }

        [Theory]
        [InlineData("Simple", PhantomFollowMode.Simple)]
        [InlineData("simple", PhantomFollowMode.Simple)]
        [InlineData("  GLUED ", PhantomFollowMode.Glued)]
        [InlineData("third_person", PhantomFollowMode.ThirdPerson)]
        [InlineData("ThirdPerson", PhantomFollowMode.ThirdPerson)]
        [InlineData("third-person", PhantomFollowMode.ThirdPerson)]
        [InlineData("Third Person", PhantomFollowMode.ThirdPerson)]
        [InlineData("6", PhantomFollowMode.ThirdPerson)]
        [InlineData("0", PhantomFollowMode.None)]
        public void ParseFollow_AcceptsNamesAndInts(string input, PhantomFollowMode expected)
        {
            Assert.Equal(expected, PhantomCameraModes.ParseFollow(input));
        }

        [Theory]
        [InlineData("Mimic", PhantomLookAtMode.Mimic)]
        [InlineData("group", PhantomLookAtMode.Group)]
        [InlineData("2", PhantomLookAtMode.Simple)]
        public void ParseLookAt_AcceptsNamesAndInts(string input, PhantomLookAtMode expected)
        {
            Assert.Equal(expected, PhantomCameraModes.ParseLookAt(input));
        }

        [Fact]
        public void Parse_InvalidValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => PhantomCameraModes.ParseFollow("orbit"));
            Assert.Throws<ArgumentException>(() => PhantomCameraModes.ParseLookAt("askew"));
        }

        [Theory]
        [InlineData(PhantomFollowMode.ThirdPerson, "ThirdPerson")]
        [InlineData(PhantomFollowMode.Simple, "Simple")]
        public void FollowMode_ToLabel_IsTheEnumName(PhantomFollowMode mode, string expected)
        {
            Assert.Equal(expected, mode.ToLabel());
        }
    }
}
