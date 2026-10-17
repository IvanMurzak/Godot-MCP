/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// The agent guide of the Navigation RQL module, rendered into the <c>godot-rql</c> skill (<see cref="NavigationModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e manifest
    /// (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D.
    /// </summary>
    public static class NavigationGuide
    {
        public const string Topic = "Navigation";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "root | call AddChild(\"NavigationRegion3D\", \"NavRegion\")   a navigable area (NavigationRegion2D in 2D)\n" +
            "nodes | where name == \"NavRegion\" | set NavigationMesh = {\"$type\": \"NavigationMesh\"}   a new mesh (2D: NavigationPolygon = {\"$type\": \"NavigationPolygon\"}; or a res:// path)\n" +
            "nodes | where name == \"NavRegion\" | set NavigationMesh.GeometryParsedGeometryType = \"StaticColliders\", NavigationMesh.AgentRadius = 0.5   bake settings: parse collision shapes\n" +
            "nodes | where name == \"NavRegion\" | call AddChild(\"StaticBody3D\", \"Floor\")   geometry below the region\n" +
            "nodes | where name == \"Floor\" | call AddChild(\"CollisionShape3D\", \"FloorShape\")   its collision shape\n" +
            "nodes | where name == \"FloorShape\" | set Shape = {\"$type\": \"BoxShape3D\", \"Size\": \"(10, 1, 10)\"}   a 10 x 10 floor\n" +
            "nodes | where name == \"NavRegion\" | call Bake()   bake the mesh now; returns the polygon count\n" +
            "nodes | where $ is NavigationRegion3D | select name Enabled polygonCount NavigationMesh   the regions and their baked polygons\n" +
            "root | call AddChild(\"NavigationAgent3D\", \"NavAgent\")   a pathfinding agent (usually a child of the moving body)\n" +
            "nodes | where name == \"NavAgent\" | set Radius = 0.75, MaxSpeed = 8, PathDesiredDistance = 1, TargetDesiredDistance = 1, AvoidanceEnabled = true   agent settings (validated)\n" +
            "root | call AddChild(\"NavigationLink3D\", \"NavLink\")   an off-mesh connection (a jump, a ladder)\n" +
            "nodes | where name == \"NavLink\" | set StartPosition = \"(0, 0, 0)\", EndPosition = \"(5, 0, 0)\", Bidirectional = true   its two ends, in local space\n" +
            "nodes | where $ is NavigationAgent3D | select name Radius MaxSpeed PathDesiredDistance AvoidanceEnabled   read the agents";

        public const string Markdown =
            "Navigation nodes come in 2D and 3D: NavigationRegion2D/3D (a navigable area holding a NavigationPolygon / NavigationMesh), " +
            "NavigationAgent2D/3D (pathfinding and avoidance for a moving node) and NavigationLink2D/3D (an off-mesh connection). Add them " +
            "with `AddChild`, then set their members (undoable; the scene is marked unsaved):\n\n```\n" + Examples + "\n```\n\n" +
            "Validated (an invalid value fails the whole mutation and changes nothing): agent `Radius` (above 0), `PathDesiredDistance`, " +
            "`TargetDesiredDistance`, `MaxSpeed`, `Height` (3D), `TimeHorizonAgents`, `TimeHorizonObstacles` (0 or more), " +
            "`AvoidancePriority` (0 to 1); region and link `EnterCost` / `TravelCost` (0 or more). `Bake()` on a region bakes on the main " +
            "thread and needs its NavigationMesh / NavigationPolygon; the baked polygons are not undoable (bake again). The 3D mesh's " +
            "`GeometryParsedGeometryType` decides what is parsed (\"MeshInstances\", \"StaticColliders\", \"Both\"); a 2D NavigationPolygon " +
            "bakes from its outlines: `nodes | where name == \"NavRegion2D\" | get NavigationPolygon | call AddOutline([\"(0, 0)\", " +
            "\"(400, 0)\", \"(400, 300)\", \"(0, 300)\"])`. Godot's agent defaults: 3D radius 0.5 m, desired distances 1 m, max speed " +
            "10 m/s; 2D radius 10 px, path distance 20 px, target distance 10 px, max speed 100 px/s. `rql-schema \"NavigationAgent3D\"` " +
            "lists every member.";
    }
}
