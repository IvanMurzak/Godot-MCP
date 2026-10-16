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
using Godot.Collections;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// Dynamic-wrapper helper for the Phantom Camera addon — the Class-B "speak to the engine over names, not
    /// types" surface. Phantom Camera is a GDScript <c>class_name</c> addon, so its types register in the
    /// GLOBAL script-class list (NOT in <c>ClassDB</c>); this resolves them by string name + instantiates them
    /// via <c>GD.Load&lt;GDScript&gt;(path).New()</c>. The package therefore takes NO compile-time dependency
    /// on the addon (it never names a Phantom Camera type).
    ///
    /// <para>
    /// Lives behind <c>#if TOOLS</c>: it touches Godot static facades (<see cref="ProjectSettings"/>,
    /// <see cref="GD"/>, <see cref="ResourceLoader"/>) and CONSTRUCTS <see cref="Node"/>s, which P/Invoke and
    /// crash a no-Godot xUnit host. So it is excluded from the pure-managed test assembly and is E2E-verified
    /// instead. The pinned <c>snake_case</c> member names + enum-int values it is driven with ARE unit-tested
    /// (see <c>PhantomCameraEnums.cs</c> / <c>PhantomCameraEnumsTests</c>). Every method here is invoked ONLY
    /// from inside a <c>MainThread.Instance.Run(...)</c> delegate (editor main thread).
    /// </para>
    /// </summary>
    public static class AddonInterop
    {
        /// <summary>
        /// Resolve a GDScript <c>class_name</c> to its <c>res://</c> script path via the global script-class
        /// list, or null when the addon is not installed / the name is unknown.
        /// </summary>
        public static string? ResolveGlobalClassPath(string className)
        {
            foreach (Dictionary entry in ProjectSettings.GetGlobalClassList())
            {
                if (entry.TryGetValue("class", out var c) && c.AsString() == className)
                    return entry.TryGetValue("path", out var p) ? p.AsString() : null;
            }
            return null;
        }

        /// <summary>True when the GDScript <c>class_name</c> is registered (the addon is installed). The gate.</summary>
        public static bool GlobalClassExists(string className) =>
            ResolveGlobalClassPath(className) != null;

        /// <summary>
        /// Instantiate a Phantom Camera GDScript node BY NAME (<c>GD.Load&lt;GDScript&gt;(path).New()</c>), or
        /// null when the class can't be resolved/loaded. Constructs a <see cref="Node"/> → editor-only.
        /// </summary>
        public static Node? InstantiateScriptNode(string className)
        {
            var path = ResolveGlobalClassPath(className);
            if (path == null || !ResourceLoader.Exists(path)) return null;
            var script = GD.Load<GDScript>(path);
            return script?.New().As<Node>();
        }

        /// <summary>
        /// Reverse-resolve a node's attached script to its registered global <c>class_name</c>, or
        /// <see cref="GodotObject.GetClass"/> (the engine BASE class, e.g. <c>Node3D</c>) when the script is
        /// not a registered global class. Used so a tool result reports <c>PhantomCamera3D</c> rather than the
        /// base <c>Node3D</c> that <c>GetClass()</c> returns for a GDScript <c>class_name</c> node.
        /// </summary>
        public static string ResolveScriptClassName(Node node)
        {
            var script = node.GetScript().As<Script>();
            var path = script?.ResourcePath;
            if (string.IsNullOrEmpty(path)) return node.GetClass();

            foreach (Dictionary entry in ProjectSettings.GetGlobalClassList())
            {
                if (entry.TryGetValue("path", out var p) && p.AsString() == path
                    && entry.TryGetValue("class", out var c))
                    return c.AsString();
            }
            return node.GetClass();
        }
    }
}
#endif
