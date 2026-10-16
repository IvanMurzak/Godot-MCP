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
using System.Reflection;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
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
        [Description("Modify properties of a Godot Resource (.tres/.res asset) via ReflectorNet and save it " +
            "back to disk. Identify the resource with 'resourceRef' (a res:// path is required — an instance " +
            "id with no on-disk path cannot be saved). Two modification surfaces (supply at least one; both " +
            "may be combined, applied jsonPatch first then pathPatches):\n" +
            "  1. 'pathPatches' — list of {path, value} entries routed through Reflector.TryModifyAt. " +
            "Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.\n" +
            "  2. 'jsonPatch' — a JSON Merge Patch (RFC 7396) string routed through Reflector.TryPatch.\n" +
            "On success the resource is re-saved with ResourceSaver and the editor filesystem is updated " +
            "(the .import sidecar is left untouched). Use '" + ResourceGetDataToolId + "' first to inspect " +
            "the structure. Returns a log of what was changed and what was ignored.")]
        public Logs Modify
        (
            [Description("Reference to the resource to modify (res:// path required; an unsaved instance cannot be persisted).")]
            ResourceRef resourceRef,
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

                var reflector = GodotMcpReflector.GetOrCreate();
                object? objToModify = resource;
                var anyChange = false;
                // Track which surface (if any) reassigned 'objToModify' to a fresh boxed instance, so the
                // $type-divergence rejection below can name the culprit for debuggability.
                string? divergedSurface = null;

                // ReflectorNet's TryPatch/TryModifyAt accumulate errors into 'logs' and are documented not to
                // throw, but a malformed patch could still surface an exception from a converter. Wrap each
                // patch-surface call so a bad entry degrades to a logged skip instead of aborting and leaving
                // a half-modified, unsaved resource.
                // 1) JSON Merge Patch (applied first).
                if (hasJsonPatch)
                {
                    try
                    {
                        if (reflector.TryPatch(ref objToModify, jsonPatch!, logs: logs))
                        {
                            anyChange = true;
                            if (divergedSurface == null && !ReferenceEquals(objToModify, resource))
                                divergedSurface = "jsonPatch";
                        }
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
                        // ReflectorNet picks its write converter from the value's typeName and silently drops a
                        // mismatched write (e.g. 'System.Double' into a float property) while reporting success.
                        // For a top-level primitive property, align the typeName and read the value back.
                        try
                        {
                            var primitiveProperty = TopLevelPrimitiveProperty(resource, patch.Path);
                            var expectedTypeName = primitiveProperty?.PropertyType.GetTypeId();
                            var valueTypeCorrected = expectedTypeName != null &&
                                !string.Equals(patch.Value.typeName, expectedTypeName, StringComparison.Ordinal);
                            if (valueTypeCorrected)
                                patch.Value.typeName = expectedTypeName!;
                            var valueBefore = primitiveProperty?.GetValue(resource);

                            if (reflector.TryModifyAt(ref objToModify, patch.Path, patch.Value, logs: logs))
                            {
                                // "Reported as applied" is not "changed": a top-level primitive that reads back
                                // unchanged does not count as a change, so it alone never triggers a re-save
                                // that reports success.
                                if (primitiveProperty != null && ReferenceEquals(objToModify, resource) &&
                                    Equals(valueBefore, primitiveProperty.GetValue(resource)))
                                {
                                    logs.Warning($"{nameof(pathPatches)}[{i}] ('{patch.Path}') read back " +
                                        "unchanged (same value, or silently ignored) and was not counted as a change" +
                                        (valueTypeCorrected ? $"; its value type was corrected to '{expectedTypeName}'." : "."));
                                    continue;
                                }

                                anyChange = true;
                                if (divergedSurface == null && !ReferenceEquals(objToModify, resource))
                                    divergedSurface = $"pathPatches[{i}] ('{patch.Path}')";
                            }
                        }
                        catch (Exception ex)
                        {
                            logs.Error($"{nameof(pathPatches)}[{i}] ('{patch.Path}') threw and was skipped: {ex.Message}");
                        }
                    }
                }

                // 'objToModify' is passed by ref: a patch may reassign it to a fresh boxed instance (e.g. a
                // '$type' replacement) instead of mutating the live resource in place. Resource is a reference
                // type, so in-place writes hit the loaded resource — but if the ref diverged, the resource we
                // are about to save was NOT updated, so persisting would write stale data. Guard against it.
                if (anyChange && !ReferenceEquals(objToModify, resource))
                {
                    logs.Error($"Modification via {divergedSurface ?? "a patch surface"} produced a new " +
                        "instance instead of mutating the loaded Resource in place (likely a '$type' " +
                        "replacement); the resource was NOT re-saved.");
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

        /// <summary>
        /// Resolve the primitive-typed (<c>float</c>, <c>int</c>, <c>bool</c>, ...) CLR property named by a
        /// top-level single-segment patch path, or <c>null</c> for a nested path, an unknown name or any other
        /// type. Matches the CLR (PascalCase) and Godot (snake_case) spelling; the most-derived declaration of a
        /// <c>new</c>-hidden property wins.
        /// </summary>
        static PropertyInfo? TopLevelPrimitiveProperty(object target, string path)
        {
            if (path.IndexOfAny(PathSeparators) >= 0)
                return null;

            var normalized = path.Replace("_", string.Empty);
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (string.Equals(property.Name, normalized, StringComparison.OrdinalIgnoreCase) &&
                        property.CanRead && property.GetIndexParameters().Length == 0)
                        return property.PropertyType.IsPrimitive ? property : null;
                }
            }
            return null;
        }

        static readonly char[] PathSeparators = { '/', '[' };
    }
}
#endif
