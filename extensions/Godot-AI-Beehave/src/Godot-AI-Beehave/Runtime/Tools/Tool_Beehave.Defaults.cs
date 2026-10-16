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

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    public partial class Tool_Beehave
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API and no addon, so it lives OUTSIDE <c>#if TOOLS</c>, is fully
        /// CI-unit-testable (see <c>Tool_Beehave_DefaultsTests</c>), and E2E-verifiable via
        /// <c>godot-cli run-tool beehave-defaults</c>. Returns a recommended behaviour-tree SKELETON the LLM can
        /// then build with <c>beehave-tree-create</c> / <c>-add-composite</c> / <c>-add-leaf</c>. Because it
        /// needs no addon, it is the always-available entry point even before Beehave is installed.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "Beehave / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter SKELETON for a Beehave behaviour tree (root class, " +
            "tick rate, a top-level composite, and the leaf placeholders to fill in). Pure-managed: touches " +
            "no scene and needs no addon installed, so it is safe to call any time to discover a sane tree " +
            "shape before scaffolding the real nodes. 'compact' (default false) is reserved for trimming the " +
            "guidance note.")]
        public BeehaveSkeleton Defaults
        (
            [Description("When true, omit the long guidance note from the result. Defaults to false.")]
            bool compact = false
        )
        {
            var skeleton = BeehaveDefaults.Recommended();
            if (compact)
                skeleton.Note = string.Empty;
            return skeleton;
        }
    }
}
