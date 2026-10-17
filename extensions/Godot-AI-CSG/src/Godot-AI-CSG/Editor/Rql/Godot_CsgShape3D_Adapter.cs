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
    /// <summary>
    /// Every CSG node (CSGBox3D, CSGSphere3D, CSGCylinder3D, CSGTorus3D, CSGCombiner3D, CSGPolygon3D, CSGMesh3D): the
    /// boolean <c>Operation</c> is the plain C# member (enum by name: "Union", "Intersection", "Subtraction"); the module
    /// adds <c>rootShape</c>, whether this shape is the one that builds the combined mesh.
    /// </summary>
    public sealed class Godot_CsgShape3D_Adapter : RqlTypeAdapter<CsgShape3D>
    {
        public override string Description =>
            "CSG shape (CSG extension): Operation = \"Union\" | \"Intersection\" | \"Subtraction\" combines it with its CSG siblings " +
            "under the same CSG parent; rootShape is true for the shape that builds the final mesh";

        protected override void Members(MemberTable<CsgShape3D> m) =>
            m.Add<bool>("rootShape", s => s.IsRootShape(),
                description: "true when no CSG node is above this one: it builds the combined mesh of its CSG children");
    }
}
#endif
