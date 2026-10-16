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
        /// Editor-only tool — creates a <c>NavigationLink2D</c> or <c>NavigationLink3D</c> node (an off-mesh
        /// connection between two points) in the currently edited scene, with the given start/end positions, and
        /// returns its structured config. All Godot API access is marshalled onto the editor main thread via
        /// <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            LinkCreateToolId,
            Title = "Navigation / Link Create"
        )]
        [Description("Create a NavigationLink (an off-mesh connection between two points, e.g. a jump or a " +
            "ladder) in the currently edited Godot scene and return its structured config. 'dimension' is '2D' " +
            "(NavigationLink2D, uses X/Y) or '3D' (NavigationLink3D, uses X/Y/Z), default '3D'. Provide the " +
            "start and end positions via 'startX/startY/startZ' and 'endX/endY/endZ' (Z is ignored in 2D). " +
            "Optionally pass 'parentPath' and 'name'. The new node's owner is set to the scene root so it is " +
            "saved with the scene.")]
        public NavigationNodeInfo LinkCreate
        (
            [Description("Navigation dimension: '2D' (NavigationLink2D) or '3D' (NavigationLink3D). Defaults to '3D'.")]
            string? dimension = null,
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Link start position X (Godot 'start_position'). Defaults to 0.")]
            double startX = 0,
            [Description("Link start position Y (Godot 'start_position'). Defaults to 0.")]
            double startY = 0,
            [Description("Link start position Z (Godot 'start_position', 3D only; ignored in 2D). Defaults to 0.")]
            double startZ = 0,
            [Description("Link end position X (Godot 'end_position'). Defaults to 0.")]
            double endX = 0,
            [Description("Link end position Y (Godot 'end_position'). Defaults to 0.")]
            double endY = 0,
            [Description("Link end position Z (Godot 'end_position', 3D only; ignored in 2D). Defaults to 0.")]
            double endZ = 0
        )
        {
            var dim = string.IsNullOrWhiteSpace(dimension)
                ? NavigationDimension.Three
                : NavigationDimensions.Parse(dimension);

            return MainThread.Instance.Run(() =>
            {
                var root = GetEditedSceneRootOrThrow();
                var parent = ResolveParentOrThrow(root, parentPath);

                Node node;
                if (dim == NavigationDimension.Two)
                {
                    node = new NavigationLink2D
                    {
                        StartPosition = new Vector2((float)startX, (float)startY),
                        EndPosition = new Vector2((float)endX, (float)endY)
                    };
                }
                else
                {
                    node = new NavigationLink3D
                    {
                        StartPosition = new Vector3((float)startX, (float)startY, (float)startZ),
                        EndPosition = new Vector3((float)endX, (float)endY, (float)endZ)
                    };
                }

                AttachToScene(root, parent, node, name);
                return ReadInfo(node);
            });
        }
    }
}
#endif
