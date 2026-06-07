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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// Enumerates the managed assemblies loaded into the Godot editor process — the Godot analog of
    /// Unity-MCP's <c>AssemblyUtils.AllAssemblies</c>. Used as the scan set the
    /// <c>McpPluginBuilder</c> walks for <c>[AiToolType]</c>/<c>[AiTool]</c> registration AND, since
    /// issue #86, for <c>IReflectorModule</c> discovery
    /// (<c>.WithReflectorModulesFromAssembly(...)</c>) — so any loaded assembly (including future
    /// Godot-MCP extensions, unknown ahead of time) can contribute ReflectorNet converters /
    /// serialization-blacklist entries / scan-ignore rules with NO hardcoded extension list.
    ///
    /// <para>
    /// <b>Why <see cref="AssemblyLoadContext.Default"/>.</b> Godot loads the project/addon assembly —
    /// and, via <see cref="GodotMcpAssemblyResolver"/>, every NuGet dependency it resolves at editor
    /// runtime — into the <b>default</b> <see cref="AssemblyLoadContext"/>. A Godot-MCP extension ships
    /// as additional <c>.cs</c> compiled into the same project game assembly (Godot globs all project
    /// <c>.cs</c> into one assembly) or as a referenced library loaded into the same default context, so
    /// <see cref="AssemblyLoadContext.Default"/>'s loaded set is exactly where an extension's
    /// <see cref="System.Reflection.Assembly"/> appears. Enumerating it (rather than only the addon
    /// assembly) is what lets the discovery reach extension-contributed modules.
    /// </para>
    ///
    /// <para>
    /// This type is pure-BCL (no Godot API, no <c>#if TOOLS</c>) so it is unit-testable in the
    /// plain-xUnit host. The heavy assemblies the builder must never type-enumerate (the BCL, the reused
    /// McpPlugin/ReflectorNet/R3/SignalR stack, the test asmdefs) are pruned by the
    /// <c>.IgnoreAssemblies(...)</c> call the connection passes to the builder — NOT here — mirroring the
    /// Unity reference, which enumerates broadly and prunes at the builder.
    /// </para>
    /// </summary>
    public static class GodotAssemblyUtils
    {
        /// <summary>
        /// The managed assemblies currently loaded in the default <see cref="AssemblyLoadContext"/>,
        /// snapshotted into an array. Dynamic (in-memory) assemblies are excluded — they have no scannable
        /// on-disk types relevant to tool/module discovery and a few (e.g. ref-emit) throw on
        /// <see cref="Assembly.GetTypes"/>. The snapshot is taken eagerly (the returned array does not
        /// observe later loads) so a discovery pass walks a stable set.
        /// </summary>
        public static Assembly[] AllAssemblies =>
            AssemblyLoadContext.Default.Assemblies
                .Where(assembly => !assembly.IsDynamic)
                .ToArray();
    }
}
