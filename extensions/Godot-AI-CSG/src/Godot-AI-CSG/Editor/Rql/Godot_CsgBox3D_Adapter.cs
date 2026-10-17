/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>CSGBox3D: <c>Size</c> validated (every axis above 0, see <see cref="CsgRules.Size"/>).</summary>
    public sealed class Godot_CsgBox3D_Adapter : RqlTypeAdapter<CsgBox3D>
    {
        public override string Description => "CSGBox3D primitive (CSG extension): Size \"(x, y, z)\" is validated (every axis above 0)";

        protected override void Members(MemberTable<CsgBox3D> m) =>
            // A write through one axis (set Size.X = 0) is assigned back through the setter without the member's validate,
            // so the setter checks again: the write then fails and RQL rolls the mutation back.
            m.Add<Vector3>("Size", b => b.Size, (b, v) => b.Size = Checked(v),
                validate: (b, v) => CsgRules.Size(v.X, v.Y, v.Z), description: "box extents in metres, every axis above 0");

        private static Vector3 Checked(Vector3 v) =>
            CsgRules.Size(v.X, v.Y, v.Z) is { } reason ? throw new ArgumentException(reason) : v;
    }
}
#endif
