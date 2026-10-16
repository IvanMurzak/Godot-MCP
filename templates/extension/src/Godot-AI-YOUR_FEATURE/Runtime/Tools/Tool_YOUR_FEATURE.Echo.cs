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

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE
{
    public partial class Tool_YOUR_FEATURE
    {
        /// <summary>The tool id, exposed as a const so tests / E2E reference the exact string.</summary>
        public const string EchoToolId = "YOUR_TOOL_PREFIX-echo";

        /// <summary>
        /// Pure-managed sample tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is
        /// fully CI-unit-testable (see <c>Tool_YOUR_FEATURE_EchoTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool YOUR_TOOL_PREFIX-echo</c>. Replace it with your real tool(s); keep
        /// pure-managed tools here and editor-driving tools under <c>../../Editor/Tools/</c>.
        /// </summary>
        [AiTool
        (
            EchoToolId,
            Title = "YOUR_DISPLAY_NAME / Echo",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Sample readiness probe for the YOUR_DISPLAY_NAME extension. Returns the input " +
            "'message' echoed back, or 'YOUR_TOOL_PREFIX-ready' when omitted. Proves the extension's " +
            "tool family is discovered and callable end-to-end after the package compiles into the " +
            "consumer's Godot project.")]
        public string Echo
        (
            [Description("Optional message to echo back. When null or empty, returns 'YOUR_TOOL_PREFIX-ready'.")]
            string? message = null
        )
        {
            return string.IsNullOrEmpty(message) ? "YOUR_TOOL_PREFIX-ready" : message;
        }
    }
}
