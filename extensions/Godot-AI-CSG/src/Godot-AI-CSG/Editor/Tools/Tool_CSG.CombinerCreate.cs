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

namespace com.IvanMurzak.Godot.MCP.CSG
{
    public partial class Tool_CSG
    {
        /// <summary>
        /// Editor-only tool — creates a <c>CsgCombiner3D</c> container in the currently edited scene and
        /// returns its structured config. A combiner is the root that groups child CSG shapes so their boolean
        /// operations resolve against each other (and so the merged result can itself take part in a parent
        /// op). All Godot API access is marshalled onto the editor main thread via <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            CombinerCreateToolId,
            Title = "CSG / Combiner Create"
        )]
        [Description("Create a CsgCombiner3D container in the currently edited Godot scene and return its " +
            "structured config. A combiner groups child CSG shapes so their union/intersection/subtraction " +
            "ops resolve together. Optionally pass 'parentPath' (a node path relative to the scene root) to " +
            "parent it (defaults to the scene root), 'name' to rename it, and 'operation' (the combiner's own " +
            "op when nested under another CSG node: 'Union' (default), 'Intersection', or 'Subtraction'). The " +
            "new node's owner is set to the scene root so it is saved with the scene.")]
        public CsgShapeInfo CombinerCreate
        (
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("The combiner's boolean operation: 'Union' (default), 'Intersection', or 'Subtraction'.")]
            string? operation = null
        )
        {
            var op = string.IsNullOrWhiteSpace(operation)
                ? CsgOperation.Union
                : CsgOperations.Parse(operation);

            return MainThread.Instance.Run(() =>
            {
                var combiner = new CsgCombiner3D
                {
                    Operation = ToGodotOperation(op)
                };

                AttachToScene(combiner, name, parentPath);
                return ReadInfo(combiner);
            });
        }
    }
}
#endif
