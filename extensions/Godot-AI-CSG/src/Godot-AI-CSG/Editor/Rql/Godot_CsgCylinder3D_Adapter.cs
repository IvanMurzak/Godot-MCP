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
    /// <summary>CSGCylinder3D: <c>Radius</c> and <c>Height</c> above 0, <c>Sides</c> at least 3 (see <see cref="CsgRules"/>).</summary>
    public sealed class Godot_CsgCylinder3D_Adapter : RqlTypeAdapter<CsgCylinder3D>
    {
        public override string Description =>
            "CSGCylinder3D primitive (CSG extension): Radius > 0, Height > 0 and Sides >= 3 are validated; Cone = true makes a cone";

        protected override void Members(MemberTable<CsgCylinder3D> m) =>
            m.Add<float>("Radius", c => c.Radius, (c, v) => c.Radius = v,
                    validate: (c, v) => CsgRules.Positive("Radius", v), description: "cylinder radius in metres (above 0)")
             .Add<float>("Height", c => c.Height, (c, v) => c.Height = v,
                    validate: (c, v) => CsgRules.Positive("Height", v), description: "cylinder height in metres (above 0)")
             .Add<int>("Sides", c => c.Sides, (c, v) => c.Sides = v,
                    validate: (c, v) => CsgRules.AtLeast("Sides", v, CsgRules.MinSides), description: "sides around the cylinder (at least 3)");
    }
}
#endif
