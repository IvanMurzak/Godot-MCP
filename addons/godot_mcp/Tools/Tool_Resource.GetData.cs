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
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Resource
    {
        public const string ResourceGetDataToolId = "resource-get-data";

        [AiTool
        (
            ResourceGetDataToolId,
            Title = "Resource / Get Data",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Get the serialized data of a Godot Resource (.tres/.res asset) — every serializable " +
            "property — via ReflectorNet. Identify the resource with 'resourceRef' (a res:// path is " +
            "preferred; an instance id of an already-loaded resource also works). Use '" + ResourceFindToolId +
            "' to locate the resource first. Returns a ReflectorNet SerializedMember describing the resource; " +
            "a resource that cannot be resolved yields a structured error.")]
        public SerializedMember GetData
        (
            [Description("Reference to the resource to read (res:// path preferred, else a loaded instance id).")]
            ResourceRef resourceRef
        )
        {
            if (resourceRef == null)
                throw new ArgumentNullException(nameof(resourceRef));
            if (!resourceRef.IsValid(out var validationError))
                throw new ArgumentException(validationError, nameof(resourceRef));

            return MainThread.Instance.Run(() =>
            {
                var resource = ResolveResource(resourceRef, out var path, out var error);
                if (resource == null)
                    throw new Exception(error ?? $"Resource {resourceRef} not found.");

                var reflector = GodotMcpReflector.GetOrCreate();
                var name = string.IsNullOrEmpty(path)
                    ? (string.IsNullOrEmpty(resource.ResourceName) ? resource.GetClass() : resource.ResourceName)
                    : path!;

                return reflector.Serialize(
                    obj: resource,
                    name: name,
                    recursive: true);
            });
        }
    }
}
#endif
