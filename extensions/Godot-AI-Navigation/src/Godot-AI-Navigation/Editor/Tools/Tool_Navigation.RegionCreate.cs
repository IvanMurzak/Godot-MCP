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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    public partial class Tool_Navigation
    {
        /// <summary>
        /// Editor-only tool — creates a <c>NavigationRegion2D</c> or <c>NavigationRegion3D</c> node (a navigable
        /// area) in the currently edited scene and returns its structured config. All Godot API access is
        /// marshalled onto the editor main thread via <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            RegionCreateToolId,
            Title = "Navigation / Region Create"
        )]
        [Description("Create a NavigationRegion (a navigable area) in the currently edited Godot scene and " +
            "return its structured config. 'dimension' is '2D' (NavigationRegion2D) or '3D' " +
            "(NavigationRegion3D), default '3D'. Optionally pass 'parentPath' (a node path relative to the scene " +
            "root) to parent it (defaults to the scene root) and 'name' to rename it. The new node's owner is " +
            "set to the scene root so it is saved with the scene. Assign its navigation polygon/mesh afterwards " +
            "with navigation-region-set-mesh.")]
        public NavigationNodeInfo RegionCreate
        (
            [Description("Navigation dimension: '2D' (NavigationRegion2D) or '3D' (NavigationRegion3D). Defaults to '3D'.")]
            string? dimension = null,
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null
        )
        {
            var dim = string.IsNullOrWhiteSpace(dimension)
                ? NavigationDimension.Three
                : NavigationDimensions.Parse(dimension);

            return MainThread.Instance.Run(() =>
            {
                var root = GetEditedSceneRootOrThrow();
                var parent = ResolveParentOrThrow(root, parentPath);

                Node node = dim == NavigationDimension.Two
                    ? new NavigationRegion2D()
                    : new NavigationRegion3D();

                AttachToScene(root, parent, node, name);
                return ReadInfo(node);
            });
        }
    }
}
#endif
