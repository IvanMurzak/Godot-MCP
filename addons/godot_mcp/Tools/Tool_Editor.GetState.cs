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
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Editor
    {
        public const string EditorApplicationGetStateToolId = "editor-application-get-state";

        [AiTool
        (
            EditorApplicationGetStateToolId,
            Title = "Editor / Application / Get State",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the current run/play state of the Godot editor: whether a scene is currently " +
            "being run in a separate game process, the res:// path of that scene (if any), and the editor " +
            "version string. The Godot analog of Unity's 'editor-application-get-state'. Use " +
            "'editor-application-set-state' to start/stop a play-run.")]
        public EditorStateData GetState(string? nothing = null)
        {
            return MainThread.Instance.Run(() => ToEditorStateData());
        }
    }
}
#endif
