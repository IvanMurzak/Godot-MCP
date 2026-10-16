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

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    public partial class Tool_Terrain3D
    {
        /// <summary>
        /// Editor-only tool — creates a <c>Terrain3D</c> node (resolved by string name through <c>ClassDB</c>)
        /// in the currently edited scene and returns its structured config. PRESENCE-GATED: when the Terrain3D
        /// addon is absent it returns a structured <c>Installed: false</c> result instead of crashing. All Godot
        /// API access is marshalled onto the editor main thread via <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            CreateToolId,
            Title = "Terrain3D / Create"
        )]
        [Description("Create a Terrain3D node in the currently edited Godot scene and return its structured " +
            "config. Requires the Terrain3D GDExtension addon (returns 'Installed: false' with an install hint " +
            "when absent). Optionally pass 'name' to rename it, 'parentPath' (a node path relative to the scene " +
            "root) to parent it (defaults to the scene root), 'regionSize' to seed the region size (snapped to a " +
            "valid Terrain3D size: 64/128/256/512/1024/2048), and 'dataDirectory' (a res:// path) to seed where " +
            "the terrain persists region data on disk. The new node's owner is set to the scene root so it is " +
            "saved with the scene.")]
        public Terrain3DNodeInfo Create
        (
            [Description("Name for the new Terrain3D node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Optional initial region size; snapped to a valid Terrain3D RegionSize " +
                "(64/128/256/512/1024/2048). When omitted, the addon default (256) is kept.")]
            int? regionSize = null,
            [Description("Optional initial res:// data directory where Terrain3D persists region data (must be " +
                "writable). When omitted, no data directory is set.")]
            string? dataDirectory = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.NativeClassExists(AddonGate.PresenceClass))
                    return Terrain3DNodeInfo.FromGate(AddonGate.NotInstalled());

                var root = GetEditedSceneRootOrThrow();

                Node parent = root;
                if (!string.IsNullOrWhiteSpace(parentPath))
                    parent = root.GetNodeOrNull(new NodePath(parentPath))
                        ?? throw new ArgumentException($"No parent node found at path '{parentPath}'.", nameof(parentPath));

                var node = AddonInterop.InstantiateNativeNode(Terrain3DNames.TerrainClass)
                    ?? throw new InvalidOperationException(
                        $"Failed to instantiate '{Terrain3DNames.TerrainClass}' via ClassDB even though the class exists.");

                if (!string.IsNullOrWhiteSpace(name))
                    node.Name = name;

                parent.AddChild(node);
                node.Owner = root; // so the node is persisted when the scene is saved

                // Seed config AFTER the node is in the tree (its Terrain3DData is initialized on _enter_tree).
                if (regionSize.HasValue)
                    node.Call(Terrain3DNames.ChangeRegionSizeMethod, RegionSizes.Snap(regionSize.Value));
                if (!string.IsNullOrWhiteSpace(dataDirectory))
                    node.Set(Terrain3DNames.DataDirectoryProperty, dataDirectory);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(node);

                return ReadInfo(node);
            });
        }
    }
}
#endif
