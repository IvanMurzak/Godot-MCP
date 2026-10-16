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
using Godot;
// '.GridMap' namespace shadows 'Godot.GridMap'; alias the Godot type so an unqualified 'GridMap' can never
// bind to the namespace (a CS0118 at package-build time).
using GdGridMap = Godot.GridMap;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    /// <summary>
    /// Editor-only shared helpers for the <c>gridmap-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The reads/writes use the strongly-typed <c>Godot.GridMap</c> API (aliased <c>GdGridMap</c>) on purpose —
    /// that typed surface (resolved from the consumer's own GodotSharp) is exactly what the source-only
    /// packaging recipe must compile against cross-version. The properties used are stable across Godot
    /// 4.3 … 4.5, so one info shape (<see cref="GridMapNodeInfo"/>) covers every version.
    /// </para>
    /// </summary>
    public partial class Tool_GridMap
    {
        /// <summary>The edited scene root, or throw a clear error when no scene is open.</summary>
        static Node GetEditedSceneRootOrThrow()
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
                throw new InvalidOperationException(
                    "No scene is currently being edited; open or create a scene first.");
            return root;
        }

        /// <summary>
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a <c>GridMap</c> node,
        /// throwing a clear error when the path is empty, the node is missing, or the node is not a
        /// <c>GridMap</c>.
        /// </summary>
        static GdGridMap ResolveGridMapOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            var node = root.GetNodeOrNull(new NodePath(nodePath));
            if (node == null)
                throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (node is not GdGridMap grid)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a GridMap.", nameof(nodePath));

            return grid;
        }

        /// <summary>Build a pure-managed <see cref="GridMapNodeInfo"/> snapshot from a live GridMap node.</summary>
        static GridMapNodeInfo ReadInfo(GdGridMap grid)
        {
            var meshLib = grid.MeshLibrary;
            var size = grid.CellSize;
            return new GridMapNodeInfo
            {
                NodePath = grid.GetPath().ToString(),
                Kind = "GridMap",
                TypeName = grid.GetClass(),
                CellSizeX = size.X,
                CellSizeY = size.Y,
                CellSizeZ = size.Z,
                CellScale = grid.CellScale,
                CellOctantSize = grid.CellOctantSize,
                CellCenterX = grid.CellCenterX,
                CellCenterY = grid.CellCenterY,
                CellCenterZ = grid.CellCenterZ,
                MeshLibraryPath = meshLib?.ResourcePath ?? string.Empty,
                CellCount = grid.GetUsedCells().Count
            };
        }

        /// <summary>Build a pure-managed <see cref="GridMapCellInfo"/> snapshot of one cell from a live node.</summary>
        static GridMapCellInfo ReadCell(GdGridMap grid, int x, int y, int z)
        {
            var pos = new Vector3I(x, y, z);
            return new GridMapCellInfo
            {
                NodePath = grid.GetPath().ToString(),
                CellX = x,
                CellY = y,
                CellZ = z,
                Item = grid.GetCellItem(pos),
                Orientation = grid.GetCellItemOrientation(pos)
            };
        }
    }
}
#endif
