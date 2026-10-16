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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    public partial class Tool_Navigation
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is fully
        /// CI-unit-testable (see <c>Tool_Navigation_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool navigation-defaults</c>. Returns the recommended starter configuration for a
        /// 2D or 3D <c>NavigationAgent</c> (radius, desired distances, max speed), which the LLM can then pass
        /// to <c>navigation-agent-configure</c>. The sane values differ by dimension (2D works in pixels, 3D in
        /// metres), mirroring Godot's own per-dimension defaults.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "Navigation / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (radius, path/target desired distance, max " +
            "speed) for a Godot NavigationAgent of the requested dimension. Pure-managed: touches no scene, so " +
            "it is safe to call any time to discover sane defaults before creating or configuring a real agent. " +
            "The defaults differ by dimension (2D works in pixels, 3D in metres). 'dimension' accepts '2D' or " +
            "'3D' (default '3D').")]
        public NavigationNodeInfo Defaults
        (
            [Description("Navigation dimension: '2D' (NavigationAgent2D) or '3D' (NavigationAgent3D). Defaults to '3D'.")]
            string? dimension = null
        )
        {
            var dim = string.IsNullOrWhiteSpace(dimension)
                ? NavigationDimension.Three
                : NavigationDimensions.Parse(dimension);
            return NavigationDefaults.ForAgent(dim);
        }
    }
}
