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
    /// <summary>
    /// NavigationPolygon (the 2D region's resource): <c>AddOutline(points)</c> / <c>ClearOutlines()</c> as calls on the resource
    /// itself (so they are undoable), and <c>outlineCount</c>.
    /// </summary>
    public sealed class Godot_NavigationPolygon_Adapter : RqlTypeAdapter<NavigationPolygon>
    {
        public override string Description =>
            "NavigationPolygon (Navigation extension): call AddOutline([\"(0, 0)\", \"(100, 0)\", …]) / ClearOutlines(), then Bake() the region; outlineCount";

        protected override void Members(MemberTable<NavigationPolygon> m) =>
            m.Add<int>("outlineCount", p => p.GetOutlineCount(), description: "outlines the bake starts from")
             // Typed calls, not Expose: the CLR overload set of AddOutline differs between GodotSharp versions (Vector2[] in
             // 4.3, ReadOnlySpan<Vector2> later), and a span parameter cannot be built from a query literal.
             .Method<Vector2[]>("AddOutline", AddOutline, "points",
                    "add a closed outline (a list of at least 3 \"(x, y)\" points) that the bake fills with polygons")
             .Method("ClearOutlines", p => p.ClearOutlines(), "remove every outline");

        private static void AddOutline(NavigationPolygon polygon, Vector2[] points)
        {
            if (NavigationRules.Outline(points?.Length ?? 0) is { } reason) throw new ArgumentException(reason);
            polygon.AddOutline(points!);
        }
    }
}
#endif
