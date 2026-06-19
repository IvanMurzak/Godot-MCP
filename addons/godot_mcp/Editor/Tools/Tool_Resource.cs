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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Resource tool family (<c>resource-*</c>) — the Godot analog of the resource-mutating half of
    /// Unity-MCP's <c>Tool_Assets</c> (find / get-data / modify / create / move / delete). Each tool method
    /// lives in its own partial-class file and drives the Godot editor's resource pipeline
    /// (<see cref="ResourceLoader"/>/<see cref="ResourceSaver"/>/<see cref="EditorFileSystem"/>) through the
    /// main-thread dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: a Godot <see cref="Resource"/> on disk (<c>.tres</c>/<c>.res</c>) ↔ a Unity
    /// asset; a <c>res://</c> path / <c>uid://</c> ↔ a Unity asset path / GUID; <see cref="ResourceSaver"/>
    /// ↔ <c>AssetDatabase.CreateAsset</c>; <see cref="EditorFileSystem.Scan"/>/<c>ReimportFiles</c> ↔
    /// <c>AssetDatabase.Refresh</c>/<c>ImportAsset</c>. A resource is identified by a
    /// <see cref="Data.ResourceRef"/> (res:// path preferred, else a loaded instance id) and resolved on
    /// the main thread by <see cref="ResolveResource"/>.
    /// </para>
    ///
    /// <para>
    /// Import-pipeline discipline: tools that write to disk go through <see cref="ResourceSaver"/> and the
    /// editor filesystem (<see cref="EditorFileSystem.UpdateFile"/>), and moves/renames/deletes use
    /// <see cref="EditorFileSystem"/>-aware operations so Godot's sidecar <c>.import</c> metadata stays
    /// consistent. The family never hand-edits <c>.import</c> files.
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>): the handlers touch <see cref="EditorInterface"/> and live
    /// <see cref="Resource"/> objects. The pure-managed result models
    /// (<see cref="Data.ResourceInfo"/>/<see cref="Data.ResourceFindResult"/>) and the
    /// <see cref="ResPathNormalizer"/> live outside this guard and are unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_Resource
    {
        /// <summary>
        /// Resolve a <see cref="Data.ResourceRef"/> to a loaded <see cref="Resource"/> and its
        /// <c>res://</c> path. Must be called on the main thread. Returns null and sets
        /// <paramref name="error"/> on any failure (invalid ref, instance id not a Resource, path with no
        /// resource).
        ///
        /// Resolution order mirrors <see cref="Data.ResourceRef"/>'s declared priority: <c>ResourcePath</c>
        /// first (the stable identity of an on-disk asset), then a loaded <c>InstanceId</c>.
        /// </summary>
        internal static Resource? ResolveResource(Data.ResourceRef? resourceRef, out string? path, out string? error)
        {
            error = null;
            path = null;

            if (resourceRef == null)
            {
                error = "resourceRef is null.";
                return null;
            }
            if (!resourceRef.IsValid(out error))
                return null;

            // 1) res:// path (priority 1).
            if (!string.IsNullOrEmpty(resourceRef.ResourcePath))
            {
                var resPath = resourceRef.ResourcePath!;
                if (!ResourceLoader.Exists(resPath))
                {
                    error = $"No resource exists at '{resPath}'.";
                    return null;
                }

                var loaded = ResourceLoader.Load(resPath);
                if (loaded == null)
                {
                    error = $"Resource at '{resPath}' failed to load.";
                    return null;
                }

                path = resPath;
                return loaded;
            }

            // 2) Loaded instance id (priority 2).
            if (resourceRef.InstanceId != 0)
            {
                if (!GodotObject.IsInstanceIdValid(resourceRef.InstanceId))
                {
                    error = $"No live object with instanceId '{resourceRef.InstanceId}'.";
                    return null;
                }
                var obj = GodotObject.InstanceFromId(resourceRef.InstanceId);
                if (obj is Resource resById)
                {
                    var rp = resById.ResourcePath;
                    path = string.IsNullOrEmpty(rp) ? null : rp;
                    return resById;
                }

                error = $"Object with instanceId '{resourceRef.InstanceId}' is not a Resource (it is '{obj?.GetClass() ?? "null"}').";
                return null;
            }

            error = "resourceRef has neither a resourcePath nor an instanceId.";
            return null;
        }

        /// <summary>
        /// Build a <see cref="Data.ResourceInfo"/> from a resource path (and an optional already-loaded
        /// resource for the type). Main-thread only (reads <see cref="ResourceLoader"/>/the resource).
        /// </summary>
        internal static ResourceInfo ToResourceInfo(string resPath, Resource? loaded = null)
        {
            var uidId = ResourceLoader.GetResourceUid(resPath);
            var uid = uidId == ResourceUid.InvalidId ? null : ResourceUid.IdToText(uidId);

            string? type = loaded?.GetClass();
            if (string.IsNullOrEmpty(type))
            {
                var classFromFs = EditorInterface.Singleton.GetResourceFilesystem()?.GetFileType(resPath);
                type = string.IsNullOrEmpty(classFromFs) ? null : classFromFs;
            }

            return new ResourceInfo
            {
                ResourcePath = resPath,
                Uid = uid,
                Type = type,
            };
        }
    }
}
#endif
