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
    /// <summary>NavigationRegion2D: validated costs, <c>polygonCount</c>, and <c>Bake()</c> (a synchronous navigation polygon bake).</summary>
    public sealed class Godot_NavigationRegion2D_Adapter : RqlTypeAdapter<NavigationRegion2D>
    {
        public override string Description =>
            "NavigationRegion2D navigable area (Navigation extension): set NavigationPolygon = {\"$type\": \"NavigationPolygon\"} (or a res:// path), " +
            "add outlines to it, call Bake(); EnterCost / TravelCost >= 0; polygonCount";

        protected override void Members(MemberTable<NavigationRegion2D> m) =>
            m.Costs(r => r.EnterCost, (r, v) => r.EnterCost = v, r => r.TravelCost, (r, v) => r.TravelCost = v)
             .Add<int>("polygonCount", r => r.NavigationPolygon?.GetPolygonCount() ?? 0,
                    description: "polygons in the NavigationPolygon (0 before a bake or without one)")
             .Function<int>("Bake", Bake,
                    "bake the NavigationPolygon from its outlines and the geometry below the region, on the main thread; returns the " +
                    "polygon count. The baked polygons are not undoable: bake again");

        private static int Bake(NavigationRegion2D region)
        {
            if (NavigationRules.CanBake($"NavigationRegion2D '{region.Name}'", "NavigationPolygon", region.NavigationPolygon != null, region.IsBaking()) is { } reason)
                throw new InvalidOperationException(reason);
            region.BakeNavigationPolygon(onThread: false);
            return region.NavigationPolygon!.GetPolygonCount();
        }
    }
}
#endif
