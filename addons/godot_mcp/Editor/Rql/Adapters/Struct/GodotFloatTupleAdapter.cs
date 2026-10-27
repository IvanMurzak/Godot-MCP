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
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// A float tuple struct (vectors, colors, transforms, rects …): its components come from <see cref="Component"/>, with -0
    /// normalised to 0 (Godot produces -0 from rotations and quaternions). RQL (0.2+) tidies the display text (6 significant
    /// digits, float noise as 0) and compares object literals by the members they name.
    /// </summary>
    public abstract class GodotFloatTupleAdapter<T> : NumericTupleAdapter<T> where T : struct
    {
        protected sealed override float Part(T value, int index) => 0f + Component(value, index);

        /// <summary>Component <paramref name="index"/> of <paramref name="value"/>.</summary>
        protected abstract float Component(T value, int index);
    }
}
#endif
