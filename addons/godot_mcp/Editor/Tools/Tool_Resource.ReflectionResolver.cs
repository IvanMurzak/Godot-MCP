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
        /// <c>ResourceLoader.Load</c> / <c>InstanceFromId</c>) is a native Godot call; the converter is only
        /// ever invoked from inside a tool's <c>MainThread.Instance.Run</c> (e.g. <c>node-modify</c>), so the
        /// delegate runs on the editor main thread without an extra marshal. Called once from
        /// <c>GodotMcpConnection.Start</c> after the reflector is built; idempotent (re-assigns the same
        /// delegate).
        /// </summary>
        internal static void InstallReflectionResolver()
        {
            Godot_Resource_ReflectionConverter.ResourceResolver = static (ResourceRef resourceRef, out object? resource, out string? error) =>
            {
                var resolved = ResolveResource(resourceRef, out _, out error);
                resource = resolved;
                return resource != null;
            };
        }
    }
}
#endif
