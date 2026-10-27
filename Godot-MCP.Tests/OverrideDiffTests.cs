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
        public void A2DTransformDiffersTheSameWay()
        {
            var template = new Transform2D(0, Vector2.Zero);
            var diff = OverrideDiff.Transform(new Transform2D(Mathf.DegToRad(45), new Vector2(2, 0)), template).Select(d => d.property).ToArray();
            Assert.Equal(new[] { "position", "rotation_degrees" }, diff);
        }

        [Fact]
        public void SameIgnoresFloatNoiseAndComparesObjectsByIdentity()
        {
            Assert.True(OverrideDiff.Same(1.0, 1.00001));
            Assert.False(OverrideDiff.Same(1.0, 1.1));
            Assert.True(OverrideDiff.Same(new Vector3(1, 2, 3), new Vector3(1, 2, 3.00001f)));
            Assert.True(OverrideDiff.Same(null, null));
            Assert.False(OverrideDiff.Same(null, 0L));
            Assert.True(OverrideDiff.Same("a", "a"));
            var o = new object();
            Assert.True(OverrideDiff.Same(o, o));
            Assert.False(OverrideDiff.Same(o, new object()));
            Assert.True(OverrideDiff.Same(new Color(1, 0, 0), new Color(1, 0, 0)));
        }
    }
}
