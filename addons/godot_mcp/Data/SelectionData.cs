/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Structured snapshot of the Godot editor's current node selection — the Godot analog of Unity-MCP's
    /// <c>SelectionData</c>. Godot's <see cref="global::Godot.EditorSelection"/> selects scene-tree
    /// <see cref="global::Godot.Node"/>s only (there is no Unity-style asset-GUID / Transform / Component
    /// distinction), so this model carries a flat list of selected nodes plus a convenience pointer to the
    /// "active" one (Godot has no first-class active-object concept; the LAST selected node is reported as
    /// active, matching how the editor inspector tracks the most-recently-clicked node).
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>): each selected node is captured as a
    /// <see cref="NodeData"/> (built on the main thread from the live node), so the model touches no Godot
    /// native object once constructed and is unit-testable in the plain xUnit host.
    /// </summary>
    [System.Serializable]
    [Description("Snapshot of the Godot editor node selection: the selected nodes and the active (last-" +
        "selected) node, each as structured NodeData.")]
    public class SelectionData
    {
        [JsonInclude, JsonPropertyName("nodes")]
        [Description("All currently-selected scene-tree nodes, in selection order.")]
        public List<NodeData> Nodes { get; set; } = new();

        [JsonInclude, JsonPropertyName("activeNode")]
        [Description("The active (last-selected) node, or null when the selection is empty. Godot has no " +
            "first-class active-object concept; the last selected node is reported here.")]
        public NodeData? ActiveNode { get; set; } = null;

        [JsonInclude, JsonPropertyName("count")]
        [Description("Number of selected nodes.")]
        public int Count { get; set; } = 0;

        public SelectionData() { }

        public override string ToString()
            => $"Selection count={Count} active='{ActiveNode?.Name ?? "(none)"}'";
    }
}
