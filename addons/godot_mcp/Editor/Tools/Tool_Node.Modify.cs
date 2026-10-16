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
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Node
    {
        public const string NodeModifyToolId = "node-modify";

        [AiTool
        (
            NodeModifyToolId,
            Title = "Node / Modify",
            IdempotentHint = true
        )]
        [Description("Modify properties of a Node in the currently edited Godot scene. " +
            "Identify the Node with 'nodeRef'. Two modification surfaces (supply at least one; both may be " +
            "combined, applied jsonPatch first then pathPatches):\n" +
            "  1. 'pathPatches' — list of {path, value} entries, each applied and read back.\n" +
            "  2. 'jsonPatch' — a JSON Merge Patch (RFC 7396) string.\n" +
            "Returns a log of what was changed and what was ignored.\n" +
            GodotObjectPatcher.PathSyntaxHelp)]
        public Logs Modify
        (
            [Description("Reference to the Node to modify (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("Optional list of path-scoped patches, each a {path, value}; see the path syntax in the tool description.")]
            List<NodePropertyPatch>? pathPatches = null,
            [Description("Optional JSON Merge Patch (RFC 7396); keys may be Godot property names at any depth (see the path syntax in the tool description).")]
            string? jsonPatch = null
        )
        {
            if ((pathPatches == null || pathPatches.Count == 0) && string.IsNullOrWhiteSpace(jsonPatch))
                throw new ArgumentException(
                    $"At least one of '{nameof(pathPatches)}' or '{nameof(jsonPatch)}' is required.");

            return MainThread.Instance.Run(() =>
            {
                var logs = new Logs();

                var node = ResolveNode(nodeRef, out var error);
                if (error != null)
                {
                    logs.Error(error);
                    return logs;
                }
                if (node == null)
                {
                    logs.Error($"Node by {nodeRef} not found.");
                    return logs;
                }

                var patcher = new GodotObjectPatcher(GodotMcpReflector.GetOrCreate(), node, logs);
                var anyChange = patcher.Apply(jsonPatch, pathPatches);

                // A '$type' replacement assigns a fresh instance instead of mutating the live node, so reporting
                // success + marking unsaved would be a silent no-op. Guard against it.
                if (patcher.DivergedSurface != null)
                {
                    logs.Error($"Modification via {patcher.DivergedSurface} produced a new instance instead of " +
                        "mutating the live Node in place (the editor Node was not updated); the scene was left unchanged.");
                    return logs;
                }

                if (anyChange)
                    EditorInterface.Singleton.MarkSceneAsUnsaved();
                else
                    logs.Warning("No modifications were made.");

                return logs;
            });
        }
    }
}
#endif
