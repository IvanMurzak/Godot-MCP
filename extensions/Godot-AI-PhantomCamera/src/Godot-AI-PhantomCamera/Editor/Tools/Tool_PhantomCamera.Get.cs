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
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using System.ComponentModel;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    public partial class Tool_PhantomCamera
    {
        /// <summary>
        /// Editor-only, read-only tool — reads the scalar config (priority, follow/look-at mode + targets,
        /// damping) of an existing PhantomCamera node, driven dynamically by name. Presence-gated (Class B),
        /// main-thread marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "PhantomCamera / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read the scalar config (priority, follow mode, follow target, look-at mode, look-at " +
            "target, damping flags) of an existing PhantomCamera3D node, addressed by 'nodePath' (relative to " +
            "the edited scene root). Read-only: does not modify the scene. Returns a structured installed:false " +
            "result when the Phantom Camera addon is not installed.")]
        public PhantomCameraInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the PhantomCamera3D to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // PRESENCE GATE — mandatory first line of every editor tool.
                if (!AddonInstalled())
                    return PhantomCameraInfo.NotInstalled();

                var root = GetEditedSceneRootOrThrow();
                return ReadInfo(ResolvePhantomCameraOrThrow(nodePath, root));
            });
        }
    }
}
#endif
