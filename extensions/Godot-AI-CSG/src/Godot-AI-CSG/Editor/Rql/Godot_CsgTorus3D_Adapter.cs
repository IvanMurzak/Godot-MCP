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
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>CSGTorus3D: <c>Sides</c> and <c>RingSides</c> at least 3 (Godot refuses fewer and logs an error).</summary>
    public sealed class Godot_CsgTorus3D_Adapter : RqlTypeAdapter<CsgTorus3D>
    {
        public override string Description => "CSGTorus3D primitive (CSG extension): Sides >= 3 and RingSides >= 3 are validated";

        protected override void Members(MemberTable<CsgTorus3D> m) =>
            m.Add<int>("Sides", t => t.Sides, (t, v) => t.Sides = v,
                    validate: (t, v) => CsgRules.AtLeast("Sides", v, CsgRules.MinSides), description: "segments around the ring (at least 3)")
             .Add<int>("RingSides", t => t.RingSides, (t, v) => t.RingSides = v,
                    validate: (t, v) => CsgRules.AtLeast("RingSides", v, CsgRules.MinSides), description: "segments around the tube (at least 3)");
    }
}
#endif
