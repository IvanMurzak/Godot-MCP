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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.CSG
{
    public partial class Tool_CSG
    {
        /// <summary>
        /// Editor-only, read-only tool — reads the scalar config of an existing CSG node
        /// (<c>CsgBox3D</c>/<c>CsgSphere3D</c>/<c>CsgCylinder3D</c>/<c>CsgCombiner3D</c>). Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "CSG / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read the scalar config (kind, operation, and the type-specific size / radius / height / " +
            "radial segments) of an existing CSG node, addressed by 'nodePath' (relative to the edited scene " +
            "root). Read-only: does not modify the scene.")]
        public CsgShapeInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the CSG node to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() => ReadInfo(ResolveCsgNodeOrThrow(nodePath)));
        }
    }
}
#endif
