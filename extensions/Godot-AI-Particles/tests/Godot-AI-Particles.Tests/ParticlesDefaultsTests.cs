/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Particles;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Particles.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="ParticlesDefaults"/> — the recommended starter config AND the
    /// value-clamping rules the editor tools reuse before writing to a live node. These are the testable
    /// core that backs the editor-only <c>particles-create</c>/<c>-configure</c> tools (which themselves
    /// need a live Godot editor, exercised by the E2E leg).
    /// </summary>
    public class ParticlesDefaultsTests
    {
        [Theory]
        [InlineData(ParticlesDimension.Two, "2D")]
        [InlineData(ParticlesDimension.Three, "3D")]
        public void For_ReturnsPopulatedStarterConfig(ParticlesDimension dimension, string expectedLabel)
        {
            var info = ParticlesDefaults.For(dimension);

            Assert.Equal(expectedLabel, info.Dimension);
            Assert.Equal(string.Empty, info.NodePath);   // not bound to a node
            Assert.Equal(ParticlesDefaults.DefaultAmount, info.Amount);
            Assert.Equal(ParticlesDefaults.DefaultLifetime, info.Lifetime);
            Assert.Equal(ParticlesDefaults.DefaultExplosiveness, info.Explosiveness);
            Assert.False(info.OneShot);
            Assert.True(info.Emitting);
            Assert.Equal(1.0, info.SpeedScale);
            Assert.True(info.LocalCoords);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(1, 1)]
        [InlineData(64, 64)]
        public void ClampAmount_FloorsAtOne(int input, int expected)
        {
            Assert.Equal(expected, ParticlesDefaults.ClampAmount(input));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void ClampLifetime_NonPositive_BecomesMinPositive(double input)
        {
            Assert.Equal(ParticlesDefaults.MinLifetime, ParticlesDefaults.ClampLifetime(input));
        }

        [Fact]
        public void ClampLifetime_PositivePassesThrough()
        {
            Assert.Equal(2.5, ParticlesDefaults.ClampLifetime(2.5));
        }

        [Theory]
        [InlineData(-0.2f, 0f)]
        [InlineData(0f, 0f)]
        [InlineData(0.5f, 0.5f)]
        [InlineData(1f, 1f)]
        [InlineData(1.7f, 1f)]
        public void ClampRatio_ClampsIntoUnitInterval(float input, float expected)
        {
            Assert.Equal(expected, ParticlesDefaults.ClampRatio(input));
        }

        [Theory]
        [InlineData(-3.0, 0.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(2.0, 2.0)]
        public void ClampSpeedScale_FloorsAtZero(double input, double expected)
        {
            Assert.Equal(expected, ParticlesDefaults.ClampSpeedScale(input));
        }

        [Theory]
        [InlineData(-1.0, 0.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(0.75, 0.75)]
        public void ClampPreprocess_FloorsAtZero(double input, double expected)
        {
            Assert.Equal(expected, ParticlesDefaults.ClampPreprocess(input));
        }
    }
}
