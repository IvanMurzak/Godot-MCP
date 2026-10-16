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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    public partial class Tool_PhantomCamera
    {
        /// <summary>
        /// Editor-only tool — creates a <c>PhantomCamera3D</c> node (the addon's GDScript <c>class_name</c>
        /// type, instantiated BY NAME via <see cref="AddonInterop"/>) in the currently edited scene and returns
        /// its structured config. Presence-gated (Class B), main-thread marshalled. Pair with
        /// <c>phantomcamera-host-create</c> — a PhantomCamera needs a host to drive the real camera.
        /// </summary>
        [AiTool
        (
            CreateToolId,
            Title = "PhantomCamera / Create"
        )]
        [Description("Create a PhantomCamera3D node (a Cinemachine-style virtual camera from the Phantom Camera " +
            "addon) in the currently edited Godot scene and return its structured config. Optionally pass " +
            "'name' to rename it, 'parentPath' (a node path relative to the scene root) to parent it (defaults " +
            "to the scene root), and 'priority' to seed its priority (higher wins). The new node's owner is set " +
            "to the scene root so it is saved with the scene. Returns a structured installed:false result when " +
            "the Phantom Camera addon is not installed.")]
        public PhantomCameraInfo Create
        (
            [Description("Name for the new PhantomCamera3D node. When omitted, Godot's default name is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Optional initial priority (Phantom Camera 'priority'); higher wins when multiple " +
                "PhantomCameras are active. Defaults to the addon's default (0).")]
            int? priority = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // PRESENCE GATE — mandatory first line of every editor tool.
                if (!AddonInstalled())
                    return PhantomCameraInfo.NotInstalled();

                var root = GetEditedSceneRootOrThrow();

                Node parent = root;
                if (!string.IsNullOrWhiteSpace(parentPath))
                    parent = root.GetNodeOrNull(new NodePath(parentPath))
                        ?? throw new ArgumentException(
                            $"No parent node found at path '{parentPath}'.", nameof(parentPath));

                var pcam = AddonInterop.InstantiateScriptNode(PhantomCameraNames.PhantomCamera3D)
                    ?? throw new InvalidOperationException(
                        "Failed to instantiate PhantomCamera3D from the installed Phantom Camera addon.");

                if (!string.IsNullOrWhiteSpace(name))
                    pcam.Name = name;

                parent.AddChild(pcam);
                pcam.Owner = root; // so the node is persisted when the scene is saved

                // Set priority AFTER AddChild so the addon node has entered the tree (its _enter_tree wires the
                // PhantomCameraManager the priority setter notifies).
                if (priority.HasValue)
                    pcam.Set(PhantomCameraNames.Priority, priority.Value);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(pcam);

                return ReadInfo(pcam);
            });
        }
    }
}
#endif
