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
using System.Text.Json.Nodes;
using com.IvanMurzak.Godot.MCP.Connection;
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

                // The Resource converter deliberately serializes a resource as a reference only, so `member`
                // carries no props. Expand this top-level resource from Godot's own property list: every
                // PROPERTY_USAGE_STORAGE entry, i.e. exactly what ResourceSaver writes.
                foreach (var prop in resource.GetPropertyList())
                {
                    if (!prop.TryGetValue("usage", out var usage) ||
                        (usage.As<PropertyUsageFlags>() & PropertyUsageFlags.Storage) == 0 ||
                        !prop.TryGetValue("name", out var propName))
                        continue;

                    // Skip unset values (Nil, or an Object-typed Variant holding null): they only add noise.
                    var value = resource.Get(propName.AsStringName());
                    if (value.VariantType == Variant.Type.Nil)
                        continue;
                    object boxed = value;
                    if (value.VariantType == Variant.Type.Object)
                    {
                        var godotObject = value.AsGodotObject();
                        if (godotObject == null)
                            continue;
                        // A sub-resource goes through the Resource converter so the caller gets a resolvable
                        // ResourceRef (res:// path / instance id), not the Variant's "<Type#id>" string form.
                        if (godotObject is Resource subResource)
                            boxed = subResource;
                    }

                    // One property the reflector cannot serialize must not take down the whole read.
                    try
                    {
                        member.AddProperty(reflector.Serialize(
                            obj: boxed,
                            name: propName.AsString(),
                            recursive: true));
                    }
                    catch (Exception ex)
                    {
                        // Keep the property in the reply so the caller can tell the data is partial.
                        member.AddProperty(SerializedMember.FromJson(typeof(Variant), new JsonObject
                        {
                            [Godot_Variant_ReflectionConverter.VariantTypeKey] = value.VariantType.ToString(),
                            [Godot_Variant_ReflectionConverter.NoteKey] = $"Could not be serialized: {ex.Message}",
                        }.ToJsonString(), propName.AsString()));
                        GodotMcpLog.Warning($"[Godot-MCP] {ResourceGetDataToolId}: property '{propName}' " +
                            $"of '{name}' could not be serialized: {ex.Message}");
                    }
                }

                return member;
            });
        }
    }
}
#endif
