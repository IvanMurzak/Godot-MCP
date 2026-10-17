/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// The agent guide of the CSG RQL module, rendered into the <c>godot-rql</c> skill (<see cref="CsgModule"/>). Pure strings
    /// (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e manifest
    /// (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D.
    /// </summary>
    public static class CsgGuide
    {
        public const string Topic = "CSG";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "root | call AddChild(\"CSGCombiner3D\", \"Combo\")   a combiner: its CSG children are combined into one mesh\n" +
            "nodes | where name == \"Combo\" | call AddChild(\"CSGBox3D\", \"Box\")   a box inside it (CSGSphere3D, CSGCylinder3D, CSGTorus3D too)\n" +
            "nodes | where name == \"Box\" | set Size = \"(3, 3, 3)\"   box extents (every axis above 0)\n" +
            "nodes | where name == \"Combo\" | call AddChild(\"CSGSphere3D\", \"Ball\")   a sphere next to the box\n" +
            "nodes | where name == \"Ball\" | set Radius = 1.8, RadialSegments = 16, Rings = 8, Operation = \"Subtraction\"   carve the sphere out of the box\n" +
            "nodes | where name == \"Combo\" | call AddChild(\"CSGCylinder3D\", \"Pipe\")   a cylinder\n" +
            "nodes | where name == \"Pipe\" | set Radius = 0.5, Height = 4, Sides = 12, Operation = \"Union\"   merge it in (Intersection keeps only the overlap)\n" +
            "nodes | where $ is CSGShape3D | select name class Operation rootShape   the CSG nodes, their operation, which one builds the mesh";

        public const string Markdown =
            "CSG (constructive solid geometry) shapes are nodes: CSGBox3D, CSGSphere3D, CSGCylinder3D, CSGTorus3D, CSGPolygon3D, " +
            "CSGMesh3D, and CSGCombiner3D to group them. A CSG node's `Operation` (\"Union\", \"Intersection\", \"Subtraction\") combines " +
            "it with the CSG siblings before it; the top CSG node of a tree (`rootShape` true) builds the result. Add shapes with " +
            "`AddChild`, then set their members (undoable; the scene is marked unsaved):\n\n```\n" + Examples + "\n```\n\n" +
            "Validated (an invalid value fails the whole mutation and changes nothing): CSGBox3D `Size` (every axis above 0), " +
            "CSGSphere3D `Radius` (above 0), `RadialSegments` (at least 4), `Rings` (at least 1), CSGCylinder3D `Radius` and `Height` " +
            "(above 0), `Sides` (at least 3), CSGTorus3D `Sides` and `RingSides` (at least 3). Godot's defaults: box 1×1×1, sphere " +
            "radius 0.5 with 12 segments and 6 rings, cylinder radius 0.5, height 2, 8 sides. Every other property is a C# member " +
            "(`Material`, `UseCollision`, `Cone`, `SmoothFaces` …) or a `props[\"snake_name\"]`; `rql-schema \"CSGSphere3D\"` lists them.";

        /// <summary>The queries of <see cref="Examples"/>, in order.</summary>
        public static string[] Queries =>
            Examples.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();
    }
}
