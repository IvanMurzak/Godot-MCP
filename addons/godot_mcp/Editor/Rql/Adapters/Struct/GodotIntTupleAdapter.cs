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
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// A whole-number tuple struct (Vector2I/3I/4I, Rect2I). RQL's tuple adapter is single-precision: it prints 6 significant
    /// digits ("(1.23457E+06, 2)", which writes back as 1234570) and compares with a relative tolerance (100000 == 100001).
    /// Integers print and compare exactly here. An object literal keeps RQL's comparison by the members it names.
    /// </summary>
    public abstract class GodotIntTupleAdapter<T> : NumericTupleAdapter<T> where T : struct
    {
        /// <summary>Component <paramref name="index"/> of <paramref name="value"/>.</summary>
        protected abstract int Whole(T value, int index);

        protected sealed override float Part(T value, int index) => Whole(value, index);

        protected sealed override float Tolerance => 0;

        protected override JsonNode? Encode(T value, EncodeContext context)
        {
            var sb = new StringBuilder("(");
            for (int i = 0; i < Width; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Whole(value, i).ToString(CultureInfo.InvariantCulture));
            }
            return JsonValue.Create(sb.Append(')').ToString());
        }

        protected override bool? Equal(T value, object? other, RqlKernel kernel)
        {
            if (other is ObjectValue) return base.Equal(value, other, kernel);
            if (!kernel.TryCoerce(other, typeof(T), out var parsed, out _) || !(parsed is T o)) return null;
            for (int i = 0; i < Width; i++)
                if (Whole(value, i) != Whole(o, i)) return false;
            return true;
        }
    }
}
#endif
