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
        /// Editor-only tool — updates scalar properties of an existing <c>NavigationAgent2D</c>/<c>3D</c> node.
        /// Only the parameters you pass are changed; each numeric value is clamped to a valid range (pure-managed
        /// <see cref="NavigationDefaults"/> rules) before it touches the node. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            AgentConfigureToolId,
            Title = "Navigation / Agent Configure"
        )]
        [Description("Update scalar properties of an existing NavigationAgent2D/3D node, addressed by 'nodePath' " +
            "(relative to the edited scene root). Only the arguments you supply are changed; each numeric value " +
            "is clamped to a valid range (radius > 0, path/target desired distance >= 0, max speed >= 0). " +
            "Returns the agent's updated config.")]
        public NavigationNodeInfo AgentConfigure
        (
            [Description("Node path (relative to the edited scene root) of the NavigationAgent to configure.")]
            string nodePath,
            [Description("New avoidance radius (Godot 'radius'); clamped to > 0.")]
            float? radius = null,
            [Description("New distance at which the next path point counts as reached (Godot 'path_desired_distance'); clamped to >= 0.")]
            float? pathDesiredDistance = null,
            [Description("New distance at which the final target counts as reached (Godot 'target_desired_distance'); clamped to >= 0.")]
            float? targetDesiredDistance = null,
            [Description("New maximum movement speed used for avoidance (Godot 'max_speed'); clamped to >= 0.")]
            float? maxSpeed = null,
            [Description("New RVO avoidance toggle (Godot 'avoidance_enabled').")]
            bool? avoidanceEnabled = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var node = ResolveAgentOrThrow(nodePath);
                ApplyAgentConfig(node, radius, pathDesiredDistance, targetDesiredDistance, maxSpeed, avoidanceEnabled);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }
    }
}
#endif
