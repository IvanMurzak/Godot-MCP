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

                // ReflectorNet's TryPatch/TryModifyAt are documented to accumulate errors into 'logs' and
                // never throw, but a malformed patch could still surface an exception from a converter or
                // navigation edge case. Wrap each patch-surface call so a bad entry degrades to a logged
                // skip instead of aborting MainThread.Run and leaving the scene half-modified + not marked.
                // 1) JSON Merge Patch (applied first).
                if (hasJsonPatch)
                {
                    try
                    {
                        if (reflector.TryPatch(ref objToModify, jsonPatch!, logs: logs))
                            anyChange = true;
                    }
                    catch (Exception ex)
                    {
                        logs.Error($"jsonPatch threw and was skipped: {ex.Message}");
                    }
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
                        // A value carrying nothing settable (no 'value', no 'fields', no 'props' — e.g. a
                        // '{typeName}'-only entry) cannot change anything. Warn naming the path instead of
                        // letting the no-op pass silently, then skip it.
                        if (NodePropertyPatch.IsStructuralNoOp(patch.Value))
                        {
                            logs.Warning($"{nameof(pathPatches)}[{i}] ('{patch.Path}') has no value/fields/props " +
                                "to apply — nothing to set; skipped (no-op).");
                            continue;
                        }
                        try
                        {
                            if (reflector.TryModifyAt(ref objToModify, patch.Path, patch.Value, logs: logs))
                            {
                                anyChange = true;
                            }
                            else
                            {
                                // TryModifyAt accumulates its own errors into 'logs', but a plain false can
                                // also mean the patch resolved to a no-op (e.g. an unresolved path or a value
                                // that changed nothing). Surface a path-named warning so the no-op is visible.
                                logs.Warning($"{nameof(pathPatches)}[{i}] ('{patch.Path}') did not modify the Node (no-op).");
                            }
                        }
                        catch (Exception ex)
                        {
                            logs.Error($"{nameof(pathPatches)}[{i}] ('{patch.Path}') threw and was skipped: {ex.Message}");
                        }
                    }
                }

                // 'objToModify' is passed by ref: a patch may reassign it to a fresh boxed instance (e.g. a
                // '$type' replacement) instead of mutating the live node in place. Node is a reference type,
                // so in-place writes hit the editor's node — but if the ref diverged, the live node was NOT
                // updated, so reporting success + marking unsaved would be a silent no-op. Guard against it.
                if (anyChange && !ReferenceEquals(objToModify, node))
                {
                    logs.Error("Modification produced a new instance instead of mutating the live Node in " +
                        "place (the editor Node was not updated); the scene was left unchanged.");
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
