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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Resource lookup shared by the Resource reflection converter (<c>Tool_Resource.ReflectionResolver.cs</c>). The
    /// <c>resource-*</c> tool family was replaced by RQL (<c>godot.resource(path)</c> / <c>godot.resources(type)</c> and
    /// the <c>editor</c> source's CreateResource / MoveResource / DeleteResource, see docs/rql.md); the class keeps its
    /// name so the resolver wiring stays unchanged. It declares no tools.
    /// </summary>
    public static partial class Tool_Resource
    {
        /// <summary>
        /// The resource at a res:// / uid:// path, or null with the reason. Shared by ResourceRef resolution and RQL's
        /// <c>godot.resource</c> / res:// coercion.
        /// </summary>
        internal static Resource? LoadByPath(string resPath, out string? error)
        {
            error = null;
            if (!ResourceLoader.Exists(resPath))
            {
                error = $"No resource exists at '{resPath}'.";
                return null;
            }
            var loaded = ResourceLoader.Load(resPath);
            if (loaded == null)
                error = $"Resource at '{resPath}' failed to load.";
            return loaded;
        }

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
                var loaded = LoadByPath(resPath, out error);
                if (loaded != null)
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
    }
}
#endif
