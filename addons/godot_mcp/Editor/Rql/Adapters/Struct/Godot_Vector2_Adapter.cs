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
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Vector2: compact "(x, y)" output; "(…)" / [ … ] / object literal input.</summary>
    public sealed class Godot_Vector2_Adapter : GodotFloatTupleAdapter<Vector2>
    {
        public override string Description => "Vector2: write as \"(x, y)\", [x, y] or {x: 1, y: 2}";
        protected override string Expected => "(x, y) or [x, y]";
        protected override int Width => 2;
        // Component-wise arithmetic (RQL spec §4.2): Scale * 1.5, Position + [0, 1, 0], -Velocity.
        protected override bool Arithmetic => true;
        protected override float Component(Vector2 v, int index) => index switch { 0 => v.X, _ => v.Y };
        protected override Vector2? Make(float[] f) => f.Length == 2 ? new Vector2(f[0], f[1]) : (Vector2?)null;
    }
}
#endif
