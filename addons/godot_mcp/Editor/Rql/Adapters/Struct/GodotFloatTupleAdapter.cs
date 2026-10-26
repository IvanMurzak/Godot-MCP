/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// A Godot numeric struct (vectors, colors, transforms, rects …). An object literal compares like the text and list
    /// forms (<c>where Position == {x: 0, y: 1, z: 0}</c>): RQL 0.1's tuple adapter parses only those two, so the literal
    /// is built through the kernel, exactly as a write builds it.
    /// </summary>
    public abstract class GodotTupleAdapter<T> : NumericTupleAdapter<T> where T : struct
    {
        // The literal of a where clause is the same object for every row: built once.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ObjectValue, Built> _literals =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ObjectValue, Built>();

        private sealed class Built
        {
            public Built(T? value) { Value = value; }
            public readonly T? Value;
        }

        protected override bool? Equal(T value, object? other, RqlKernel kernel)
        {
            if (!(other is ObjectValue literal)) return base.Equal(value, other, kernel);
            var built = _literals.GetValue(literal, l => new Built(kernel.TryCoerce(l, typeof(T), out var v, out _) && v is T t ? t : (T?)null));
            return built.Value is T target ? base.Equal(value, target, kernel) : (bool?)false;
        }
    }

    /// <summary>
    /// A float tuple struct: its components come from <see cref="Component"/>, with -0 normalised to 0 so "(-0, 0, 0)" never
    /// reaches the output (Godot produces -0 from rotations and quaternions). The text shows each component tidied for
    /// reading (<see cref="Tidy"/>): float rounding noise such as cos(90°) = -4.371139E-08 in a rotated basis, or 0.533333361
    /// for 136/255, would otherwise reach the agent; comparisons and writes keep the exact values.
    /// </summary>
    public abstract class GodotFloatTupleAdapter<T> : GodotTupleAdapter<T> where T : struct
    {
        protected sealed override float Part(T value, int index) => 0f + Component(value, index);

        /// <summary>Component <paramref name="index"/> of <paramref name="value"/>.</summary>
        protected abstract float Component(T value, int index);

        protected override JsonNode? Encode(T value, EncodeContext context)
        {
            var sb = new StringBuilder("(");
            for (int i = 0; i < Width; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Tidy(Part(value, i)));
            }
            return JsonValue.Create(sb.Append(')').ToString());
        }

        /// <summary>
        /// A float component as text: 0 below 1e-6 (float noise), else at most 7 significant digits (a float holds about 7),
        /// never in exponent form; NaN and infinities as .NET writes them.
        /// </summary>
        public static string Tidy(float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) return f.ToString(CultureInfo.InvariantCulture);
            if (Math.Abs(f) < 1e-6f) return "0";
            var text = ((double)f).ToString("G7", CultureInfo.InvariantCulture);
            if (text.IndexOf('E') < 0) return text;
            // Large magnitudes (|f| ≥ 1e7) print as whole numbers, small ones (1e-6 … 1e-5) with their significant digits.
            var rounded = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            return Math.Abs(rounded) >= 1 ? rounded.ToString("F0", CultureInfo.InvariantCulture) : rounded.ToString("0.############", CultureInfo.InvariantCulture);
        }
    }
}
#endif
