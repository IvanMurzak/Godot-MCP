/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Editor-only (RQL needs the editor-only com.IvanMurzak.Rql package); also compiled by the xUnit suite.
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Transform2D: X axis, Y axis and origin as one 6-number tuple.</summary>
    public sealed class Godot_Transform2D_Adapter : GodotFloatTupleAdapter<Transform2D>
    {
        public override string Description => "Transform2D: write as \"(xx, xy, yx, yy, ox, oy)\" (X axis, Y axis, origin)";
        protected override string Expected => "(xx, xy, yx, yy, ox, oy)";
        protected override int Width => 6;
        protected override float Component(Transform2D v, int index) => index switch { 0 => v.X.X, 1 => v.X.Y, 2 => v.Y.X, 3 => v.Y.Y, 4 => v.Origin.X, _ => v.Origin.Y };
        protected override Transform2D? Make(float[] f) => f.Length == 6 ? new Transform2D(new Vector2(f[0], f[1]), new Vector2(f[2], f[3]), new Vector2(f[4], f[5])) : (Transform2D?)null;
    }
}
#endif
