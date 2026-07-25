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
        /// native Godot call; the converter is only ever invoked from inside a tool's
        /// <c>MainThread.Instance.Run</c>, so the delegate runs on the editor main thread without an extra
        /// marshal. Called once from <c>GodotMcpConnection.Start</c> after the reflector is built; idempotent
        /// (re-assigns the same delegate). Mirrors <c>Tool_Resource.InstallReflectionResolver</c>.
        /// </para>
        /// </summary>
        internal static void InstallReflectionResolver()
        {
            Godot_Node_ReflectionConverter.NodeResolver = static (NodeRef nodeRef, out object? node, out string? error) =>
            {
                node = ResolveNode(nodeRef, out error);
                return node != null;
            };
        }
    }
}
#endif
