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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    public partial class Tool_PhantomCamera
    {
        /// <summary>
        /// Editor-only tool — sets the <c>priority</c> of an existing PhantomCamera (the addon activates the
        /// highest-priority camera with a matching host). Presence-gated (Class B), main-thread marshalled.
        /// </summary>
        [AiTool
        (
            SetPriorityToolId,
            Title = "PhantomCamera / Set Priority"
        )]
        [Description("Set the 'priority' of an existing PhantomCamera3D, addressed by 'nodePath' (relative to " +
            "the edited scene root). The Phantom Camera addon activates the highest-priority camera that has a " +
            "matching PhantomCameraHost, so raising priority is how you switch the active camera. Returns the " +
            "camera's updated config, or installed:false when the Phantom Camera addon is absent.")]
        public PhantomCameraInfo SetPriority
        (
            [Description("Node path (relative to the edited scene root) of the PhantomCamera3D to modify.")]
            string nodePath,
            [Description("New priority value. Higher wins when multiple PhantomCameras are active.")]
            int priority
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // PRESENCE GATE — mandatory first line of every editor tool.
                if (!AddonInstalled())
                    return PhantomCameraInfo.NotInstalled();

                var root = GetEditedSceneRootOrThrow();
                var pcam = ResolvePhantomCameraOrThrow(nodePath, root);

                pcam.Set(PhantomCameraNames.Priority, priority);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(pcam);
            });
        }
    }
}
#endif
