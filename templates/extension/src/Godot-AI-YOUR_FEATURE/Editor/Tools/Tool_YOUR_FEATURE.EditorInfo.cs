/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE
{
    public partial class Tool_YOUR_FEATURE
    {
        /// <summary>The editor-tool id, exposed as a const so E2E references the exact string.</summary>
        public const string EditorInfoToolId = "YOUR_TOOL_PREFIX-editor-info";

        /// <summary>
        /// Editor-only sample tool — touches the Godot editor API (<see cref="EditorInterface"/>,
        /// <see cref="Engine"/>), so it lives behind <c>#if TOOLS</c> (excluded from an exported game
        /// build) and is verified by the headless-Godot E2E (<c>godot-cli run-tool
        /// YOUR_TOOL_PREFIX-editor-info</c>), NOT by the pure-managed unit tests — a plain xUnit host
        /// has no live Godot main loop, so constructing <c>Node</c>/calling <c>EditorInterface</c> there
        /// would fault.
        ///
        /// <para>
        /// <b>Main-thread discipline (mandatory).</b> Godot editor API must be touched on the editor
        /// main thread. Tool handlers run off-thread, so every Godot call marshals through
        /// <c>MainThread.Instance.Run(...)</c>. Never touch <c>EditorInterface</c> / <c>Node</c> /
        /// <c>Resource</c> outside that delegate.
        /// </para>
        /// </summary>
        [AiTool
        (
            EditorInfoToolId,
            Title = "YOUR_DISPLAY_NAME / Editor Info",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Sample editor-only tool for the YOUR_DISPLAY_NAME extension. Returns the running " +
            "Godot editor version and UI scale. Demonstrates the #if TOOLS editor-tool pattern and the " +
            "mandatory main-thread marshalling for Godot editor API access.")]
        public string EditorInfo()
        {
            // All Godot editor API access is marshalled onto the editor main thread.
            return MainThread.Instance.Run(() =>
            {
                var version = (string)Engine.GetVersionInfo()["string"];
                var scale = EditorInterface.Singleton.GetEditorScale();
                return $"Godot {version} (editor UI scale {scale:0.##}x)";
            });
        }
    }
}
#endif
