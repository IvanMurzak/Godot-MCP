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
        /// Editor-only tool — creates a <c>NavigationAgent2D</c> or <c>NavigationAgent3D</c> node (a pathfinding
        /// + avoidance helper, typically a child of a moving body) under a parent node in the currently edited
        /// scene and returns its structured config. All Godot API access is marshalled onto the editor main
        /// thread via <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            AgentCreateToolId,
            Title = "Navigation / Agent Create"
        )]
        [Description("Create a NavigationAgent (a pathfinding + avoidance helper, usually a child of a moving " +
            "body) in the currently edited Godot scene and return its structured config. 'dimension' is '2D' " +
            "(NavigationAgent2D) or '3D' (NavigationAgent3D), default '3D'. Optionally pass 'parentPath' (a node " +
            "path relative to the scene root) to parent it under a specific node (defaults to the scene root) " +
            "and 'name' to rename it. Configure it afterwards with navigation-agent-configure.")]
        public NavigationNodeInfo AgentCreate
        (
            [Description("Navigation dimension: '2D' (NavigationAgent2D) or '3D' (NavigationAgent3D). Defaults to '3D'.")]
            string? dimension = null,
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent node to attach the agent " +
                "under. When omitted, the agent is parented to the scene root.")]
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
                    ? new NavigationAgent2D()
                    : new NavigationAgent3D();

                AttachToScene(root, parent, node, name);
                return ReadInfo(node);
            });
        }
    }
}
#endif
