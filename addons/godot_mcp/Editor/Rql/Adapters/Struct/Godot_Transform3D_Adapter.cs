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
    /// <summary>Transform3D: basis axes then origin as one 12-number tuple (read Position / Rotation on the node for the friendly form).</summary>
    public sealed class Godot_Transform3D_Adapter : GodotFloatTupleAdapter<Transform3D>
    {
        public override string Description => "Transform3D: write as \"(xx, xy, xz, yx, yy, yz, zx, zy, zz, ox, oy, oz)\" (basis X, Y, Z axes, then origin)";
        protected override string Expected => "(xx, xy, xz, yx, yy, yz, zx, zy, zz, ox, oy, oz)";
        protected override int Width => 12;
        protected override float Component(Transform3D v, int index) => index switch { 0 => v.Basis.X.X, 1 => v.Basis.X.Y, 2 => v.Basis.X.Z, 3 => v.Basis.Y.X, 4 => v.Basis.Y.Y, 5 => v.Basis.Y.Z, 6 => v.Basis.Z.X, 7 => v.Basis.Z.Y, 8 => v.Basis.Z.Z, 9 => v.Origin.X, 10 => v.Origin.Y, _ => v.Origin.Z };
        protected override Transform3D? Make(float[] f) => f.Length == 12 ? new Transform3D(new Basis(new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5]), new Vector3(f[6], f[7], f[8])), new Vector3(f[9], f[10], f[11])) : (Transform3D?)null;
    }
}
#endif
