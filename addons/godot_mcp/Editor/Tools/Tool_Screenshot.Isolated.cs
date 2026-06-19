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
using System.Collections.Generic;
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
        public const string ScreenshotIsolatedToolId = "screenshot-isolated";

        [AiTool
        (
            ScreenshotIsolatedToolId,
            Title = "Screenshot / Isolated Node",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Render a target Node3D in ISOLATION (only the target is visible, in its own world) " +
            "from a chosen camera angle, framed automatically to its bounding box, lit by a default " +
            "directional light, and return the result as a PNG image for direct LLM inspection. Identify " +
            "the target with 'nodeRef'. Choose the view with 'cameraView' (Front/Back/Left/Right/Top/Bottom). " +
            "Pick a 'background' (SolidColor or Transparent) and, for SolidColor, a hex 'backgroundColor'. " +
            "The target is rendered via a temporary off-screen copy, so the edited scene is never mutated. " +
            "Output is a square 'resolution'x'resolution' PNG, capped to keep the response transportable. " +
            "NOTE: rendering needs a real GPU — a Godot launched with '--headless' yields an empty image, " +
            "surfaced here as a structured error rather than a blank PNG.")]
        public ResponseCallTool ScreenshotIsolated
        (
            [Description("Reference to the target Node3D to render in isolation (instanceId preferred, else path).")]
            NodeRef nodeRef,
            [Description("Camera angle relative to the target's bounding box. Default: Front.")]
            ScreenshotView cameraView = ScreenshotView.Front,
            [Description("Background mode for the render: SolidColor (default) or Transparent.")]
            ScreenshotBackground background = ScreenshotBackground.SolidColor,
            [Description("Hex background color (e.g. '#404040'). Used only when background is SolidColor.")]
            string backgroundColor = "#404040",
            [Description("Camera vertical field of view in degrees. Default: 60.")]
            float fieldOfView = 60f,
            [Description("Near clip plane distance. Default: 0.05.")]
            float nearClipPlane = 0.05f,
            [Description("Far clip plane distance. Default: 4000.")]
            float farClipPlane = 4000f,
            [Description("Framing multiplier around the object. 1.0 = tight fit, 1.5 = 50% extra space. Default: 1.2.")]
            float padding = 1.2f,
            [Description("Output image resolution in pixels (width = height). Default: 512.")]
            int resolution = 512
        )
        {
            if (resolution < ScreenshotMath.MinDimension || resolution > ScreenshotMath.MaxDimension)
                return ResponseCallTool.Error(
                    $"resolution must be between {ScreenshotMath.MinDimension} and {ScreenshotMath.MaxDimension}. Got {resolution}.");

            if (!ScreenshotMath.ValidateFraming(fieldOfView, nearClipPlane, farClipPlane, padding, out var framingError))
                return ResponseCallTool.Error(framingError!);

            // A single square view; clamp to the transport cap.
            (resolution, _) = ScreenshotMath.ClampToTransportLimit(resolution, resolution);

            var resolvedBackgroundColor = backgroundColor ?? "#404040";
            if (background == ScreenshotBackground.SolidColor &&
                !ScreenshotMath.TryParseHtmlColor(resolvedBackgroundColor, out _))
            {
                return ResponseCallTool.Error(
                    $"Invalid backgroundColor '{resolvedBackgroundColor}'. Expected '#RGB', '#RRGGBB', or '#RRGGBBAA'.");
            }

            return MainThread.Instance.Run(() =>
            {
                var node = Tool_Node.ResolveNode(nodeRef, out var resolveError);
                if (node == null)
                    return ResponseCallTool.Error(resolveError ?? $"Target node {nodeRef} not found.");

                if (node is not Node3D node3D)
                    return ResponseCallTool.Error(
                        $"Node {nodeRef} is a '{node.GetClass()}', not a Node3D. screenshot-isolated renders 3D nodes only.");

                return CaptureIsolated(node3D, cameraView, background, resolvedBackgroundColor,
                    fieldOfView, nearClipPlane, farClipPlane, padding, resolution);
            });
        }

        static ResponseCallTool CaptureIsolated(
            Node3D target,
            ScreenshotView view,
            ScreenshotBackground background,
            string backgroundColorHex,
            float fov,
            float near,
            float far,
            float padding,
            int resolution)
        {
            // Own-world SubViewport => only what we add below is visible: a copy of the target plus a light.
            // The live edited scene is never touched (we render a Duplicate, in a fresh World3D).
            var subViewport = new SubViewport
            {
                Size = new Vector2I(resolution, resolution),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                OwnWorld3D = true,
                TransparentBg = background == ScreenshotBackground.Transparent,
            };

            // The caller owns the SubViewport's lifetime: every construction step below (Duplicate, AABB
            // walk, the LookAtFromPosition calls — which THROW on a degenerate eye/target/up) can throw
            // before the render runs, so freeing must wrap construction too. RenderSubViewportToPng only
            // hosts/un-hosts; this finally QueueFree's the SubViewport and all temp children on every path.
            try
            {
                // The OwnWorld3D SubViewport otherwise clears to Godot's DEFAULT environment color, ignoring
                // the requested SolidColor hex. For the SolidColor path, install a WorldEnvironment whose
                // Environment clears the background to the parsed color so the produced PNG actually shows the
                // requested background. (Transparent stays driven by TransparentBg above and gets no
                // WorldEnvironment.) The WorldEnvironment is a child of the SubViewport, so
                // FreeOffscreenViewport's subtree QueueFree frees it along with the clone/camera/light.
                if (background == ScreenshotBackground.SolidColor &&
                    ScreenshotMath.TryParseHtmlColor(backgroundColorHex, out var bg))
                {
                    var worldEnvironment = new WorldEnvironment
                    {
                        Environment = new global::Godot.Environment
                        {
                            BackgroundMode = global::Godot.Environment.BGMode.Color,
                            BackgroundColor = new Color(bg.r, bg.g, bg.b, bg.a),
                        },
                    };
                    subViewport.AddChild(worldEnvironment);
                }

                // Duplicate so we can recenter at the origin without mutating the real node's transform.
                // Scripts are deliberately NOT duplicated: a static off-screen render needs no behavior, and
                // including them would run the clones' _EnterTree/_Ready on AddChild — observable side
                // effects that contradict this tool's ReadOnlyHint=true.
                var clone = (Node3D)target.Duplicate((int)(Node.DuplicateFlags.Signals | Node.DuplicateFlags.Groups));
                clone.Transform = Transform3D.Identity;
                subViewport.AddChild(clone);

                var bounds = ComputeAabb(clone);
                var radius = bounds.Size.Length() * 0.5f;
                var distance = ScreenshotMath.ComputeCameraDistance(radius, fov, padding);

                // Bracket the clip planes around the framed object so it never falls outside [near, far]
                // (which would render an empty/background-only image and report it as a successful capture).
                (near, far) = ScreenshotMath.BracketClipPlanes(distance, radius, near, far);

                var (dir, up) = ScreenshotMath.GetViewDirectionAndUp(view);
                var dirVec = new Vector3(dir.x, dir.y, dir.z);
                var upVec = new Vector3(up.x, up.y, up.z);
                var center = bounds.GetCenter();
                var camPos = center + dirVec * distance;

                var camera = new Camera3D
                {
                    Projection = Camera3D.ProjectionType.Perspective,
                    Fov = fov,
                    Near = near,
                    Far = far,
                    Current = true,
                };
                subViewport.AddChild(camera);
                camera.LookAtFromPosition(camPos, center, upVec);

                // Default lighting: a single directional light along the view direction so the visible face
                // is lit regardless of the scene's own lights (which are not present in the isolated world).
                var light = new DirectionalLight3D
                {
                    LightEnergy = 1.0f,
                    LightColor = Colors.White,
                };
                subViewport.AddChild(light);
                light.LookAtFromPosition(camPos, center, upVec);

                return RenderSubViewportToPng(subViewport,
                    caption: $"Isolated render of '{target.Name}' ({view}, {resolution}x{resolution}, background={background})");
            }
            finally
            {
                FreeOffscreenViewport(subViewport);
            }
        }

        // Aggregate the world-space AABB of every VisualInstance3D in the subtree (the clone's, already at
        // the origin). Falls back to a small unit box when the target has no visual geometry so the camera
        // still gets a sane finite framing distance.
        static Aabb ComputeAabb(Node3D root)
        {
            var initialised = false;
            var bounds = new Aabb();

            foreach (var vi in CollectVisualInstances(root))
            {
                var local = vi.GetAabb();
                // Transform the instance-local AABB into the root's space via the instance's global transform.
                var worldAabb = vi.GlobalTransform * local;
                if (!initialised)
                {
                    bounds = worldAabb;
                    initialised = true;
                }
                else
                {
                    bounds = bounds.Merge(worldAabb);
                }
            }

            if (!initialised || bounds.Size.LengthSquared() < 1e-8f)
                bounds = new Aabb(Vector3.Zero, Vector3.One * 0.1f);

            return bounds;
        }

        static IEnumerable<VisualInstance3D> CollectVisualInstances(Node node)
        {
            if (node is VisualInstance3D vi)
                yield return vi;
            foreach (var child in node.GetChildren(includeInternal: false))
            {
                foreach (var nested in CollectVisualInstances(child))
                    yield return nested;
            }
        }
    }
}
#endif
