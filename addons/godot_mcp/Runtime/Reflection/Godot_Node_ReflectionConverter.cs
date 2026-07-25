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
    /// ReflectorNet converter for Godot <see cref="global::Godot.Node"/> (and every <c>Node</c>-derived type
    /// — <c>Control</c>/<c>Node2D</c>/<c>Node3D</c>/…). The scene-tree sibling of
    /// <see cref="Godot_Resource_ReflectionConverter{T}"/>: a node crosses the MCP boundary as a lightweight
    /// <see cref="NodeRef"/> (instance id preferred, else scene-tree path), never a deep serialization of the
    /// node graph.
    ///
    /// <para>
    /// <b>Why this exists</b> (the second defect reported in issue #292): with nothing registered for
    /// <c>Godot.Node</c>, an instance call such as
    /// <c>reflection-method-call { targetObject: { typeName: "Godot.Control", value: { instanceId: N } } }</c>
    /// fell through to ReflectorNet's generic converter, which tried to read <c>{"instanceId": N}</c> as a
    /// nested <c>SerializedMember</c>, swallowed the resulting <c>JsonException</c> and returned the type's
    /// default — <c>null</c>. The caller then saw only the generic
    /// <c>'targetObject' deserialized instance is null</c>, so every instance method (e.g.
    /// <c>Control.AddThemeConstantOverride</c>) was unreachable. Routing through this converter resolves the
    /// ref to the LIVE node instead.
    /// </para>
    ///
    /// <para>
    /// <b>Failure policy — deliberately louder than the Resource sibling.</b> An explicit JSON <c>null</c>
    /// (or an absent value) still deserializes to <c>null</c>, because assigning null is a legitimate way to
    /// clear a <c>Node</c>-typed member. But a NON-EMPTY ref that cannot be resolved <b>throws</b> rather
    /// than degrading to <c>null</c> — there is no reading of "here is instanceId 123" under which silently
    /// substituting null is what the caller meant, and a silent substitution is exactly the failure mode
    /// issue #292 is about. (<see cref="Godot_Resource_ReflectionConverter{T}"/> predates that decision and
    /// keeps its log-and-null behaviour; changing it is out of scope here.)
    /// </para>
    ///
    /// <para>
    /// <b>#if TOOLS split:</b> this type is pure-managed (a <see cref="Type"/> token plus a
    /// <see cref="NodeRef"/> data model). The live <c>InstanceFromId</c> / <c>GetNodeOrNull</c> resolution —
    /// a native Godot call that must run on the editor main thread — is injected via
    /// <see cref="NodeResolver"/> by the editor boot (see <c>Tool_Node.InstallReflectionResolver</c>). With
    /// no resolver installed (a plain unit-test host) a non-empty ref throws a clear
    /// "no resolver installed" error instead of pretending to succeed.
    /// </para>
    /// </summary>
    public class Godot_Node_ReflectionConverter : Godot_Node_ReflectionConverter<global::Godot.Node> { }

    public class Godot_Node_ReflectionConverter<T> : GenericReflectionConverter<T>
        where T : global::Godot.GodotObject
    {
        /// <summary>
        /// Resolves a <see cref="NodeRef"/> to a live <see cref="global::Godot.Node"/> on the editor main
        /// thread. Installed by the editor boot under <c>#if TOOLS</c>.
        /// </summary>
        public static NodeResolverDelegate? NodeResolver { get; set; }

        /// <param name="nodeRef">The reference to resolve (already validated as non-empty).</param>
        /// <param name="node">The resolved live node on success; otherwise <c>null</c>.</param>
        /// <param name="error">A human-readable failure reason when resolution fails; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> when a live node was resolved; otherwise <c>false</c>.</returns>
        public delegate bool NodeResolverDelegate(NodeRef nodeRef, out object? node, out string? error);

        // A node reference is a leaf on the wire — see Godot_Resource_ReflectionConverter for the rationale.
        public override bool AllowCascadeSerialization => false;
        public override bool AllowSetValue => true;
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
            return ResolveFromJson(reflector, data.valueJsonElement, fallbackType ?? typeof(T));
        }

        protected override object? DeserializeValueAsJsonElement(
            Reflector reflector,
            SerializedMember data,
            Type type,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null)
        {
            return ResolveFromJson(reflector, data.valueJsonElement, type);
        }

        /// <summary>
        /// Map a <see cref="NodeRef"/>-shaped JSON value to a live node. A <c>null</c>/absent value resolves
        /// to <c>null</c> (clearing the member). Anything else must resolve, or this throws.
        /// </summary>
        object? ResolveFromJson(Reflector reflector, JsonElement? valueJsonElement, Type targetType)
        {
            if (valueJsonElement == null || valueJsonElement.Value.ValueKind == JsonValueKind.Null)
                return null;

            NodeRef? nodeRef;
            try
            {
                nodeRef = reflector.JsonSerializer.Deserialize<NodeRef>(valueJsonElement.Value);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Could not read a Node reference for '{targetType.GetTypeShortName()}' from " +
                    $"{valueJsonElement.Value.GetRawText()}: {ex.Message}. {RefShapeHelp}");
            }

            string? refError = null;
            if (nodeRef == null || !nodeRef.IsValid(out refError))
                throw new ArgumentException(
                    $"Could not read a Node reference for '{targetType.GetTypeShortName()}' from " +
                    $"{valueJsonElement.Value.GetRawText()}: {refError ?? "the reference is empty"}. {RefShapeHelp}");

            var resolver = NodeResolver;
            if (resolver == null)
                throw new InvalidOperationException(
                    $"Cannot resolve {nodeRef} to a live '{targetType.GetTypeShortName()}': no Node resolver is " +
                    "installed (this happens outside a running Godot editor).");

            if (!resolver(nodeRef, out var resolved, out var error) || resolved == null)
                throw new ArgumentException($"Could not resolve {nodeRef}: {error ?? "unknown error"}.");

            // Guard the inheritance: an instance id / path can name a node of an unrelated type.
            if (!targetType.IsInstanceOfType(resolved))
                throw new ArgumentException(
                    $"Resolved {nodeRef} to a '{resolved.GetType().GetTypeShortName()}', which is not " +
                    $"assignable to '{targetType.GetTypeShortName()}'.");

            return resolved;
        }

        static string RefShapeHelp =>
            $"A Node is referenced as {{\"{NodeRef.NodeRefProperty.InstanceId}\": <id>}} or " +
            $"{{\"{NodeRef.NodeRefProperty.Path}\": \"/root/Main/Player\"}}.";

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

            // A node is described on the wire by a NodeRef — never a deep serialization of the scene graph.
            // Reading GetInstanceId / GetPath is a native Godot call, so this branch only runs against a real
            // node at editor runtime; the pure-managed ref shaping is unit-tested via ToNodeRef below.
            return SerializedMember.FromValue(reflector, type, ToNodeRef(obj as global::Godot.Node), name);
        }

        /// <summary>
        /// Build the wire <see cref="NodeRef"/> for a live node: its instance id (the stable identity of a
        /// live node — see <see cref="NodeRef"/>'s priority note) plus its scene-tree path for readability.
        /// A <c>null</c> node maps to an empty ref. Reads native Godot members, so it runs only at editor
        /// runtime.
        /// </summary>
        public static NodeRef ToNodeRef(global::Godot.Node? node)
        {
            if (node == null)
                return new NodeRef();

            return new NodeRef(node.GetInstanceId())
            {
                Path = node.IsInsideTree() ? node.GetPath().ToString() : null,
            };
        }
    }
}
