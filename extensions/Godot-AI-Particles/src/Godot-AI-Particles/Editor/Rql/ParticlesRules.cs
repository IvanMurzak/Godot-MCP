/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Globalization;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>
    /// The value rules the module's adapters enforce before an emitter is written (an RQL <c>validate</c> runs before any
    /// write, so a rejected value fails the whole mutation). Each returns the rejection reason, or null when the value is
    /// valid. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class ParticlesRules
    {
        /// <summary>Godot refuses fewer than one particle (<c>set_amount</c> logs an error and keeps the old value).</summary>
        public static string? Amount(int amount) =>
            amount >= 1 ? null : $"Amount must be at least 1 (got {amount})";

        /// <summary>Godot refuses a lifetime of zero or less (<c>set_lifetime</c>).</summary>
        public static string? Lifetime(double lifetime) =>
            lifetime > 0 && !double.IsNaN(lifetime) && !double.IsInfinity(lifetime)
                ? null
                : $"Lifetime must be a number of seconds above 0 (got {lifetime.ToString(CultureInfo.InvariantCulture)})";

        /// <summary>Explosiveness and Randomness are ratios: 0 to 1 (the range of the Inspector).</summary>
        public static string? Ratio(string member, float value) =>
            value >= 0f && value <= 1f
                ? null
                : $"{member} must be between 0 and 1 (got {value.ToString(CultureInfo.InvariantCulture)})";
    }
}
