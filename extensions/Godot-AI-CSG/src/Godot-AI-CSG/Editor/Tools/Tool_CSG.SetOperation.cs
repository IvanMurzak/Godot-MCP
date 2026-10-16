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
        /// Editor-only tool — sets the boolean operation (<c>union</c> / <c>intersection</c> /
        /// <c>subtraction</c>) of an existing CSG node, addressed by node path. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetOperationToolId,
            Title = "CSG / Set Operation"
        )]
        [Description("Set the boolean operation of an existing CSG node (CsgBox3D/CsgSphere3D/CsgCylinder3D/" +
            "CsgCombiner3D), addressed by 'nodePath' (relative to the edited scene root). 'operation' is " +
            "'Union' (merge), 'Intersection' (keep the overlap), or 'Subtraction' (carve away). The operation " +
            "resolves against the node's CSG siblings under the same CSG parent/combiner. Returns the node's " +
            "updated config.")]
        public CsgShapeInfo SetOperation
        (
            [Description("Node path (relative to the edited scene root) of the CSG node to modify.")]
            string nodePath,
            [Description("The boolean operation: 'Union', 'Intersection', or 'Subtraction'.")]
            string operation
        )
        {
            var op = CsgOperations.Parse(operation);

            return MainThread.Instance.Run(() =>
            {
                var shape = ResolveCsgNodeOrThrow(nodePath);
                shape.Operation = ToGodotOperation(op);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(shape);
            });
        }
    }
}
#endif
