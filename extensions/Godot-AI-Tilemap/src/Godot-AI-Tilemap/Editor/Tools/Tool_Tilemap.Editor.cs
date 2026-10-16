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
using System.Collections.Generic;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// Editor-only shared helpers for the <c>tilemap-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The reads/writes use the strongly-typed <c>TileMapLayer</c> API on purpose — that typed surface (resolved
    /// from the consumer's own GodotSharp) is exactly what the source-only packaging recipe must compile against
    /// cross-version. <c>TileMapLayer</c> and its <c>SetCell</c>/<c>EraseCell</c>/<c>GetUsedCells</c>/<c>Clear</c>
    /// surface are stable across Godot 4.3 … 4.5.
    /// </para>
    /// </summary>
    public partial class Tool_Tilemap
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
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a <c>TileMapLayer</c>,
        /// throwing a clear error when the path is empty, the node is missing, or the node is not a
        /// <c>TileMapLayer</c>.
        /// </summary>
        static TileMapLayer ResolveTileMapLayerOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            var node = root.GetNodeOrNull(new NodePath(nodePath));
            if (node == null)
                throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (node is not TileMapLayer layer)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a TileMapLayer.", nameof(nodePath));

            return layer;
        }

        /// <summary>Read the used cells of a live <c>TileMapLayer</c> into pure-managed <see cref="TileCell"/>s.</summary>
        static List<TileCell> ReadUsedCells(TileMapLayer layer)
        {
            var cells = new List<TileCell>();
            foreach (Vector2I coords in layer.GetUsedCells())
            {
                var sourceId = layer.GetCellSourceId(coords);
                var atlas = layer.GetCellAtlasCoords(coords);
                cells.Add(new TileCell(coords.X, coords.Y, sourceId, atlas.X, atlas.Y));
            }
            return cells;
        }

        /// <summary>Build a pure-managed <see cref="TileMapLayerInfo"/> snapshot from a live layer.</summary>
        static TileMapLayerInfo ReadInfo(TileMapLayer layer)
        {
            var cells = ReadUsedCells(layer);
            return new TileMapLayerInfo
            {
                NodePath = layer.GetPath().ToString(),
                TypeName = layer.GetClass(),
                TileSetPath = layer.TileSet?.ResourcePath ?? string.Empty,
                UsedCellCount = cells.Count,
                UsedCells = cells
            };
        }
    }
}
#endif
