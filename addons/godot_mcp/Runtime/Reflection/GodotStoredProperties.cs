/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// The read side of the property tools: a resource or node is serialized as a reference only (by its
    /// converter), so its data is added from Godot's own property list — every PROPERTY_USAGE_STORAGE entry,
    /// exactly what ResourceSaver / the scene saver writes. Each entry's name and value can be sent back to
    /// <c>resource-modify</c> / <c>node-modify</c> unchanged.
    /// </summary>
    public static class GodotStoredProperties
    {
        /// <summary>Add every stored property of <paramref name="owner"/> to <paramref name="member"/>. Main thread only.</summary>
        public static void AddTo(SerializedMember member, Reflector reflector, GodotObject owner, string ownerName, string toolId)
        {
            foreach (var prop in owner.GetPropertyList())
            {
                if (!prop.TryGetValue("usage", out var usage) ||
                    (usage.As<PropertyUsageFlags>() & PropertyUsageFlags.Storage) == 0 ||
                    !prop.TryGetValue("name", out var propName))
                    continue;

                // Skip unset values (Nil, or an Object-typed Variant holding null): they only add noise.
                var value = owner.Get(propName.AsStringName());
                if (value.VariantType == Variant.Type.Nil)
                    continue;
                object boxed = value;
                if (value.VariantType == Variant.Type.Object)
                {
                    var godotObject = value.AsGodotObject();
                    if (godotObject == null)
                        continue;
                    // A resource or node goes through its own converter, so the caller gets a resolvable
                    // ResourceRef / NodeRef typed as the object's class.
                    if (godotObject is Resource or Node)
                        boxed = godotObject;
                }

                // One property the reflector cannot serialize must not take down the whole read.
                try
                {
                    member.AddProperty(reflector.Serialize(obj: boxed, name: propName.AsString(), recursive: true));
                }
                catch (Exception ex)
                {
                    // Keep the property in the reply so the caller can tell the data is partial.
                    member.AddProperty(SerializedMember.FromJson(typeof(Variant),
                        Godot_Variant_ReflectionConverter.NoteNode(value.VariantType, $"Could not be serialized: {ex.Message}").ToJsonString(),
                        propName.AsString()));
                    GodotMcpLog.Warning($"[Godot-MCP] {toolId}: property '{propName}' of '{ownerName}' could not be serialized: {ex.Message}");
                }
            }
        }
    }
}
