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
using Godot;

namespace com.IvanMurzak.Godot.MCP.CSG
{
    public partial class Tool_CSG
    {
        /// <summary>
        /// Editor-only tool — creates a <c>CsgCylinder3D</c> node in the currently edited scene and returns its
        /// structured config. All Godot API access is marshalled onto the editor main thread via
        /// <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            CylinderCreateToolId,
            Title = "CSG / Cylinder Create"
        )]
        [Description("Create a CsgCylinder3D primitive in the currently edited Godot scene and return its " +
            "structured config. Optionally pass 'parentPath' (a node path relative to the scene root) to " +
            "parent it (defaults to the scene root, or place it under a CsgCombiner3D for a boolean op), " +
            "'name' to rename it, 'radius' (clamped to > 0; default 0.5), and 'height' (clamped to > 0; " +
            "default 2). The new node's owner is set to the scene root so it is saved with the scene.")]
        public CsgShapeInfo CylinderCreate
        (
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Cylinder radius (Godot 'radius'); clamped to > 0. Defaults to 0.5.")]
            double? radius = null,
            [Description("Cylinder height (Godot 'height'); clamped to > 0. Defaults to 2.")]
            double? height = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var cylinder = new CsgCylinder3D
                {
                    Radius = (float)CsgDefaults.ClampRadius(radius ?? CsgDefaults.DefaultRadius),
                    Height = (float)CsgDefaults.ClampHeight(height ?? CsgDefaults.DefaultHeight)
                };

                AttachToScene(cylinder, name, parentPath);
                return ReadInfo(cylinder);
            });
        }
    }
}
#endif
