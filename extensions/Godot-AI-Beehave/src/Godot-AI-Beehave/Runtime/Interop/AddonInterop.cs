/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Godot;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// Editor-only dynamic wrapper that resolves + instantiates the third-party Beehave addon's classes BY
    /// STRING NAME — the package never names a Beehave type at compile time. Beehave registers GDScript
    /// <c>class_name</c> types in the GLOBAL script-class list (NOT in <c>ClassDB</c>), so the resolver uses
    /// <c>ProjectSettings.GetGlobalClassList()</c> + <c>GD.Load&lt;GDScript&gt;(path).New()</c> — the
    /// <c>kind: gdscript</c> path.
    ///
    /// <para>
    /// <b>Entirely behind <c>#if TOOLS</c> and E2E-verified, NEVER unit-tested.</b> Every method touches a
    /// Godot static facade (<c>ProjectSettings</c>, <c>ResourceLoader</c>, <c>GD</c>) and/or constructs a
    /// <c>Node</c>, which P/Invokes and faults in a no-Godot xUnit host. What IS pure-managed and unit-tested is
    /// the addon's name/member/enum-int CONSTANTS (<see cref="BeehaveEnums"/>) and the <see cref="AddonGate"/>
    /// shape — the constants ARE the contract; this is the editor-only plumbing that consumes them. Members are
    /// driven via <c>GodotObject.Set/Get/Call</c> with GDScript <c>snake_case</c> names.
    /// </para>
    /// </summary>
    internal static class AddonInterop
    {
        /// <summary>Resolve a GDScript <c>class_name</c> to its <c>res://</c> path via the global class list.</summary>
        public static string? ResolveGlobalClassPath(string className)
        {
            foreach (global::Godot.Collections.Dictionary entry in ProjectSettings.GetGlobalClassList())
                if (entry.TryGetValue("class", out var c) && c.AsString() == className)
                    return entry.TryGetValue("path", out var p) ? p.AsString() : null;
            return null;
        }

        /// <summary>True when the named GDScript <c>class_name</c> is registered (i.e. the addon is installed).</summary>
        public static bool GlobalClassExists(string className) =>
            ResolveGlobalClassPath(className) != null;

        /// <summary>
        /// Instantiate a Beehave node by its <c>class_name</c> (the gate must have run first). Returns null when
        /// the class is not registered or its script cannot be loaded.
        /// </summary>
        public static Node? InstantiateScriptNode(string className)
        {
            var path = ResolveGlobalClassPath(className);
            if (path == null || !ResourceLoader.Exists(path))
                return null;
            var script = GD.Load<GDScript>(path);
            return script?.New().As<Node>();
        }

        /// <summary>
        /// Reverse-resolve a live node's Beehave <c>class_name</c> from its attached script's resource path via
        /// the global class list. Used by <c>beehave-get</c>'s structure dump — <c>node.GetClass()</c> reports a
        /// GDScript node's native BASE (<c>"Node"</c>), not its script class, so this is the reliable name
        /// source. Returns null for a node with no class-listed script.
        /// </summary>
        public static string? ScriptClassName(Node node)
        {
            var script = node.GetScript().As<Script>();
            var path = script?.ResourcePath;
            if (string.IsNullOrEmpty(path))
                return null;
            foreach (global::Godot.Collections.Dictionary entry in ProjectSettings.GetGlobalClassList())
                if (entry.TryGetValue("path", out var p) && p.AsString() == path)
                    return entry.TryGetValue("class", out var c) ? c.AsString() : null;
            return null;
        }
    }
}
#endif
