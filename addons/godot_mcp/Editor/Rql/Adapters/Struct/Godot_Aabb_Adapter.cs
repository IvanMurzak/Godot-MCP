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
    /// <summary>Aabb (3D bounds): compact "(x, y, z, width, height, depth)" output.</summary>
    public sealed class Godot_Aabb_Adapter : GodotFloatTupleAdapter<Aabb>
    {
        public override string Description => "Aabb: write as \"(x, y, z, width, height, depth)\" (position, then size)";
        protected override string Expected => "(x, y, z, width, height, depth)";
        protected override int Width => 6;
        protected override float Component(Aabb v, int index) => index switch { 0 => v.Position.X, 1 => v.Position.Y, 2 => v.Position.Z, 3 => v.Size.X, 4 => v.Size.Y, _ => v.Size.Z };
        protected override Aabb? Make(float[] f) => f.Length == 6 ? new Aabb(new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5])) : (Aabb?)null;
    }
}
