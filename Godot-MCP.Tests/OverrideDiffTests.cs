/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Godot;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>The pure part of <c>overrides</c> (how a scene instance differs from its scene file); the live smoke reads real instances.</summary>
    public class OverrideDiffTests
    {
        [Fact]
        public void ATransformDiffersByPositionRotationAndScale()
        {
            var template = new Transform3D(Basis.Identity, new Vector3(0, 1.5f, 0));
            var moved = new Transform3D(Basis.Identity.Scaled(new Vector3(1.2f, 1.2f, 1.2f)), new Vector3(3, 1.5f, 0));
            var diff = OverrideDiff.Transform(moved, template).ToList();
            Assert.Equal(new[] { "position", "scale" }, diff.Select(d => d.property).ToArray());
            Assert.Equal(new Vector3(1.2f, 1.2f, 1.2f), (Vector3)diff[1].live);
            Assert.Equal(new Vector3(1, 1, 1), (Vector3)diff[1].template);

            var turned = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(90)), template.Origin);
            var rot = OverrideDiff.Transform(turned, template).Single();
            Assert.Equal("rotation_degrees", rot.property);
            Assert.Equal(90, ((Vector3)rot.live).Y, 3);
            Assert.Empty(OverrideDiff.Transform(template, template));
        }

        [Fact]
        public void ARotatedAndScaledTransformKeepsItsAngle()
        {
            var template = new Transform3D(Basis.Identity, Vector3.Zero);
            var basis = new Basis(Vector3.Right, Mathf.DegToRad(60)).Scaled(new Vector3(1.2f, 1.2f, 1.2f));
            var rot = OverrideDiff.Transform(new Transform3D(basis, Vector3.Zero), template).Single(d => d.property == "rotation_degrees");
            Assert.Equal(60, ((Vector3)rot.live).X, 2);
        }

        [Fact]
        public void ApproxSameIgnoresFloatNoiseAndLeavesOtherKindsToTheCaller()
        {
            Assert.True(OverrideDiff.ApproxSame(1.0, 1.00001));
            Assert.False(OverrideDiff.ApproxSame(1.0, 1.1));
            Assert.True(OverrideDiff.ApproxSame(new Vector3(1, 2, 3), new Vector3(1, 2, 3.00001f)));
            Assert.True(OverrideDiff.ApproxSame(new Color(1, 0, 0), new Color(1, 0, 0)));
            Assert.Null(OverrideDiff.ApproxSame(null, null));
            Assert.Null(OverrideDiff.ApproxSame("a", "a"));
            Assert.Null(OverrideDiff.ApproxSame(1.0, null));
        }
    }
}
