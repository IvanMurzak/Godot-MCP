/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.CSG
{
    public partial class Tool_CSG
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is fully
        /// CI-unit-testable (see <c>Tool_CSG_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool csg-defaults</c>. Returns the recommended starter configuration for a CSG
        /// primitive of the requested kind, which the LLM can then pass to the matching
        /// <c>csg-*-create</c> tool.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "CSG / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (size / radius / height / radial segments) " +
            "for a Godot CSG node of the requested 'kind'. Pure-managed: touches no scene, so it is safe to " +
            "call any time to discover sane defaults before creating a real CSG node. 'kind' accepts 'box', " +
            "'sphere', 'cylinder', or 'combiner' (default 'box').")]
        public CsgShapeInfo Defaults
        (
            [Description("CSG kind: 'box' (CsgBox3D), 'sphere' (CsgSphere3D), 'cylinder' (CsgCylinder3D), or " +
                "'combiner' (CsgCombiner3D). Defaults to 'box'.")]
            string? kind = null
        )
        {
            var k = string.IsNullOrWhiteSpace(kind)
                ? CsgPrimitiveKind.Box
                : CsgPrimitiveKinds.Parse(kind);
            return CsgDefaults.For(k);
        }
    }
}
