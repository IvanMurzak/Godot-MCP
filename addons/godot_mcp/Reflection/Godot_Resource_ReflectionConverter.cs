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
using System.Reflection;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Converter;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// ReflectorNet converter for Godot <see cref="global::Godot.Resource"/> (and every <c>Resource</c>-derived
    /// type — <c>Mesh</c>/<c>BoxMesh</c>/<c>Material</c>/<c>Texture2D</c>/…). The Godot analog of Unity-MCP's
    /// <c>UnityEngine_Object_ReflectionConverter</c>: a Resource crosses the MCP boundary as a lightweight
    /// REFERENCE (<see cref="ResourceRef"/> — a <c>res://</c> path or a loaded instance id), never a deep
    /// serialization of the resource graph.
    ///
    /// <para>
    /// Why this exists: without it, <c>Reflector.TryModify</c> falls back to instantiate-and-populate when
    /// asked to set a <c>Resource</c>-typed member (e.g. <c>MeshInstance3D.Mesh</c>), which fails with
    /// <c>Instance creation failed for Godot.BoxMesh</c> because Godot resources are not constructed that way.
    /// Routing through this converter resolves the ref to the LIVE on-disk asset instead.
    /// </para>
    ///
    /// <para>
    /// <b>Type matching (derived types):</b> <see cref="BaseReflectionConverter{T}.SerializationPriority"/>
    /// scores by inheritance distance from <c>T = Godot.Resource</c>, so this converter matches the exact
    /// <c>Resource</c> type (highest) AND every subclass (a positive, distance-decayed score). A more specific
    /// converter for a concrete subtype, if ever registered, still wins by exact-match priority.
    /// </para>
    ///
    /// <para>
    /// <b>#if TOOLS split:</b> this type is pure-managed (it touches only a <see cref="Type"/> token for
    /// matching and a <see cref="ResourceRef"/> data model). The actual <c>ResourceLoader.Load</c> /
    /// <c>InstanceFromId</c> resolution — a native Godot call that must run on the editor main thread — is
    /// injected via <see cref="ResourceResolver"/> by the editor boot (<c>#if TOOLS</c>). So the converter
    /// registers + matches + (de)serializes the ref shape under CI with no Godot binary, while the live
    /// resolution is exercised by the headless Godot smoke. When no resolver is installed (a plain unit run),
    /// deserialization of a non-empty ref yields <c>null</c> with a logged note rather than throwing.
    /// </para>
    /// </summary>
    public class Godot_Resource_ReflectionConverter : Godot_Resource_ReflectionConverter<global::Godot.Resource> { }

    public class Godot_Resource_ReflectionConverter<T> : GenericReflectionConverter<T>
        where T : global::Godot.GodotObject
    {
        /// <summary>
        /// Resolves a <see cref="ResourceRef"/> to a live Godot <see cref="global::Godot.Resource"/> on the
        /// editor main thread (via <c>ResourceLoader.Load</c> / <c>GodotObject.InstanceFromId</c>). Installed
        /// by the editor boot under <c>#if TOOLS</c> (see <c>Tool_Resource.InstallReflectionResolver</c>). When
        /// <c>null</c> (a plain unit-test host with no Godot runtime), <see cref="Deserialize"/> of a non-empty
        /// ref returns <c>null</c> with a logged note instead of throwing.
        /// </summary>
        public static ResourceResolverDelegate? ResourceResolver { get; set; }

        /// <param name="resourceRef">The reference to resolve (already validated as non-empty).</param>
        /// <param name="resource">The resolved live resource on success; otherwise <c>null</c>.</param>
        /// <param name="error">A human-readable failure reason when resolution fails; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> when a live resource was resolved; otherwise <c>false</c>.</returns>
        public delegate bool ResourceResolverDelegate(ResourceRef resourceRef, out object? resource, out string? error);

        // A Resource reference is a leaf — there is no nested object graph to descend into on the wire.
        // Turning cascade off routes deserialization of the value through DeserializeValueAsJsonElement (which
        // we override) instead of the field/property structural walk, matching how the ref is serialized.
        public override bool AllowCascadeSerialization => false;
        public override bool AllowSetValue => true;

        // Treat a Resource-ref JSON node (e.g. {"resourcePath":"res://…"}) as atomic on the merge-patch
        // (Reflector.TryPatch) path so it routes through SetValue/Deserialize and resolves to the live
        // Resource, instead of being structurally descended key-by-key. Mirrors Unity-MCP's
        // UnityEngine_Object_ReflectionConverter.TreatJsonObjectAsAtomicValue => true.
        public override bool TreatJsonObjectAsAtomicValue(Type type) => true;

        public override object? Deserialize(
            Reflector reflector,
            SerializedMember data,
            Type? fallbackType = null,
            string? fallbackName = null,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null,
            DeserializationContext? context = null)
        {
            var targetType = fallbackType ?? typeof(T);
            return ResolveFromJson(reflector, data.valueJsonElement, targetType, depth, logs);
        }

        protected override object? DeserializeValueAsJsonElement(
            Reflector reflector,
            SerializedMember data,
            Type type,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null)
        {
            return ResolveFromJson(reflector, data.valueJsonElement, type, depth, logs);
        }

        /// <summary>
        /// Map a <see cref="ResourceRef"/>-shaped JSON value to a live resource. A <c>null</c>/absent value or
        /// an empty ref resolves to <c>null</c> (clearing the property — never throws). A non-empty ref is
        /// handed to the injected <see cref="ResourceResolver"/>; with no resolver installed it returns
        /// <c>null</c> with a logged note so a plain unit-test host degrades gracefully.
        /// </summary>
        object? ResolveFromJson(Reflector reflector, JsonElement? valueJsonElement, Type targetType, int depth, Logs? logs)
        {
            // No value (or explicit JSON null) => assign null, clearing the property.
            if (valueJsonElement == null || valueJsonElement.Value.ValueKind == JsonValueKind.Null)
                return null;

            ResourceRef? resourceRef;
            try
            {
                resourceRef = reflector.JsonSerializer.Deserialize<ResourceRef>(valueJsonElement.Value);
            }
            catch (Exception ex)
            {
                logs?.Error($"Failed to read a ResourceRef for '{targetType.GetTypeShortName()}': {ex.Message}", depth);
                return null;
            }

            // An empty/absent ref clears the property (assign null) rather than failing.
            if (resourceRef == null || !resourceRef.IsValid())
                return null;

            var resolver = ResourceResolver;
            if (resolver == null)
            {
                // Pure-managed host (no Godot runtime): the live ResourceLoader.Load path is unavailable.
                logs?.Warning(
                    $"No ResourceResolver is installed; cannot resolve {resourceRef} to a live Resource " +
                    $"for '{targetType.GetTypeShortName()}' (this is expected outside the editor).", depth);
                return null;
            }

            if (!resolver(resourceRef, out var resolved, out var error))
            {
                logs?.Error($"Could not resolve {resourceRef}: {error ?? "unknown error"}.", depth);
                return null;
            }

            if (resolved == null)
                return null;

            // Guard the inheritance: a res:// path could load a resource of an unrelated type.
            if (!targetType.IsInstanceOfType(resolved))
            {
                logs?.Error(
                    $"Resolved {resourceRef} to a '{resolved.GetType().GetTypeShortName()}', which is not " +
                    $"assignable to '{targetType.GetTypeShortName()}'.", depth);
                return null;
            }

            logs?.Success($"Resolved {resourceRef} to a live '{targetType.GetTypeShortName()}'.", depth);
            return resolved;
        }

        protected override SerializedMember InternalSerialize(
            Reflector reflector,
            object? obj,
            Type type,
            string? name = null,
            bool recursive = true,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null,
            SerializationContext? context = null)
        {
            if (obj == null)
                return SerializedMember.Null(type, name);

            // A Resource is described on the wire by a ResourceRef (res:// path preferred, else its loaded
            // instance id) — never a deep serialization of the resource graph. Reading ResourcePath /
            // GetInstanceId is a native Godot call, so this branch only runs against a real Resource at
            // editor runtime; the pure-managed ResourceRef shaping is unit-tested via ToResourceRef below.
            var resourceRef = ToResourceRef(obj as global::Godot.Resource);
            return SerializedMember.FromValue(reflector, type, resourceRef, name);
        }

        /// <summary>
        /// Build the wire <see cref="ResourceRef"/> for a live resource: its <c>res://</c> path when it has
        /// one (the stable identity of an on-disk asset), else its loaded instance id. A <c>null</c> resource
        /// maps to an empty ref. Reads native Godot members, so it runs only at editor runtime.
        /// </summary>
        public static ResourceRef ToResourceRef(global::Godot.Resource? resource)
        {
            if (resource == null)
                return new ResourceRef();

            var path = resource.ResourcePath;
            if (!string.IsNullOrEmpty(path))
                return new ResourceRef(path);

            return new ResourceRef(resource.GetInstanceId());
        }
    }
}
