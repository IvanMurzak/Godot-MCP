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

namespace com.IvanMurzak.Godot.MCP.Particles.Tests
{
    public class ParticlesRulesTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        public void AmountOfOneOrMoreIsValid(int amount) => Assert.Null(ParticlesRules.Amount(amount));

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void AmountBelowOneIsRejected(int amount) => Assert.Contains("at least 1", ParticlesRules.Amount(amount));

        [Theory]
        [InlineData(0.01)]
        [InlineData(2.0)]
        public void PositiveLifetimeIsValid(double lifetime) => Assert.Null(ParticlesRules.Lifetime(lifetime));

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void NonPositiveOrNonFiniteLifetimeIsRejected(double lifetime) => Assert.Contains("above 0", ParticlesRules.Lifetime(lifetime));

        [Theory]
        [InlineData(0f)]
        [InlineData(0.5f)]
        [InlineData(1f)]
        public void RatiosFromZeroToOneAreValid(float value) => Assert.Null(ParticlesRules.Ratio("Explosiveness", value));

        [Theory]
        [InlineData(-0.1f)]
        [InlineData(1.5f)]
        [InlineData(float.NaN)]
        public void RatiosOutsideZeroToOneAreRejectedByName(float value)
        {
            var reason = ParticlesRules.Ratio("Randomness", value);
            Assert.NotNull(reason);
            Assert.StartsWith("Randomness must be between 0 and 1", reason);
        }
    }
}
