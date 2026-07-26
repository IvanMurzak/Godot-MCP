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
    /// <b>Failure policy — report, do not decide.</b> An unresolvable ref records a
    /// <see cref="LogType.Error"/> into the caller's <see cref="Logs"/> and yields <c>null</c>, exactly as
    /// <see cref="Godot_Resource_ReflectionConverter{T}"/> does. A converter cannot know what its caller
    /// wants done about a failure, so the CALL SITE sets policy:
    /// <c>reflection-method-call</c> refuses the invocation via <see cref="ReflectionArgumentGuard"/>
    /// (which is what makes issue #292's silent success loud), while <c>node-modify</c>'s merge patch
    /// reports the member and still applies the rest. An earlier revision threw from here instead; that made
    /// ONE bad member abort an entire <c>node-modify</c> patch, which is a worse answer than the one the
    /// caller asked for. An explicit JSON <c>null</c> is untouched — clearing a <c>Node</c>-typed member
    /// stays legitimate.
    /// </para>
    ///
    /// <para>
    /// <b>#if TOOLS split:</b> this type is pure-managed (a <see cref="Type"/> token plus a
    /// <see cref="NodeRef"/> data model). The live <c>InstanceFromId</c> / <c>GetNodeOrNull</c> resolution —
    /// a native Godot call that must run on the editor main thread — is injected via
    /// <see cref="NodeResolver"/> by the editor boot (see <c>Tool_Node.InstallReflectionResolver</c>). With
    /// no resolver installed (a plain unit-test host) a non-empty ref logs a WARNING and yields <c>null</c>
    /// — that is an environment fact, not a bad request, so it must not fail a call on its own.
    /// </para>
    /// </summary>
    public class Godot_Node_ReflectionConverter : Godot_Node_ReflectionConverter<global::Godot.Node> { }

    public class Godot_Node_ReflectionConverter<T> : GenericReflectionConverter<T>
        where T : global::Godot.Node
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
            return ResolveFromJson(reflector, data.valueJsonElement, fallbackType ?? typeof(T), depth, logs);
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
        /// Map a <see cref="NodeRef"/>-shaped JSON value to a live node. A <c>null</c>/absent value resolves
        /// to <c>null</c> (clearing the member). Every other failure records an <see cref="LogType.Error"/>
        /// into <paramref name="logs"/> and yields <c>null</c>, leaving the call site to decide whether that
        /// is fatal — see the class doc.
        /// </summary>
        object? ResolveFromJson(Reflector reflector, JsonElement? valueJsonElement, Type targetType, int depth, Logs? logs)
        {
            if (valueJsonElement == null || valueJsonElement.Value.ValueKind == JsonValueKind.Null)
                return null;

            var raw = valueJsonElement.Value.GetRawText();

            NodeRef? nodeRef;
            try
            {
                nodeRef = reflector.JsonSerializer.Deserialize<NodeRef>(valueJsonElement.Value);
            }
            catch (Exception ex)
            {
                logs?.Error(
                    $"Could not read a Node reference for '{targetType.GetTypeShortName()}' from {raw}: " +
                    $"{ex.Message}. {RefShapeHelp}", depth);
                return null;
            }

            string? refError = null;
            if (nodeRef == null || !nodeRef.IsValid(out refError))
            {
                logs?.Error(
                    $"Could not read a Node reference for '{targetType.GetTypeShortName()}' from {raw}: " +
                    $"{refError ?? "the reference is empty"}. {RefShapeHelp}", depth);
                return null;
            }

            var resolver = NodeResolver;
            if (resolver == null)
            {
                // No resolver installed is an ENVIRONMENT fact, not a bad request — outside a running Godot
                // editor there is no scene tree to look in. Warning, so it never fails a call on its own.
                logs?.Warning(
                    $"No Node resolver is installed; cannot resolve {nodeRef} to a live " +
                    $"'{targetType.GetTypeShortName()}' (this is expected outside the editor).", depth);
                return null;
            }

            if (!resolver(nodeRef, out var resolved, out var error) || resolved == null)
            {
                logs?.Error($"Could not resolve {nodeRef}: {error ?? "unknown error"}.", depth);
                return null;
            }

            // Guard the inheritance: an instance id / path can name a node of an unrelated type.
            if (!targetType.IsInstanceOfType(resolved))
            {
                logs?.Error(
                    $"Resolved {nodeRef} to a '{resolved.GetType().GetTypeShortName()}', which is not " +
                    $"assignable to '{targetType.GetTypeShortName()}'.", depth);
                return null;
            }

            logs?.Success($"Resolved {nodeRef} to a live '{targetType.GetTypeShortName()}'.", depth);
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
        /// A <c>null</c> node maps to an empty ref (the only branch a plain unit-test host reaches).
        ///
        /// <para>
        /// <c>IsInsideTree</c> / <c>GetPath</c> / <c>GetInstanceId</c> are native scene-tree reads, so the
        /// non-null branch marshals onto the editor main thread for the same reason
        /// <c>Tool_Node.InstallReflectionResolver</c> does: <c>reflection-method-call</c> serializes a
        /// method's RESULT inside the action it may run off the main thread
        /// (<c>executeInMainThread: false</c>), so a method returning a <c>Node</c> reaches here from a
        /// worker. The marshal is free when already on the main thread.
        /// </para>
        /// </summary>
        public static NodeRef ToNodeRef(global::Godot.Node? node)
        {
            if (node == null)
                return new NodeRef();

            return MainThread.Instance.Run(() => new NodeRef(node.GetInstanceId())
            {
                Path = node.IsInsideTree() ? node.GetPath().ToString() : null,
            });
        }
    }
}
