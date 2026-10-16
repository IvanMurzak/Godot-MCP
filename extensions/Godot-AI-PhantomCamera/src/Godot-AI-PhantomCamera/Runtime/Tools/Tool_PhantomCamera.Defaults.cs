/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    public partial class Tool_PhantomCamera
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is fully
        /// CI-unit-testable (see <c>Tool_PhantomCamera_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool phantomcamera-defaults</c>. Unlike the editor tools it does NOT require the
        /// Phantom Camera addon to be installed (it only recommends values), so it is the always-runnable,
        /// never-vacuous heart of the test paths. Returns the recommended starter configuration which the LLM
        /// can then pass to <c>phantomcamera-create</c> / <c>phantomcamera-set-*</c>.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "PhantomCamera / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (priority, follow mode, look-at mode, " +
            "damping) for a Godot Phantom Camera virtual camera. Pure-managed: touches no scene and does NOT " +
            "require the Phantom Camera addon to be installed, so it is safe to call any time to discover sane " +
            "defaults before creating or configuring a real PhantomCamera. 'dimension' accepts '2D' or '3D' " +
            "(default '3D') and only selects the reported camera class name.")]
        public PhantomCameraInfo Defaults
        (
            [Description("Camera dimension: '2D' (PhantomCamera2D) or '3D' (PhantomCamera3D). Defaults to '3D'. " +
                "Only affects the reported TypeName; the scalar starter values are identical for both.")]
            string? dimension = null
        )
        {
            return PhantomCameraDefaults.For(dimension);
        }
    }
}
