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

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is fully
        /// CI-unit-testable (see <c>Tool_Animation_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool animation-defaults</c>. Returns the recommended starter configuration for a new
        /// animation, which the LLM can then pass to <c>animation-create</c>.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "Animation / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (length, loop mode, no tracks) for a new " +
            "Godot Animation resource. Pure-managed: touches no scene, so it is safe to call any time to " +
            "discover sane defaults before creating a real animation with 'animation-create'. 'loopMode' " +
            "accepts 'none', 'linear' or 'pingpong' (default 'none').")]
        public AnimationClipInfo Defaults
        (
            [Description("Loop mode for the starter config: 'none', 'linear' or 'pingpong'. Defaults to 'none'.")]
            string? loopMode = null
        )
        {
            var mode = string.IsNullOrWhiteSpace(loopMode)
                ? AnimationDefaults.DefaultLoopMode
                : AnimationLoopModes.Parse(loopMode);
            return AnimationDefaults.For(mode);
        }
    }
}
