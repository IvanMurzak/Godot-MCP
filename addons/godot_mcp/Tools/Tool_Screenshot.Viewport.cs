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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Screenshot
    {
        public const string ScreenshotViewportToolId = "screenshot-viewport";

        [AiTool
        (
            ScreenshotViewportToolId,
            Title = "Screenshot / Editor Viewport",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Capture the Godot editor's main viewport (the 3D or 2D editing surface) and return it " +
            "as a PNG image for direct LLM inspection. Set 'mode' to '3d' (default) for the 3D editor " +
            "viewport or '2d' for the 2D editor viewport. The longest output edge is capped to keep the " +
            "response transportable; oversized captures are scaled down (aspect preserved). NOTE: the " +
            "viewport must have a live GPU render — a Godot launched with '--headless' has no rendering " +
            "device and yields an empty image, which surfaces here as a structured error rather than a " +
            "blank PNG.")]
        public ResponseCallTool ScreenshotViewport
        (
            [Description("Which editor viewport to capture: '3d' (the 3D editor surface, default) or '2d' " +
                "(the 2D editor surface). Case-insensitive.")]
            string mode = "3d"
        )
        {
            var normalizedMode = (mode ?? "3d").Trim().ToLowerInvariant();
            if (normalizedMode != "3d" && normalizedMode != "2d")
                return ResponseCallTool.Error($"mode must be '3d' or '2d'. Got '{mode}'.");

            return MainThread.Instance.Run(() =>
            {
                // GetEditorViewport3D/2D return the editor's SubViewport (a Viewport). On a GPU-backed
                // editor these hold the live editing render; under --headless they read back empty, which
                // EncodeViewportPng reports as a structured error.
                Viewport? viewport = normalizedMode == "2d"
                    ? EditorInterface.Singleton.GetEditorViewport2D()
                    : EditorInterface.Singleton.GetEditorViewport3D(0);

                if (viewport == null)
                    return ResponseCallTool.Error($"Editor {normalizedMode} viewport is unavailable.");

                var png = EncodeViewportPng(viewport, flipY: false, out var error);
                if (png == null)
                    return ResponseCallTool.Error(error ?? "Failed to capture the editor viewport.");

                return ResponseCallTool.Image(png, McpPlugin.Common.Consts.MimeType.ImagePng,
                    $"Editor {normalizedMode} viewport screenshot ({png.Length} bytes PNG)");
            });
        }
    }
}
#endif
