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
    /// <b>Why <see cref="AppDomain.GetAssemblies"/> and NOT <see cref="AssemblyLoadContext.Default"/>
    /// (issue #102).</b> The Godot editor loads the project assembly into its own <b>collectible
    /// plugin <see cref="AssemblyLoadContext"/></b>, not the default one — and that project assembly
    /// is exactly where this addon, its <c>[AiToolType]</c> tool classes, and any extension
    /// <c>.cs</c> live (Godot globs all project <c>.cs</c> into one assembly). Its NuGet dependencies
    /// (McpPlugin / ReflectorNet) resolve into that same context. Enumerating only
    /// <see cref="AssemblyLoadContext.Default"/> therefore misses the very assembly hosting the
    /// tools, and the builder's attribute scan silently registers ZERO tools (empty
    /// <c>tools/list</c>, <c>ping</c> not found). Unity-MCP's <c>AssemblyUtils.AllAssemblies</c> can
    /// enumerate the default context because Unity loads user code there; Godot does not.
    /// <see cref="AppDomain.GetAssemblies"/> spans every load context in the process, so both the
    /// plugin-context project assembly and any default-context extension library are reachable.
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
        /// The managed assemblies currently loaded in the process across ALL load contexts (Godot's
        /// collectible plugin context included — that is where the project assembly hosting the tools
        /// lives; see issue #102), snapshotted into an array. Dynamic (in-memory) assemblies are
        /// excluded — they have no scannable on-disk types relevant to tool/module discovery and a few
        /// (e.g. ref-emit) throw on <see cref="Assembly.GetTypes"/>. The snapshot is taken eagerly (the
        /// returned array does not observe later loads) so a discovery pass walks a stable set.
        /// </summary>
        public static Assembly[] AllAssemblies =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .ToArray();
    }
}
