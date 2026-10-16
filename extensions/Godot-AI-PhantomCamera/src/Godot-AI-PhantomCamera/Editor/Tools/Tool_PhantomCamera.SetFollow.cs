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
        /// Editor-only tool — sets the follow mode and/or follow target of an existing PhantomCamera. Driven
        /// dynamically (<c>follow_target</c> set FIRST so the <c>follow_mode</c> setter sees a valid target).
        /// Presence-gated (Class B), main-thread marshalled.
        /// </summary>
        [AiTool
        (
            SetFollowToolId,
            Title = "PhantomCamera / Set Follow"
        )]
        [Description("Set how an existing PhantomCamera3D follows: its follow mode and/or its follow target. " +
            "Address the camera by 'nodePath' (relative to the edited scene root). 'followMode' is one of " +
            "None/Glued/Simple/Group/Path/Framed/ThirdPerson (case-insensitive); 'targetPath' is a node path " +
            "(relative to the scene root) of a Node3D to follow. Only the arguments you supply are changed. " +
            "Returns the camera's updated config, or installed:false when the Phantom Camera addon is absent.")]
        public PhantomCameraInfo SetFollow
        (
            [Description("Node path (relative to the edited scene root) of the PhantomCamera3D to modify.")]
            string nodePath,
            [Description("Follow mode: None, Glued, Simple, Group, Path, Framed, or ThirdPerson " +
                "(case-insensitive). When omitted, the mode is left unchanged.")]
            string? followMode = null,
            [Description("Node path (relative to the edited scene root) of the follow target (a Node3D). When " +
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

                // Set the target BEFORE the mode so the follow_mode setter recomputes against a valid target.
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    var target = root.GetNodeOrNull(new NodePath(targetPath))
                        ?? throw new System.ArgumentException(
                            $"No follow-target node found at path '{targetPath}'.", nameof(targetPath));
                    pcam.Set(PhantomCameraNames.FollowTarget, target);
                }

                if (!string.IsNullOrWhiteSpace(followMode))
                    pcam.Set(PhantomCameraNames.FollowMode, PhantomCameraModes.ParseFollow(followMode).ToInt());

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(pcam);
            });
        }
    }
}
#endif
