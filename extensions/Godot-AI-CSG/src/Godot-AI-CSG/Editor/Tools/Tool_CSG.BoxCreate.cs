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
        /// Editor-only tool — creates a <c>CsgBox3D</c> node in the currently edited scene and returns its
        /// structured config. All Godot API access is marshalled onto the editor main thread via
        /// <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            BoxCreateToolId,
            Title = "CSG / Box Create"
        )]
        [Description("Create a CsgBox3D primitive in the currently edited Godot scene and return its " +
            "structured config. Optionally pass 'parentPath' (a node path relative to the scene root) to " +
            "parent it (defaults to the scene root, or place it under a CsgCombiner3D to take part in a boolean " +
            "op), 'name' to rename it, and 'sizeX'/'sizeY'/'sizeZ' to seed its extents (each clamped to a " +
            "positive value; default 2). The new node's owner is set to the scene root so it is saved with the scene.")]
        public CsgShapeInfo BoxCreate
        (
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Box width along X (Godot 'size.x'); clamped to > 0. Defaults to 2.")]
            double? sizeX = null,
            [Description("Box height along Y (Godot 'size.y'); clamped to > 0. Defaults to 2.")]
            double? sizeY = null,
            [Description("Box depth along Z (Godot 'size.z'); clamped to > 0. Defaults to 2.")]
            double? sizeZ = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var box = new CsgBox3D();

                var x = CsgDefaults.ClampSize(sizeX ?? CsgDefaults.DefaultBoxSize);
                var y = CsgDefaults.ClampSize(sizeY ?? CsgDefaults.DefaultBoxSize);
                var z = CsgDefaults.ClampSize(sizeZ ?? CsgDefaults.DefaultBoxSize);
                box.Size = new Vector3((float)x, (float)y, (float)z);

                AttachToScene(box, name, parentPath);
                return ReadInfo(box);
            });
        }
    }
}
#endif
