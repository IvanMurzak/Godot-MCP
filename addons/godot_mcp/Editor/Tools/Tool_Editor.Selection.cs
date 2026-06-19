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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Editor selection tool family (<c>editor-selection-*</c>) — the Godot analog of Unity-MCP's
    /// <c>Tool_Editor_Selection</c>. Each tool method lives in its own partial-class file (Get / Set) and
    /// drives the editor's node selection via <see cref="EditorSelection"/> (obtained from
    /// <see cref="EditorInterface.GetSelection"/>) through the main-thread dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: Godot's <see cref="EditorSelection"/> selects scene-tree
    /// <see cref="Node"/>s only — there is no Unity-style asset-GUID / Transform / Component selection
    /// distinction, and no first-class "active object". So <see cref="SelectionData"/> carries a flat node
    /// list plus the LAST-selected node as the "active" one (matching how the editor inspector tracks the
    /// most-recently-clicked node).
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>). The pure-managed <see cref="SelectionData"/> model lives outside
    /// this guard and is unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_Editor_Selection
    {
        /// <summary>
        /// Build a <see cref="SelectionData"/> from the editor's current node selection. Main-thread only.
        /// The active node is reported as the LAST selected node (Godot has no first-class active object).
        /// </summary>
        internal static SelectionData ToSelectionData()
        {
            var data = new SelectionData();

            var selection = EditorInterface.Singleton.GetSelection();
            var nodes = selection.GetSelectedNodes();

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;
                data.Nodes.Add(Tool_Node.ToNodeData(node, hierarchyDepth: 0));
            }

            data.Count = data.Nodes.Count;
            data.ActiveNode = data.Nodes.Count > 0 ? data.Nodes[data.Nodes.Count - 1] : null;

            return data;
        }
    }
}
#endif
