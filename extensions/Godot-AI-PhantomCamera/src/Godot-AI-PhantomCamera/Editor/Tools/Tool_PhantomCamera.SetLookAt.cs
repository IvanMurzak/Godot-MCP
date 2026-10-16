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
        /// Editor-only tool — sets the look-at mode and/or look-at target of an existing PhantomCamera3D
        /// (3D only). Driven dynamically (<c>look_at_target</c> set FIRST so the <c>look_at_mode</c> setter sees
        /// a valid target). Presence-gated (Class B), main-thread marshalled.
        /// </summary>
        [AiTool
        (
            SetLookAtToolId,
            Title = "PhantomCamera / Set Look At"
        )]
        [Description("Set how an existing PhantomCamera3D looks at a target: its look-at mode and/or its " +
            "look-at target. Address the camera by 'nodePath' (relative to the edited scene root). 'lookAtMode' " +
            "is one of None/Mimic/Simple/Group (case-insensitive); 'targetPath' is a node path (relative to the " +
            "scene root) of a Node3D to look at. Only the arguments you supply are changed. Returns the camera's " +
            "updated config, or installed:false when the Phantom Camera addon is absent.")]
        public PhantomCameraInfo SetLookAt
        (
            [Description("Node path (relative to the edited scene root) of the PhantomCamera3D to modify.")]
            string nodePath,
            [Description("Look-at mode: None, Mimic, Simple, or Group (case-insensitive). When omitted, the " +
                "mode is left unchanged.")]
            string? lookAtMode = null,
            [Description("Node path (relative to the edited scene root) of the look-at target (a Node3D). When " +
                "omitted, the target is left unchanged.")]
            string? targetPath = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // PRESENCE GATE — mandatory first line of every editor tool.
                if (!AddonInstalled())
                    return PhantomCameraInfo.NotInstalled();

                var root = GetEditedSceneRootOrThrow();
                var pcam = ResolvePhantomCameraOrThrow(nodePath, root);

                // Set the target BEFORE the mode so the look_at_mode setter recomputes against a valid target.
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    var target = root.GetNodeOrNull(new NodePath(targetPath))
                        ?? throw new System.ArgumentException(
                            $"No look-at-target node found at path '{targetPath}'.", nameof(targetPath));
                    pcam.Set(PhantomCameraNames.LookAtTarget, target);
                }

                if (!string.IsNullOrWhiteSpace(lookAtMode))
                    pcam.Set(PhantomCameraNames.LookAtMode, PhantomCameraModes.ParseLookAt(lookAtMode).ToInt());

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(pcam);
            });
        }
    }
}
#endif
