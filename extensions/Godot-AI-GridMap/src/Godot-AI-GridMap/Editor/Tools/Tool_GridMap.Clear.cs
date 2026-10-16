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

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        /// <summary>
        /// Editor-only tool — clears ALL cells of a <c>GridMap</c> (Godot <c>clear()</c>), leaving the node and
        /// its MeshLibrary assignment intact. Main-thread-marshalled. Returns the emptied GridMap's config.
        /// </summary>
        [AiTool
        (
            ClearToolId,
            Title = "GridMap / Clear"
        )]
        [Description("Clear ALL cells of a GridMap (addressed by 'nodePath', relative to the edited scene " +
            "root), leaving the node and its assigned MeshLibrary intact. Returns the GridMap's config with a " +
            "cell count of 0. To clear a single cell, use gridmap-clear-cell.")]
        public GridMapNodeInfo Clear
        (
            [Description("Node path (relative to the edited scene root) of the GridMap node to clear.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var grid = ResolveGridMapOrThrow(nodePath);
                grid.Clear();

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(grid);
            });
        }
    }
}
#endif
