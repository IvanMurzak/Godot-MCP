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
        /// Editor-only tool — ensures a <c>PhantomCameraHost</c> exists under a <c>Camera3D</c> in the edited
        /// scene. The host is the companion node that actually drives the real camera; WITHOUT it a
        /// PhantomCamera does nothing, so this is normally the first call. Presence-gated (Class B), main-thread
        /// marshalled. If no <c>Camera3D</c> is found (and none is supplied) a new one is created under the
        /// scene root so the host has a valid parent.
        /// </summary>
        [AiTool
        (
            HostCreateToolId,
            Title = "PhantomCamera / Host Create"
        )]
        [Description("Ensure a PhantomCameraHost exists under a Camera3D in the currently edited Godot scene " +
            "(required by the Phantom Camera addon — without a host, PhantomCameras do nothing). Pass " +
            "'cameraPath' (a node path relative to the scene root) to target a specific Camera3D; when omitted " +
            "the first Camera3D in the scene is used, or a new one is created under the scene root. Returns the " +
            "host's path and the camera it was attached to, or a structured installed:false result when the " +
            "Phantom Camera addon is not installed.")]
        public PhantomCameraInfo HostCreate
        (
            [Description("Optional node path (relative to the edited scene root) of the Camera3D to attach the " +
                "host to. When omitted, the first Camera3D in the scene is used, or one is created.")]
            string? cameraPath = null,
            [Description("Optional name for the Camera3D when one must be created. Defaults to 'Camera3D'.")]
            string? cameraName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // PRESENCE GATE — mandatory first line of every editor tool.
                if (!AddonInstalled())
                    return PhantomCameraInfo.NotInstalled();

                var root = GetEditedSceneRootOrThrow();

                Camera3D? camera;
                if (!string.IsNullOrWhiteSpace(cameraPath))
                {
                    camera = root.GetNodeOrNull<Camera3D>(new NodePath(cameraPath))
                        ?? throw new ArgumentException(
                            $"No Camera3D found at path '{cameraPath}'.", nameof(cameraPath));
                }
                else
                {
                    camera = FindFirstCamera3D(root);
                    if (camera == null)
                    {
                        camera = new Camera3D
                        {
                            Name = string.IsNullOrWhiteSpace(cameraName) ? "Camera3D" : cameraName
                        };
                        root.AddChild(camera);
                        camera.Owner = root;
                    }
                }

                var host = FindHostChild(camera);
                if (host == null)
                {
                    host = AddonInterop.InstantiateScriptNode(PhantomCameraNames.PhantomCameraHost)
                        ?? throw new InvalidOperationException(
                            "Failed to instantiate PhantomCameraHost from the installed Phantom Camera addon.");
                    host.Name = PhantomCameraNames.PhantomCameraHost;
                    camera.AddChild(host);
                    host.Owner = root; // so the host is persisted when the scene is saved
                }

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(host);

                return new PhantomCameraInfo
                {
                    Installed = true,
                    Addon = AddonGate.PhantomCameraAddon,
                    NodePath = host.GetPath().ToString(),
                    TypeName = PhantomCameraNames.PhantomCameraHost,
                    CameraPath = camera.GetPath().ToString()
                };
            });
        }
    }
}
#endif
