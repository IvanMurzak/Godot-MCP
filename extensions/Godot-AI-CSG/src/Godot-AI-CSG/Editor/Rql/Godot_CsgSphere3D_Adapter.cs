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
    /// <summary>CSGSphere3D: <c>Radius</c> above 0, <c>RadialSegments</c> at least 4, <c>Rings</c> at least 1 (see <see cref="CsgRules"/>).</summary>
    public sealed class Godot_CsgSphere3D_Adapter : RqlTypeAdapter<CsgSphere3D>
    {
        public override string Description =>
            "CSGSphere3D primitive (CSG extension): Radius > 0, RadialSegments >= 4 and Rings >= 1 are validated";

        protected override void Members(MemberTable<CsgSphere3D> m) =>
            m.Add<float>("Radius", s => s.Radius, (s, v) => s.Radius = v,
                    validate: (s, v) => CsgRules.Positive("Radius", v), description: "sphere radius in metres (above 0)")
             .Add<int>("RadialSegments", s => s.RadialSegments, (s, v) => s.RadialSegments = v,
                    validate: (s, v) => CsgRules.AtLeast("RadialSegments", v, CsgRules.MinRadialSegments),
                    description: "segments around the sphere (at least 4)")
             .Add<int>("Rings", s => s.Rings, (s, v) => s.Rings = v,
                    validate: (s, v) => CsgRules.AtLeast("Rings", v, CsgRules.MinRings), description: "horizontal slices (at least 1)");
    }
}
#endif
