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

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    public partial class Tool_Dialogic
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API and no addon needed, so it lives OUTSIDE <c>#if TOOLS</c> and
        /// is fully CI-unit-testable (see <c>Tool_Dialogic_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool dialogic-defaults</c>. Returns the recommended starter Dialogic timeline + character
        /// config, which the LLM can then pass to <c>dialogic-timeline-create</c> / <c>dialogic-character-create</c>.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "Dialogic / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration for Dialogic authoring: a 2-line starter " +
            "timeline (plain .dtl text) and a named, colored starter character. Pure-managed: needs no scene " +
            "and no installed addon, so it is safe to call any time to discover sane defaults before creating " +
            "real timeline/character resources. 'kind' optionally narrows the result: 'timeline' or 'character' " +
            "(default: both).")]
        public DialogicResourceInfo Defaults
        (
            [Description("Optional filter: 'timeline' (only the starter timeline), 'character' (only the starter " +
                "character), or omit for both.")]
            string? kind = null
        )
        {
            return DialogicDefaults.For(kind);
        }
    }
}
