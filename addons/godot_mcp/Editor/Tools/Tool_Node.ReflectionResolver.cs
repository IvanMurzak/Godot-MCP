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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Node
    {
        /// <summary>
        /// Wire the editor-side scene-tree resolution into <see cref="Godot_Node_ReflectionConverter{T}"/>
        /// so that converter — which is pure-managed and lives outside <c>#if TOOLS</c> — can turn a
        /// <see cref="NodeRef"/> into a LIVE <c>Node</c>. This is what makes instance-method reflection calls
        /// (<c>reflection-method-call</c> with <c>targetObject: {"instanceId": N}</c>) reach a real node
        /// instead of deserializing to null (issue #292).
        ///
        /// <para>
        /// The lookup itself (<see cref="ResolveNode"/> → <c>InstanceFromId</c> / <c>GetNodeOrNull</c>) is a
        /// native Godot scene-tree call and <see cref="ResolveNode"/> is documented main-thread-only, so the
        /// delegate marshals explicitly. That is NOT redundant: <c>reflection-method-call</c> deserializes its
        /// arguments inside the action it may run OFF the main thread (<c>executeInMainThread: false</c>), so
        /// the converter is reachable from a worker thread. <c>MainThread.Instance.Run</c> executes inline
        /// when already on the main thread (<c>GodotMainThread.RunAsync</c> short-circuits on
        /// <c>IsMainThread</c>), so the common path pays nothing.
        /// </para>
        ///
        /// <para>
        /// Called once from <c>GodotMcpConnection.Start</c> after the reflector is built; idempotent
        /// (re-assigns the same delegate). Mirrors <c>Tool_Resource.InstallReflectionResolver</c>.
        /// </para>
        /// </summary>
        internal static void InstallReflectionResolver()
        {
            Godot_Node_ReflectionConverter.NodeResolver = static (NodeRef nodeRef, out object? node, out string? error) =>
            {
                (node, error) = MainThread.Instance.Run(() =>
                {
                    var found = ResolveNode(nodeRef, out var inner);
                    return ((object?)found, inner);
                });
                return node != null;
            };
        }
    }
}
#endif
