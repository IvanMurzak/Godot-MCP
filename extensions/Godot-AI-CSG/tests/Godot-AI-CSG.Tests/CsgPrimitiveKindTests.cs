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
using com.IvanMurzak.Godot.MCP.CSG;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.CSG.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="CsgPrimitiveKinds"/> — the kind parsing the <c>csg-defaults</c> tool
    /// uses to pick the right starter config, plus the engine-class-name mapping that documents the
    /// upper-case-CSG engine naming. No Godot binary required.
    /// </summary>
    public class CsgPrimitiveKindTests
    {
        [Theory]
        [InlineData("box", CsgPrimitiveKind.Box)]
        [InlineData("BOX", CsgPrimitiveKind.Box)]
        [InlineData(" sphere ", CsgPrimitiveKind.Sphere)]
        [InlineData("cylinder", CsgPrimitiveKind.Cylinder)]
        [InlineData("Combiner", CsgPrimitiveKind.Combiner)]
        public void TryParse_KnownValues_ParseToExpected(string input, CsgPrimitiveKind expected)
        {
            Assert.True(CsgPrimitiveKinds.TryParse(input, out var kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("torus")]
        [InlineData("mesh")]
        public void TryParse_UnknownOrEmpty_ReturnsFalse(string? input)
        {
            Assert.False(CsgPrimitiveKinds.TryParse(input, out _));
        }

        [Fact]
        public void Parse_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => CsgPrimitiveKinds.Parse("polygon"));
        }

        [Fact]
        public void ToLabel_And_ToGodotClassName_AreStable()
        {
            Assert.Equal("Box", CsgPrimitiveKind.Box.ToLabel());
            Assert.Equal("Sphere", CsgPrimitiveKind.Sphere.ToLabel());
            Assert.Equal("Cylinder", CsgPrimitiveKind.Cylinder.ToLabel());
            Assert.Equal("Combiner", CsgPrimitiveKind.Combiner.ToLabel());

            // The engine spells the acronym CSG upper-case (vs the C# binding's CsgBox3D).
            Assert.Equal("CSGBox3D", CsgPrimitiveKind.Box.ToGodotClassName());
            Assert.Equal("CSGSphere3D", CsgPrimitiveKind.Sphere.ToGodotClassName());
            Assert.Equal("CSGCylinder3D", CsgPrimitiveKind.Cylinder.ToGodotClassName());
            Assert.Equal("CSGCombiner3D", CsgPrimitiveKind.Combiner.ToGodotClassName());
        }
    }
}
