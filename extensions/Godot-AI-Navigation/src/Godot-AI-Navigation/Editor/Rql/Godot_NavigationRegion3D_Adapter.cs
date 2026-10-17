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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>NavigationRegion3D: validated costs, <c>polygonCount</c>, and <c>Bake()</c> (a synchronous navigation mesh bake).</summary>
    public sealed class Godot_NavigationRegion3D_Adapter : RqlTypeAdapter<NavigationRegion3D>
    {
        public override string Description =>
            "NavigationRegion3D navigable area (Navigation extension): set NavigationMesh = {\"$type\": \"NavigationMesh\"} (or a res:// path), " +
            "call Bake() to bake it from the region's child geometry; EnterCost / TravelCost >= 0; polygonCount";

        protected override void Members(MemberTable<NavigationRegion3D> m) =>
            m.Costs(r => r.EnterCost, (r, v) => r.EnterCost = v, r => r.TravelCost, (r, v) => r.TravelCost = v)
             .Add<int>("polygonCount", r => r.NavigationMesh?.GetPolygonCount() ?? 0,
                    description: "polygons in the NavigationMesh (0 before a bake or without a mesh)")
             .Function<int>("Bake", Bake,
                    "bake the NavigationMesh from the geometry below the region, on the main thread (the mesh's parse settings " +
                    "decide which nodes count); returns the polygon count. The baked polygons are not undoable: bake again");

        private static int Bake(NavigationRegion3D region)
        {
            if (NavigationRules.CanBake($"NavigationRegion3D '{region.Name}'", "NavigationMesh", region.NavigationMesh != null, region.IsBaking()) is { } reason)
                throw new InvalidOperationException(reason);
            region.BakeNavigationMesh(onThread: false);
            return region.NavigationMesh!.GetPolygonCount();
        }
    }
}
#endif
