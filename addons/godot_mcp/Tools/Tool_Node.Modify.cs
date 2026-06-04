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
        [Description("Modify properties of a Node in the currently edited Godot scene via ReflectorNet. " +
            "Identify the Node with 'nodeRef'. Two modification surfaces (supply at least one; both may be " +
            "combined, applied jsonPatch first then pathPatches):\n" +
            "  1. 'pathPatches' — list of {path, value} entries routed through Reflector.TryModifyAt for " +
            "atomic per-path modification. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', " +
            "'dictField/[key]'.\n" +
            "  2. 'jsonPatch' — a JSON Merge Patch (RFC 7396) string routed through Reflector.TryPatch.\n" +
            "Returns a log of what was changed and what was ignored.")]
        public Logs Modify
        (
            [Description("Reference to the Node to modify (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("Optional list of path-scoped patches routed through Reflector.TryModifyAt. " +
                "Each entry is a {path, value}. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.")]
            List<NodePropertyPatch>? pathPatches = null,
            [Description("Optional JSON Merge Patch (RFC 7396) applied via Reflector.TryPatch.")]
            string? jsonPatch = null
        )
        {
            var hasPathPatches = pathPatches != null && pathPatches.Count > 0;
            var hasJsonPatch = !string.IsNullOrWhiteSpace(jsonPatch);

            if (!hasPathPatches && !hasJsonPatch)
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

                var reflector = GodotMcpReflector.GetOrCreate();
                object? objToModify = node;
                var anyChange = false;

                // 1) JSON Merge Patch.
                if (hasJsonPatch)
                {
                    if (reflector.TryPatch(ref objToModify, jsonPatch!, logs: logs))
                        anyChange = true;
                }

                // 2) Path patches.
                if (hasPathPatches)
                {
                    for (int i = 0; i < pathPatches!.Count; i++)
                    {
                        var patch = pathPatches[i];
                        if (patch == null || string.IsNullOrEmpty(patch.Path))
                        {
                            logs.Error($"{nameof(pathPatches)}[{i}] with empty path skipped.");
                            continue;
                        }
                        if (patch.Value == null)
                        {
                            logs.Error($"{nameof(pathPatches)}[{i}] ('{patch.Path}') with null value skipped.");
                            continue;
                        }
                        if (reflector.TryModifyAt(ref objToModify, patch.Path, patch.Value, logs: logs))
                            anyChange = true;
                    }
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
