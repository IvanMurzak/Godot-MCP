/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.CSG
{
    public partial class Tool_CSG
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>csg-defaults</c>).</summary>
        public const string DefaultsToolId = "csg-defaults";

        /// <summary>Editor tool id — create a <c>CsgBox3D</c> (<c>csg-box-create</c>).</summary>
        public const string BoxCreateToolId = "csg-box-create";

        /// <summary>Editor tool id — create a <c>CsgSphere3D</c> (<c>csg-sphere-create</c>).</summary>
        public const string SphereCreateToolId = "csg-sphere-create";

        /// <summary>Editor tool id — create a <c>CsgCylinder3D</c> (<c>csg-cylinder-create</c>).</summary>
        public const string CylinderCreateToolId = "csg-cylinder-create";

        /// <summary>Editor tool id — create a <c>CsgCombiner3D</c> container (<c>csg-combiner-create</c>).</summary>
        public const string CombinerCreateToolId = "csg-combiner-create";

        /// <summary>Editor tool id — set a CSG node's boolean operation (<c>csg-set-operation</c>).</summary>
        public const string SetOperationToolId = "csg-set-operation";

        /// <summary>Editor tool id — read a CSG node's scalar config (<c>csg-get</c>).</summary>
        public const string GetToolId = "csg-get";
    }
}
