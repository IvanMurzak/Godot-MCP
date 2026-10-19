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

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>The whole-number rule of the integer struct adapters (Vector2I / Vector3I / Vector4I / Rect2I).</summary>
    internal static class WholeNumbers
    {
        /// <summary>The components as ints when there are exactly <paramref name="width"/> whole numbers; null otherwise.</summary>
        public static int[]? Of(float[] parts, int width)
        {
            if (parts.Length != width) return null;
            var ints = new int[width];
            for (int k = 0; k < width; k++)
            {
                if (Math.Abs(parts[k] - Math.Round(parts[k])) > 1e-6) return null;
                ints[k] = (int)Math.Round(parts[k]);
            }
            return ints;
        }
    }
}
#endif
