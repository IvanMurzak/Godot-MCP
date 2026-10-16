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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    public partial class Tool_Navigation
    {
        /// <summary>
        /// Editor-only, read-only tool — reads the scalar config of an existing navigation node (any of
        /// <c>NavigationRegion2D/3D</c>, <c>NavigationAgent2D/3D</c>, <c>NavigationLink2D/3D</c>). The returned
        /// <see cref="NavigationNodeInfo.Kind"/> tells you which fields are meaningful. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "Navigation / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read the scalar config of an existing navigation node — a NavigationRegion2D/3D, " +
            "NavigationAgent2D/3D, or NavigationLink2D/3D — addressed by 'nodePath' (relative to the edited " +
            "scene root). The returned 'Kind' (Region/Agent/Link) tells you which fields are meaningful: Region " +
            "exposes Enabled + ResourcePath/ResourceType; Agent exposes Radius + the desired distances + max " +
            "speed + AvoidanceEnabled; Link exposes Enabled + start/end positions + Bidirectional. Read-only: " +
            "does not modify the scene.")]
        public NavigationNodeInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the navigation node to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() => ReadInfo(ResolveNavigationNodeOrThrow(nodePath)));
        }
    }
}
#endif
