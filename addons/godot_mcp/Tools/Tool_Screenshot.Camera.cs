/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Screenshot
    {
        public const string ScreenshotCameraToolId = "screenshot-camera";

        [AiTool
        (
            ScreenshotCameraToolId,
            Title = "Screenshot / Camera",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Capture a screenshot from a specific Camera3D (or Camera2D) in the edited scene and " +
            "return it as a PNG image for direct LLM inspection. Identify the camera with 'nodeRef' " +
            "(instanceId preferred, else a scene-tree path). The camera's own world is rendered into a " +
            "temporary off-screen SubViewport at the requested 'width'x'height', so the editor's live " +
            "selection/camera is left untouched. Output edges are capped to keep the response transportable. " +
            "NOTE: rendering needs a real GPU — a Godot launched with '--headless' yields an empty image, " +
            "surfaced here as a structured error rather than a blank PNG.")]
        public ResponseCallTool ScreenshotCamera
        (
            [Description("Reference to the camera Node (Camera3D or Camera2D) to capture from " +
                "(instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("Output width in pixels. Default 1920. Must be > 0 and within the dimension cap.")]
            int width = 1920,
            [Description("Output height in pixels. Default 1080. Must be > 0 and within the dimension cap.")]
            int height = 1080
        )
        {
            if (!ScreenshotMath.ValidateDimensions(width, height, out var dimError))
                return ResponseCallTool.Error(dimError!);

            // Clamp the requested size to the transport limit up front so the off-screen SubViewport is
            // allocated at the final size (cheaper than rendering large then downscaling).
            (width, height) = ScreenshotMath.ClampToTransportLimit(width, height);

            return MainThread.Instance.Run(() =>
            {
                var node = Tool_Node.ResolveNode(nodeRef, out var resolveError);
                if (node == null)
                    return ResponseCallTool.Error(resolveError ?? $"Camera node {nodeRef} not found.");

                if (node is Camera3D camera3D)
                    return CaptureCamera3D(camera3D, width, height);
                if (node is Camera2D camera2D)
                    return CaptureCamera2D(camera2D, width, height);

                return ResponseCallTool.Error(
                    $"Node {nodeRef} is a '{node.GetClass()}', not a Camera3D or Camera2D.");
            });
        }

        // Render the camera's 3D world into an off-screen SubViewport via a temporary clone camera, so the
        // editor's own current camera / selection is never disturbed. MUST run on the main thread.
        static ResponseCallTool CaptureCamera3D(Camera3D source, int width, int height)
        {
            var subViewport = new SubViewport
            {
                Size = new Vector2I(width, height),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                // Share the source camera's world so the same scene content is visible.
                World3D = source.GetWorld3D(),
            };

            var cloneCamera = new Camera3D
            {
                GlobalTransform = source.GlobalTransform,
                Projection = source.Projection,
                Fov = source.Fov,
                Size = source.Size,
                Near = source.Near,
                Far = source.Far,
                KeepAspect = source.KeepAspect,
                Current = true,
            };
            subViewport.AddChild(cloneCamera);

            return RenderSubViewportToPng(subViewport,
                caption: $"Camera3D '{source.Name}' screenshot ({width}x{height})");
        }

        // Render the camera's 2D world into an off-screen SubViewport that shares World2D and applies the
        // source camera's transform as the SubViewport's canvas transform (so the same framing is captured).
        static ResponseCallTool CaptureCamera2D(Camera2D source, int width, int height)
        {
            var sourceViewport = source.GetViewport();
            var world2D = sourceViewport?.GetWorld2D();
            if (world2D == null)
                return ResponseCallTool.Error(
                    $"Camera2D '{source.Name}' is not inside a viewport with a World2D; cannot capture.");

            var subViewport = new SubViewport
            {
                Size = new Vector2I(width, height),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                World2D = world2D,
            };

            // Center the requested camera position in the output: translate the canvas so the camera's
            // global position maps to the middle of the output, scaled by the camera zoom.
            var zoom = source.Zoom;
            if (zoom.X == 0f) zoom.X = 1f;
            if (zoom.Y == 0f) zoom.Y = 1f;
            var center = source.GetTargetPosition();
            var canvasTransform = new Transform2D(0f, zoom, 0f,
                new Vector2(width, height) * 0.5f - center * zoom);
            subViewport.CanvasTransform = canvasTransform;

            return RenderSubViewportToPng(subViewport,
                caption: $"Camera2D '{source.Name}' screenshot ({width}x{height})");
        }

        // Shared off-screen render: parent the SubViewport into the editor tree (so it gets a rendering
        // device), force a draw, read back + encode, and always free the temporary nodes. MUST run on the
        // main thread.
        static ResponseCallTool RenderSubViewportToPng(SubViewport subViewport, string caption)
        {
            // The SubViewport needs a parent in the live tree to allocate a render target. The editor's
            // base control is a stable, always-present host; the SubViewport renders off-screen (it is not
            // a visible child of any editor panel) and is removed before the call returns.
            var host = EditorInterface.Singleton.GetBaseControl();
            if (host == null)
            {
                subViewport.QueueFree();
                return ResponseCallTool.Error("Editor base control is unavailable; cannot host the off-screen render.");
            }

            try
            {
                host.AddChild(subViewport);

                // The SubViewport renders into its target across the next draw. A SINGLE force-draw issued
                // immediately after AddChild can read back BEFORE the off-screen target has the scene content
                // (empirically: the first draw yields the clear color only). The SubViewport is created with
                // UpdateMode.Always, and two force-draws let the content land before the synchronous
                // read-back below.
                RenderingServer.ForceDraw();
                RenderingServer.ForceDraw();

                var png = EncodeViewportPng(subViewport, flipY: false, out var error);
                if (png == null)
                    return ResponseCallTool.Error(error ?? "Failed to read back the camera render.");

                return ResponseCallTool.Image(png, McpPlugin.Common.Consts.MimeType.ImagePng,
                    $"{caption} ({png.Length} bytes PNG)");
            }
            finally
            {
                if (subViewport.GetParent() != null)
                    subViewport.GetParent().RemoveChild(subViewport);
                subViewport.QueueFree();
            }
        }
    }
}
#endif
