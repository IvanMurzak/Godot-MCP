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
    public partial class Tool_Resource
    {
        public const string ResourceModifyToolId = "resource-modify";

        [AiTool
        (
            ResourceModifyToolId,
            Title = "Resource / Modify",
            IdempotentHint = true
        )]
        [Description("Modify properties of a Godot Resource (.tres/.res asset) and save it " +
            "back to disk. Identify the resource with 'resourceRef' (a res:// path is required — an instance " +
            "id with no on-disk path cannot be saved). Two modification surfaces (supply at least one; both " +
            "may be combined, applied jsonPatch first then pathPatches):\n" +
            "  1. 'pathPatches' — list of {path, value} entries, each applied and read back.\n" +
            "  2. 'jsonPatch' — a JSON Merge Patch (RFC 7396) string.\n" +
            "On success the resource is re-saved with ResourceSaver and the editor filesystem is updated " +
            "(the .import sidecar is left untouched). Use '" + ResourceGetDataToolId + "' first to inspect " +
            "the structure. Returns a log of what was changed and what was ignored.\n" +
            GodotObjectPatcher.PathSyntaxHelp)]
        public Logs Modify
        (
            [Description("Reference to the resource to modify (res:// path required; an unsaved instance cannot be persisted).")]
            ResourceRef resourceRef,
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

                var resource = ResolveResource(resourceRef, out var path, out var error);
                if (error != null)
                {
                    logs.Error(error);
                    return logs;
                }
                if (resource == null)
                {
                    logs.Error($"Resource {resourceRef} not found.");
                    return logs;
                }
                if (string.IsNullOrEmpty(path))
                {
                    logs.Error("Resource has no res:// path on disk, so a modification cannot be saved. " +
                        "Provide a 'resourceRef' with a 'resourcePath'.");
                    return logs;
                }

                var patcher = new GodotObjectPatcher(GodotMcpReflector.GetOrCreate(), resource, logs);
                var anyChange = patcher.Apply(jsonPatch, pathPatches);

                // A '$type' replacement assigns a fresh instance instead of mutating the loaded Resource, so
                // persisting would write stale data. Guard against it.
                if (patcher.DivergedSurface != null)
                {
                    logs.Error($"Modification via {patcher.DivergedSurface} produced a new instance instead of " +
                        "mutating the loaded Resource in place (likely a '$type' replacement); the resource was NOT re-saved.");
                    return logs;
                }

                if (!anyChange)
                {
                    logs.Warning("No modifications were made; the resource was not re-saved.");
                    return logs;
                }

                // Persist via ResourceSaver (NOT a hand-edit of the .tres / .import sidecar) and make the
                // editor filesystem aware of the change.
                var saveErr = ResourceSaver.Save(resource, path!);
                if (saveErr != Error.Ok)
                {
                    logs.Error($"Modifications applied in memory but ResourceSaver.Save('{path}') failed: {saveErr}.");
                    return logs;
                }

                EditorInterface.Singleton.GetResourceFilesystem()?.UpdateFile(path!);
                logs.Success($"Resource '{path}' modified and saved.");
                return logs;
            });
        }
    }
}
#endif
