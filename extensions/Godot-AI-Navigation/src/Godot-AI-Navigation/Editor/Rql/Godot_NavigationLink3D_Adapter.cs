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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>NavigationLink3D: validated EnterCost / TravelCost; StartPosition / EndPosition / Bidirectional are plain members.</summary>
    public sealed class Godot_NavigationLink3D_Adapter : RqlTypeAdapter<NavigationLink3D>
    {
        public override string Description =>
            "NavigationLink3D off-mesh connection (Navigation extension): StartPosition / EndPosition \"(x, y, z)\" in local space, " +
            "Bidirectional; EnterCost / TravelCost >= 0";

        protected override void Members(MemberTable<NavigationLink3D> m) =>
            m.Costs(l => l.EnterCost, (l, v) => l.EnterCost = v, l => l.TravelCost, (l, v) => l.TravelCost = v);
    }
}
#endif
