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
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Screenshot tool family (<c>screenshot-*</c>) — the Godot analog of Unity-MCP's
    /// <c>Tool_Screenshot</c>. Captures the editor viewport, a specific camera, or an isolated node render
    /// and returns the result as a PNG image the LLM can inspect directly.
    ///
    /// <para>
    /// Each tool method lives in its own partial-class file (Viewport / Camera / Isolated) and drives the
    /// Godot rendering API on the editor main thread via the dispatcher. PNG bytes travel back through the
    /// SAME McpPlugin image-content path Unity uses — <see cref="McpPlugin.Common.Model.ResponseCallTool.Image"/>
    /// with the <c>image/png</c> MIME type — so the framework's structured image envelope carries the
    /// capture; we never hand-roll a base64 string field.
    /// </para>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): every handler touches <see cref="EditorInterface"/>, live
    /// <see cref="Viewport"/>/<see cref="Camera3D"/>/<see cref="SubViewport"/> objects, and the
    /// <see cref="RenderingServer"/> — none of which exists in a plain (non-editor) build. The pure-managed
    /// pieces (size-cap clamping, the bounds→distance trig, the view-direction table, hex-color parse) live
    /// in <see cref="ScreenshotMath"/> outside this guard and are unit-tested. The image-encoding of a real
    /// <see cref="Image"/> needs the Godot runtime, so it is verified by the headless/windowed live smoke,
    /// not by xUnit (see the profile's <c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Screenshot
    {
        /// <summary>
        /// Read a viewport's color buffer back to CPU, optionally correct the GPU Y-flip, resize to the
        /// transport cap if needed, and encode a PNG. MUST be called on the editor main thread. Returns the
        /// PNG bytes, or null + an error message when the viewport texture has no GPU-rendered image yet
        /// (the headless / no-GPU case — see the family doc).
        ///
        /// <para>
        /// Godot's <see cref="ViewportTexture"/> is GPU-resident; <see cref="Texture2D.GetImage"/> performs
        /// the CPU read-back. On a GPU-rendered target the rows arrive already upright (Godot's
        /// <c>GetImage</c> accounts for the API origin), so <paramref name="flipY"/> defaults to false; pass
        /// true only for a source you have empirically confirmed comes back inverted.
        /// </para>
        /// </summary>
        internal static byte[]? EncodeViewportPng(Viewport viewport, bool flipY, out string? error)
        {
            error = null;

            var texture = viewport.GetTexture();
            if (texture == null)
            {
                error = "Viewport has no texture (the viewport is not rendering to a target).";
                return null;
            }

            var image = texture.GetImage();
            if (image == null || image.IsEmpty() || image.GetWidth() <= 0 || image.GetHeight() <= 0)
            {
                error = "Viewport texture read back an empty image — the viewport produced no GPU render " +
                    "(common under '--headless', which has no rendering device). Run the capture in a " +
                    "windowed editor with a real GPU.";
                return null;
            }

            if (flipY)
                image.FlipY();

            ResizeToTransportLimit(image);

            return EncodePng(image, out error);
        }

        static byte[]? EncodePng(Image image, out string? error)
        {
            error = null;
            var bytes = image.SavePngToBuffer();
            if (bytes == null || bytes.Length == 0)
            {
                error = "PNG encode produced no bytes.";
                return null;
            }
            return bytes;
        }

        /// <summary>
        /// Scale the image in place so its longest edge is within <see cref="ScreenshotMath.MaxScreenshotDimension"/>,
        /// preserving aspect ratio. No-op when already within the limit.
        /// </summary>
        static void ResizeToTransportLimit(Image image)
        {
            var (w, h) = ScreenshotMath.ClampToTransportLimit(image.GetWidth(), image.GetHeight());
            if (w != image.GetWidth() || h != image.GetHeight())
                image.Resize(w, h, Image.Interpolation.Bilinear);
        }
    }
}
#endif
