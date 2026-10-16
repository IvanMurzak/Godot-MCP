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
using com.IvanMurzak.Godot.MCP.Particles;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Particles.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="ParticlesDimensions"/> — the dimension parsing every particle tool
    /// uses to map an LLM-supplied '2D'/'3D' string onto the right Godot node type. No Godot binary required.
    /// </summary>
    public class ParticlesDimensionTests
    {
        [Theory]
        [InlineData("2D", ParticlesDimension.Two)]
        [InlineData("2d", ParticlesDimension.Two)]
        [InlineData(" 2 ", ParticlesDimension.Two)]
        [InlineData("two", ParticlesDimension.Two)]
        [InlineData("3D", ParticlesDimension.Three)]
        [InlineData("3d", ParticlesDimension.Three)]
        [InlineData("3", ParticlesDimension.Three)]
        [InlineData("THREE", ParticlesDimension.Three)]
        public void TryParse_KnownValues_ParseToExpected(string input, ParticlesDimension expected)
        {
            Assert.True(ParticlesDimensions.TryParse(input, out var dimension));
            Assert.Equal(expected, dimension);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("4D")]
        [InlineData("flat")]
        public void TryParse_UnknownOrEmpty_ReturnsFalse(string? input)
        {
            Assert.False(ParticlesDimensions.TryParse(input, out _));
        }

        [Fact]
        public void Parse_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => ParticlesDimensions.Parse("hexagonal"));
        }

        [Fact]
        public void ToLabel_And_ToGodotClassName_AreStable()
        {
            Assert.Equal("2D", ParticlesDimension.Two.ToLabel());
            Assert.Equal("3D", ParticlesDimension.Three.ToLabel());
            Assert.Equal("GpuParticles2D", ParticlesDimension.Two.ToGodotClassName());
            Assert.Equal("GpuParticles3D", ParticlesDimension.Three.ToGodotClassName());
        }
    }
}
