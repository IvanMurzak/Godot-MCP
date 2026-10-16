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
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;
// '.GridMap' namespace shadows 'Godot.GridMap'; alias the Godot type so it is unambiguous.
using GdGridMap = Godot.GridMap;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        /// <summary>
        /// Editor-only tool — creates a <c>GridMap</c> node in the currently edited scene and returns its
        /// structured config. All Godot API access is marshalled onto the editor main thread via
        /// <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            CreateToolId,
            Title = "GridMap / Create"
        )]
        [Description("Create a GridMap node in the currently edited Godot scene and return its structured " +
            "config. Optionally pass 'parentPath' (a node path relative to the scene root) to parent it " +
            "(defaults to the scene root), 'name' to rename it, and 'cellSizeX'/'cellSizeY'/'cellSizeZ' to set " +
            "the cell dimensions (each clamped to > 0; unspecified axes keep the engine default). The new " +
            "node's owner is set to the scene root so it is saved with the scene. Assign a MeshLibrary and " +
            "place cells with the other gridmap-* tools.")]
        public GridMapNodeInfo Create
        (
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Optional cell size on the X axis (Godot 'cell_size.x'); clamped to > 0.")]
            double? cellSizeX = null,
            [Description("Optional cell size on the Y axis (Godot 'cell_size.y'); clamped to > 0.")]
            double? cellSizeY = null,
            [Description("Optional cell size on the Z axis (Godot 'cell_size.z'); clamped to > 0.")]
            double? cellSizeZ = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = GetEditedSceneRootOrThrow();

                Node parent = root;
                if (!string.IsNullOrWhiteSpace(parentPath))
                    parent = root.GetNodeOrNull(new NodePath(parentPath))
                        ?? throw new ArgumentException($"No parent node found at path '{parentPath}'.", nameof(parentPath));

                var grid = new GdGridMap();

                if (!string.IsNullOrWhiteSpace(name))
                    grid.Name = name;

                var size = grid.CellSize;
                if (cellSizeX.HasValue) size.X = (float)GridMapDefaults.ClampCellSize(cellSizeX.Value);
                if (cellSizeY.HasValue) size.Y = (float)GridMapDefaults.ClampCellSize(cellSizeY.Value);
                if (cellSizeZ.HasValue) size.Z = (float)GridMapDefaults.ClampCellSize(cellSizeZ.Value);
                grid.CellSize = size;

                parent.AddChild(grid);
                grid.Owner = root; // so the node is persisted when the scene is saved

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(grid);

                return ReadInfo(grid);
            });
        }
    }
}
#endif
