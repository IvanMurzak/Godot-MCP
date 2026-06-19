/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.ComponentModel;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Camera angle (relative to a target node's bounding box) for the isolated render. The Godot analog
    /// of Unity-MCP's <c>Tool_Screenshot.CameraView</c>. Lives in the pure-managed helper (no Godot native
    /// types) so it can be referenced from the xUnit host.
    /// </summary>
    public enum ScreenshotView
    {
        [Description("Camera faces the object's front (-Z). Standard front view.")]
        Front,
        [Description("Camera faces the object's rear (+Z).")]
        Back,
        [Description("Camera faces the object's left (-X).")]
        Left,
        [Description("Camera faces the object's right (+X).")]
        Right,
        [Description("Camera looks down at the object from above (+Y).")]
        Top,
        [Description("Camera looks up at the object from below (-Y).")]
        Bottom,
    }

    /// <summary>
    /// Background fill for the isolated render's <c>SubViewport</c>.
    /// </summary>
    public enum ScreenshotBackground
    {
        [Description("Flat color defined by backgroundColor (hex string). SubViewport is opaque.")]
        SolidColor,
        [Description("Alpha-zero background for compositing (SubViewport.TransparentBg = true). PNG carries alpha.")]
        Transparent,
    }

    /// <summary>
    /// Pure-managed math/validation shared by the screenshot tool family. Extracted from the editor-only
    /// (<c>#if TOOLS</c>) handlers — mirroring <see cref="NodePathNormalizer"/> / <see cref="ResPathNormalizer"/>
    /// — so the transport-cap clamping, the bounds-to-camera-distance computation, the view-direction table,
    /// and the hex-color parse (the parts most prone to off-by-one / aspect-ratio bugs) are unit-testable in
    /// the plain xUnit host with NO live Godot rendering. None of this touches a Godot native type.
    /// </summary>
    public static class ScreenshotMath
    {
        /// <summary>
        /// Longest output edge allowed on any single screenshot, in pixels. Godot's <c>Image.SavePngToBuffer</c>
        /// PNG travels back to the AI as image content inside a JSON envelope over the McpPlugin transport,
        /// which caps a single message. An overly large capture produces a PNG that exceeds that cap and is
        /// dropped in transit. 3840 (true 4K) leaves comfortable headroom while staying well above what the
        /// model's vision actually consumes (images are downsampled to ~1568 px on the longest edge before the
        /// model sees them), so extra resolution past ~4K only inflates the payload. Mirrors the Unity plugin's
        /// <c>MaxScreenshotDimension</c>.
        /// </summary>
        public const int MaxScreenshotDimension = 3840;

        /// <summary>Smallest accepted output edge, in pixels.</summary>
        public const int MinDimension = 1;

        /// <summary>Largest dimension a caller may request before transport clamping kicks in.</summary>
        public const int MaxDimension = 16384;

        public const float MinFieldOfView = 1f;
        public const float MaxFieldOfView = 179f;
        public const float MinPadding = 0.01f;
        public const float MaxPadding = 100f;
        public const float MaxNearClip = 1000f;
        public const float MaxFarClip = 1e6f;

        /// <summary>
        /// Validate a requested width/height pair. Returns false and sets <paramref name="error"/> when either
        /// dimension is &lt; <see cref="MinDimension"/> or &gt; <see cref="MaxDimension"/>. Pure; no clamping.
        /// </summary>
        public static bool ValidateDimensions(int width, int height, out string? error)
        {
            if (width < MinDimension || height < MinDimension)
            {
                error = $"Width and height must be >= {MinDimension}. Got {width}x{height}.";
                return false;
            }
            if (width > MaxDimension || height > MaxDimension)
            {
                error = $"Width and height must be <= {MaxDimension} pixels. Got {width}x{height}.";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Scale (width, height) so the longest edge is at most <see cref="MaxScreenshotDimension"/>, preserving
        /// aspect ratio. Returns the input unchanged when already within the limit. Each axis floors at 1 px.
        /// Mirrors the Unity plugin's <c>ClampToTransportLimit</c>.
        /// </summary>
        public static (int width, int height) ClampToTransportLimit(int width, int height)
        {
            var longest = Math.Max(width, height);
            if (longest <= MaxScreenshotDimension)
                return (width, height);

            var scale = (float)MaxScreenshotDimension / longest;
            return (Math.Max(1, (int)Math.Round(width * scale)),
                    Math.Max(1, (int)Math.Round(height * scale)));
        }

        /// <summary>
        /// Validate the camera framing parameters for the isolated render. Returns false + an error string when
        /// any value is non-finite or out of range. Pure (no Godot types).
        /// </summary>
        public static bool ValidateFraming(float fieldOfView, float nearClip, float farClip, float padding, out string? error)
        {
            if (!IsFinite(fieldOfView) || fieldOfView < MinFieldOfView || fieldOfView > MaxFieldOfView)
            {
                error = $"fieldOfView must be finite and between {MinFieldOfView} and {MaxFieldOfView} degrees. Got {fieldOfView}.";
                return false;
            }
            if (!IsFinite(padding) || padding < MinPadding || padding > MaxPadding)
            {
                error = $"padding must be finite and between {MinPadding} and {MaxPadding}. Got {padding}.";
                return false;
            }
            if (!IsFinite(nearClip) || nearClip <= 0f || nearClip > MaxNearClip)
            {
                error = $"nearClipPlane must be finite and in (0, {MaxNearClip}]. Got {nearClip}.";
                return false;
            }
            if (!IsFinite(farClip) || farClip <= nearClip || farClip > MaxFarClip)
            {
                error = $"farClipPlane must be finite, > nearClipPlane ({nearClip}), and <= {MaxFarClip}. Got {farClip}.";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Unit view direction (the offset from the bounding-box center toward the camera) and the camera's
        /// up vector for a given <see cref="ScreenshotView"/>. Returned as plain (x,y,z) float triples so the
        /// table is testable without constructing Godot's <c>Vector3</c> (a struct, but kept out of the pure
        /// helper to avoid any GodotSharp dependency leaking into the math unit tests).
        /// </summary>
        public static ((float x, float y, float z) direction, (float x, float y, float z) up) GetViewDirectionAndUp(ScreenshotView view)
        {
            return view switch
            {
                ScreenshotView.Front  => ((0f, 0f, -1f), (0f, 1f, 0f)),
                ScreenshotView.Back   => ((0f, 0f, 1f),  (0f, 1f, 0f)),
                ScreenshotView.Left   => ((-1f, 0f, 0f), (0f, 1f, 0f)),
                ScreenshotView.Right  => ((1f, 0f, 0f),  (0f, 1f, 0f)),
                ScreenshotView.Top    => ((0f, 1f, 0f),  (0f, 0f, 1f)),
                ScreenshotView.Bottom => ((0f, -1f, 0f), (0f, 0f, 1f)),
                _                     => ((0f, 0f, -1f), (0f, 1f, 0f)),
            };
        }

        /// <summary>
        /// Distance the camera must sit from the bounding-box center so a sphere of the given
        /// <paramref name="boundsRadius"/> fits the vertical field of view, scaled by <paramref name="padding"/>
        /// (1.0 = tight fit, 1.5 = 50% extra margin). A near-zero radius floors at 0.05 so a point/empty target
        /// still yields a sane finite distance. Pure trig.
        /// </summary>
        public static float ComputeCameraDistance(float boundsRadius, float fieldOfViewDegrees, float padding)
        {
            if (boundsRadius < 0.0001f)
                boundsRadius = 0.05f;

            var fovRad = fieldOfViewDegrees * 0.5f * (float)(Math.PI / 180.0);
            var sin = (float)Math.Sin(fovRad);
            if (sin < 1e-6f)
                sin = 1e-6f;
            return (boundsRadius * padding) / sin;
        }

        /// <summary>
        /// Bracket the camera clip planes around the object the isolated render just framed, so the object's
        /// depth span — a sphere of <paramref name="boundsRadius"/> centered <paramref name="cameraDistance"/>
        /// in front of the camera — always lies inside [near, far]. Without this, a large object (or a small
        /// user-supplied far) can leave the framed target OUTSIDE the clip range, producing an empty /
        /// background-only PNG that the tool still reports as success.
        ///
        /// <para>
        /// The object spans depths [cameraDistance - r, cameraDistance + r] (with a small margin <c>k</c>).
        /// Near is shrunk toward — never below — that front face (floored positive so the perspective matrix
        /// stays valid); far is grown to at least clear the back face. The caller's near/far act as the
        /// tightest defaults: they are only loosened, never tightened, so an explicit wide range is honored.
        /// Pure (no Godot types) → unit-testable.
        /// </para>
        /// </summary>
        public static (float near, float far) BracketClipPlanes(float cameraDistance, float boundsRadius, float userNear, float userFar)
        {
            // Margin so the object's silhouette is not clipped exactly at the planes.
            const float k = 1.05f;
            var span = Math.Max(0f, boundsRadius) * k;

            // Front face of the object, floored to a small positive value (near must be > 0 for perspective).
            var frontFace = Math.Max(1e-4f, cameraDistance - span);
            // Back face of the object.
            var backFace = cameraDistance + span;

            // Only loosen the caller's planes: near may move closer (smaller), far may move farther (larger).
            var near = Math.Min(userNear, frontFace);
            var far = Math.Max(userFar, backFace);

            // Guarantee a valid, non-degenerate range even for pathological inputs.
            if (near <= 0f)
                near = 1e-4f;
            if (far <= near)
                far = near + Math.Max(span * 2f, 1e-3f);

            return (near, far);
        }

        /// <summary>
        /// Parse a '#RGB' / '#RRGGBB' / '#RRGGBBAA' hex color into normalized (r,g,b,a) floats in [0,1].
        /// Returns false on a malformed string. Pure — does NOT construct Godot's <c>Color</c> (which would pull
        /// a native dependency); the editor handler converts the tuple to a <c>Color</c>. A leading '#' is
        /// optional. Mirrors Godot's own '#'-hex acceptance without the native parse.
        /// </summary>
        public static bool TryParseHtmlColor(string? hex, out (float r, float g, float b, float a) color)
        {
            color = (0f, 0f, 0f, 1f);
            if (string.IsNullOrWhiteSpace(hex))
                return false;

            var s = hex!.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal))
                s = s.Substring(1);

            // Accept 3 (RGB), 6 (RRGGBB), or 8 (RRGGBBAA) hex digits.
            if (s.Length != 3 && s.Length != 6 && s.Length != 8)
                return false;

            foreach (var c in s)
            {
                var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            float r, g, b, a = 1f;
            if (s.Length == 3)
            {
                r = Hex1(s[0]);
                g = Hex1(s[1]);
                b = Hex1(s[2]);
            }
            else
            {
                r = Hex2(s, 0);
                g = Hex2(s, 2);
                b = Hex2(s, 4);
                if (s.Length == 8)
                    a = Hex2(s, 6);
            }

            color = (r, g, b, a);
            return true;
        }

        static float Hex1(char c)
        {
            var v = HexVal(c);
            // '#RGB' shorthand: each nibble is doubled (e.g. 'F' -> 0xFF).
            return (v * 16 + v) / 255f;
        }

        static float Hex2(string s, int i)
        {
            return (HexVal(s[i]) * 16 + HexVal(s[i + 1])) / 255f;
        }

        static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return c - 'A' + 10;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
