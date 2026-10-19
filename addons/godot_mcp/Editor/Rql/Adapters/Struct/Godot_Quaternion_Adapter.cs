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
    /// <summary>Quaternion: compact "(x, y, z, w)" output.</summary>
    public sealed class Godot_Quaternion_Adapter : GodotFloatTupleAdapter<Quaternion>
    {
        public override string Description => "Quaternion: write as \"(x, y, z, w)\" or [x, y, z, w]";
        protected override string Expected => "(x, y, z, w) or [x, y, z, w]";
        protected override int Width => 4;
        protected override float Component(Quaternion v, int index) => index switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };
        protected override Quaternion? Make(float[] f) => f.Length == 4 ? new Quaternion(f[0], f[1], f[2], f[3]) : (Quaternion?)null;
    }
}
#endif
