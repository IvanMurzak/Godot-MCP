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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Resource
    {
        /// <summary>
        /// Wire the editor-side resource resolution into <see cref="Godot_Resource_ReflectionConverter{T}"/>
        /// so that converter — which is pure-managed and lives outside <c>#if TOOLS</c> — can turn a
        /// <see cref="ResourceRef"/> into a LIVE <see cref="Resource"/> when <c>node-modify</c> assigns a
        /// <c>Resource</c>-typed property. The load itself (<see cref="ResolveResource"/> →
        /// <c>ResourceLoader.Load</c> / <c>InstanceFromId</c>) is a native Godot call, so the delegate
        /// marshals explicitly.
        ///
        /// <para>
        /// That marshal is NOT redundant, despite what this comment used to claim: while <c>node-modify</c>
        /// does invoke the converter from inside its own <c>MainThread.Instance.Run</c>,
        /// <c>reflection-method-call</c> deserializes its arguments inside an action it may run OFF the main
        /// thread (<c>executeInMainThread: false</c>), so the converter is reachable from a worker.
        /// <c>MainThread.Instance.Run</c> executes inline when already on the main thread, so the common
        /// path pays nothing. Mirrors <c>Tool_Node.InstallReflectionResolver</c>.
        /// </para>
        ///
        /// <para>
        /// Called once from <c>GodotMcpConnection.Start</c> after the reflector is built; idempotent
        /// (re-assigns the same delegate).
        /// </para>
        /// </summary>
        internal static void InstallReflectionResolver()
        {
            Godot_Resource_ReflectionConverter.ResourceResolver = static (ResourceRef resourceRef, out object? resource, out string? error) =>
            {
                (resource, error) = MainThread.Instance.Run(() =>
                {
                    var found = ResolveResource(resourceRef, out _, out var inner);
                    return ((object?)found, inner);
                });
                return resource != null;
            };
        }
    }
}
#endif
