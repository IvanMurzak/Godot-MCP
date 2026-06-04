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
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Reference to a Godot <see cref="global::Godot.Resource"/> (e.g. a <c>.tres</c>/<c>.res</c> asset).
    /// The Godot analog of Unity-MCP's <c>AssetObjectRef</c>. A resource is located by its <c>res://</c>
    /// path, or — for an already-loaded resource — by its instance id.
    ///
    /// Plain data model — holds no live <see cref="global::Godot.Resource"/> handle and touches no Godot
    /// API, so it serializes/deserializes via ReflectorNet off the main thread. Loading the resource
    /// (<c>ResourceLoader.Load</c>) is the caller's responsibility, on the main thread.
    /// </summary>
    [System.Serializable]
    [Description("Reference to a Godot Resource (.tres/.res asset), located by res:// path or instance id.")]
    public class ResourceRef
    {
        public static class ResourceRefProperty
        {
            public const string InstanceId = "instanceId";
            public const string ResourcePath = "resourcePath";

            public static IEnumerable<string> All => new[] { InstanceId, ResourcePath };
        }

        [JsonInclude, JsonPropertyName(ResourceRefProperty.InstanceId)]
        [Description("Instance id of an already-loaded Resource (Godot GodotObject.GetInstanceId()). If '0', treated as unset. Priority: 2.")]
        public ulong InstanceId { get; set; } = 0;

        [JsonInclude, JsonPropertyName(ResourceRefProperty.ResourcePath)]
        [Description("res:// path of the Resource, e.g. 'res://materials/wood.tres'. Priority: 1 (Recommended).")]
        public string? ResourcePath { get; set; } = null;

        public ResourceRef() { }

        public ResourceRef(ulong instanceId)
        {
            InstanceId = instanceId;
        }

        public ResourceRef(string? resourcePath)
        {
            ResourcePath = resourcePath;
        }

        public virtual bool IsValid() => IsValid(out _);

        // NOTE: priority is DELIBERATELY inverted relative to NodeRef. A Resource is most reliably
        // identified by its res:// path (the stable identity of an on-disk asset), so ResourcePath
        // is priority 1 here; NodeRef prefers InstanceId because a scene-tree path can be ambiguous
        // or shift as the tree mutates. IsValid only checks presence, so the boolean result is
        // order-independent — the priority is a hint for the downstream resolver, which prefers the
        // priority-1 field when both are set.
        public virtual bool IsValid(out string? error)
        {
            if (!string.IsNullOrEmpty(ResourcePath))
            {
                error = null;
                return true;
            }
            if (InstanceId != 0)
            {
                error = null;
                return true;
            }

            error = $"At least one of '{ResourceRefProperty.ResourcePath}' (non-empty) or '{ResourceRefProperty.InstanceId}' (non-zero) must be set.";
            return false;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(ResourcePath))
                return $"Resource {ResourceRefProperty.ResourcePath}='{ResourcePath}'";
            if (InstanceId != 0)
                return $"Resource {ResourceRefProperty.InstanceId}='{InstanceId}'";
            return "Resource unknown";
        }
    }
}
