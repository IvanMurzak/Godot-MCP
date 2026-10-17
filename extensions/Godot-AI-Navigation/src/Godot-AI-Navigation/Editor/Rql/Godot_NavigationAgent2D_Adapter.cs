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
    /// <summary>NavigationAgent2D: validated radius, distances, speed, time horizons and avoidance priority.</summary>
    public sealed class Godot_NavigationAgent2D_Adapter : RqlTypeAdapter<NavigationAgent2D>
    {
        public override string Description =>
            "NavigationAgent2D pathfinding + avoidance helper (Navigation extension): Radius > 0; PathDesiredDistance, " +
            "TargetDesiredDistance, MaxSpeed, TimeHorizonAgents, TimeHorizonObstacles >= 0; AvoidancePriority 0..1";

        protected override void Members(MemberTable<NavigationAgent2D> m) =>
            m.Add<float>("Radius", a => a.Radius, (a, v) => a.Radius = v,
                    validate: (a, v) => NavigationRules.Radius(v), description: "avoidance radius in pixels (above 0)")
             .NonNegative("PathDesiredDistance", a => a.PathDesiredDistance, (a, v) => a.PathDesiredDistance = v,
                    "distance at which the next path point counts as reached")
             .NonNegative("TargetDesiredDistance", a => a.TargetDesiredDistance, (a, v) => a.TargetDesiredDistance = v,
                    "distance at which the target counts as reached")
             .NonNegative("MaxSpeed", a => a.MaxSpeed, (a, v) => a.MaxSpeed = v, "maximum speed used by avoidance, pixels per second")
             .NonNegative("TimeHorizonAgents", a => a.TimeHorizonAgents, (a, v) => a.TimeHorizonAgents = v,
                    "seconds ahead that avoidance looks for other agents")
             .NonNegative("TimeHorizonObstacles", a => a.TimeHorizonObstacles, (a, v) => a.TimeHorizonObstacles = v,
                    "seconds ahead that avoidance looks for obstacles")
             .Add<float>("AvoidancePriority", a => a.AvoidancePriority, (a, v) => a.AvoidancePriority = v,
                    validate: (a, v) => NavigationRules.Priority(v), description: "avoidance priority (0 to 1; higher agents are avoided by lower ones)");
    }
}
#endif
