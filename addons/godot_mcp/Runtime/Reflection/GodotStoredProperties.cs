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
    /// exactly what ResourceSaver / the scene saver writes.
    /// </summary>
    public static class GodotStoredProperties
    {
        static readonly Variant UsageKey = "usage";
        static readonly Variant NameKey = "name";

        /// <summary>Add every stored property of <paramref name="owner"/> to <paramref name="member"/>. Main thread only.</summary>
        public static void AddTo(SerializedMember member, Reflector reflector, GodotObject owner)
        {
            var properties = owner.GetPropertyList();
            foreach (var prop in properties)
            {
                using (prop)
                {
                    if (!prop.TryGetValue(UsageKey, out var usage) ||
                        (usage.As<PropertyUsageFlags>() & PropertyUsageFlags.Storage) == 0 ||
                        !prop.TryGetValue(NameKey, out var nameVariant))
                        continue;
                    AddProperty(member, reflector, owner, nameVariant.AsString());
                }
            }
        }

        static void AddProperty(SerializedMember member, Reflector reflector, GodotObject owner, string name)
        {
            // Skip unset values (Nil, or an Object-typed Variant holding null): they only add noise.
            var value = owner.Get(name);
            if (value.VariantType == Variant.Type.Nil)
                return;
            object boxed = value;
            if (value.VariantType == Variant.Type.Object)
            {
                var godotObject = value.AsGodotObject();
                if (godotObject == null)
                    return;
                // A resource or node goes through its own converter, so the caller gets a resolvable
                // ResourceRef / NodeRef typed as the object's class.
                if (godotObject is Resource or Node)
                    boxed = godotObject;
            }

            // One property the reflector cannot serialize must not take down the whole read.
            try
            {
                member.AddProperty(reflector.Serialize(obj: boxed, name: name, recursive: true));
            }
            catch (Exception ex)
            {
                // Keep the property in the reply so the caller can tell the data is partial.
                member.AddProperty(SerializedMember.FromJson(typeof(Variant),
                    Godot_Variant_ReflectionConverter.NoteNode(value.VariantType, $"Could not be serialized: {ex.Message}").ToJsonString(),
                    name));
                GodotMcpLog.Warning($"[Godot-MCP] property '{name}' of '{member.name}' could not be serialized: {ex.Message}");
            }
        }
    }
}
