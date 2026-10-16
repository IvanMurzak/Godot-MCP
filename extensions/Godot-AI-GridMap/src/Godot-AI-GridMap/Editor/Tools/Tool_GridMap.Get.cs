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

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        /// <summary>
        /// Editor-only, read-only tool — reads the scalar config (cell size/scale/octant/centering, assigned
        /// MeshLibrary path, used-cell count) of an existing <c>GridMap</c> node. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "GridMap / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read the scalar config (cell size X/Y/Z, cell scale, octant size, axis centering, " +
            "assigned MeshLibrary path, used-cell count) of an existing GridMap node, addressed by 'nodePath' " +
            "(relative to the edited scene root). Read-only: does not modify the scene.")]
        public GridMapNodeInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the GridMap node to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() => ReadInfo(ResolveGridMapOrThrow(nodePath)));
        }
    }
}
#endif
