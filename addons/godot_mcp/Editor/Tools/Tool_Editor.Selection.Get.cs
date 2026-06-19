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
    public partial class Tool_Editor_Selection
    {
        public const string EditorSelectionGetToolId = "editor-selection-get";

        [AiTool
        (
            EditorSelectionGetToolId,
            Title = "Editor / Selection / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Get the current node selection in the Godot editor as structured data: the selected " +
            "scene-tree nodes (each as NodeData with instanceId/name/path/type) and the active (last-" +
            "selected) node. The Godot analog of Unity's 'editor-selection-get'. Use 'editor-selection-set' " +
            "to change the selection. Returns an empty selection (count 0, activeNode null) when nothing is " +
            "selected.")]
        public SelectionData Get(string? nothing = null)
        {
            return MainThread.Instance.Run(() => ToSelectionData());
        }
    }
}
#endif
