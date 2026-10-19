/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Plane: compact "(a, b, c, d)" output — the normal (a, b, c) and the distance d from the origin.</summary>
    public sealed class Godot_Plane_Adapter : GodotFloatTupleAdapter<Plane>
    {
        public override string Description => "Plane: write as \"(a, b, c, d)\" (normal, distance) or [a, b, c, d]";
        protected override string Expected => "(a, b, c, d) or [a, b, c, d]";
        protected override int Width => 4;
        protected override float Component(Plane v, int index) => index switch { 0 => v.Normal.X, 1 => v.Normal.Y, 2 => v.Normal.Z, _ => v.D };
        protected override Plane? Make(float[] f) => f.Length == 4 ? new Plane(f[0], f[1], f[2], f[3]) : (Plane?)null;
    }
}
