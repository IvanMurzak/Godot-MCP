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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Editor_Selection
    {
        public const string EditorSelectionSetToolId = "editor-selection-set";

        [AiTool
        (
            EditorSelectionSetToolId,
            Title = "Editor / Selection / Set",
            IdempotentHint = true
        )]
        [Description("Set the Godot editor's node selection to the provided nodes (replacing any current " +
            "selection). The Godot analog of Unity's 'editor-selection-set'. Each entry is a NodeRef " +
            "(instanceId preferred, else scene-tree path relative to the edited scene root). All refs must " +
            "resolve to live scene-tree nodes; otherwise the call throws and the selection is left " +
            "unchanged. Pass an empty list to clear the selection. Use 'editor-selection-get' to inspect " +
            "the current selection first. Returns the post-change SelectionData.")]
        public SelectionData Set
        (
            [Description("Nodes to select, each identified by instanceId (preferred) or scene-tree path. " +
                "An empty list clears the selection.")]
            List<NodeRef> select
        )
        {
            if (select == null)
                throw new ArgumentNullException(nameof(select));

            return MainThread.Instance.Run(() =>
            {
                // Resolve all refs FIRST so a bad ref aborts before we mutate the live selection (all-or-nothing).
                var resolved = new List<Node>(select.Count);
                for (int i = 0; i < select.Count; i++)
                {
                    var node = Tool_Node.ResolveNode(select[i], out var error);
                    if (node == null)
                        throw new Exception($"select[{i}] could not be resolved: {error ?? $"{select[i]} not found."}");
                    resolved.Add(node);
                }

                var selection = EditorInterface.Singleton.GetSelection();
                selection.Clear();
                foreach (var node in resolved)
                    selection.AddNode(node);

                return ToSelectionData();
            });
        }
    }
}
#endif
