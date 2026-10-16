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

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    public partial class Tool_Terrain3D
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API and no addon needed, so it lives OUTSIDE <c>#if TOOLS</c> and
        /// is fully CI-unit-testable (see <c>Tool_Terrain3D_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool terrain3d-defaults</c>. Returns the recommended Terrain3D starter
        /// configuration (region size, mesh LODs, data directory, …), which the LLM can then pass to
        /// <c>terrain3d-create</c> / <c>terrain3d-set-region-size</c>. Unlike the editor tools it is NOT
        /// presence-gated — it gives advice and touches nothing.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "Terrain3D / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (region size, mesh LODs, mesh size, vertex " +
            "spacing, data directory) for a Godot Terrain3D node. Pure-managed: needs no scene and no addon, so " +
            "it is safe to call any time to discover sane defaults before creating a real terrain. 'preset' " +
            "accepts 'small' (region size 256), 'medium' (1024, the default) or 'large' (2048). Note: Terrain3D " +
            "persists region data to the returned 'DataDirectory' on disk, so that directory must be writable.")]
        public Terrain3DConfig Defaults
        (
            [Description("Detail preset: 'small' (region size 256), 'medium' (1024) or 'large' (2048). " +
                "Defaults to 'medium' when omitted.")]
            string? preset = null
        )
        {
            return Terrain3DDefaults.For(preset);
        }
    }
}
