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

                var member = reflector.Serialize(
                    obj: resource,
                    name: name,
                    recursive: true);

                // ReflectorNet has no Godot-native property walk for a Resource: `member` carries only the
                // reference (value = { instanceId, resourcePath }) and leaves `props` empty, so this tool
                // could not read a resource back at all. Godot's own property list is the authority here -
                // take every property ResourceSaver would write (PROPERTY_USAGE_STORAGE) and serialize each
                // value through the same reflector, so the shape matches the other tools. The original
                // `value` (the resource reference) is kept as-is for existing callers.
                foreach (var prop in resource.GetPropertyList())
                {
                    if (!prop.ContainsKey("name") || !prop.ContainsKey("usage"))
                        continue;

                    var propName = prop["name"].AsString();
                    if (string.IsNullOrEmpty(propName))
                        continue;

                    // Fully qualified on purpose: this file lives in com.IvanMurzak.Godot.MCP.Tools, where a
                    // bare `Godot.` binds to com.IvanMurzak.Godot (CS0234); adding `using Godot;` would also
                    // make `MainThread` ambiguous with this addon's own MainThread.
                    var usage = (global::Godot.PropertyUsageFlags)prop["usage"].AsInt64();
                    if ((usage & global::Godot.PropertyUsageFlags.Storage) == 0)
                        continue;

                    var value = resource.Get(propName);
                    // An unset reference is an Object-typed Variant holding null (not Nil). Each one would
                    // otherwise carry the sizeable "how a Variant is written" note while telling the caller
                    // nothing but "not set" - measured: 15 of them accounted for ~20 KB of a 41 KB reply.
                    if (value.VariantType == global::Godot.Variant.Type.Nil)
                        continue;
                    if (value.VariantType == global::Godot.Variant.Type.Object && value.AsGodotObject() == null)
                        continue;

                    member.AddProperty(reflector.Serialize(
                        obj: (object)value,
                        name: propName,
                        recursive: true));
                }

                return member;
            });
        }
    }
}
#endif
