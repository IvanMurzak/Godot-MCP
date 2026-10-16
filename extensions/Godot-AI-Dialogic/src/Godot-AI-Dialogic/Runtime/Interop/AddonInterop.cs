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
using Godot.Collections; // unqualified Dictionary => Godot.Collections.Dictionary (avoids the namespace shadow)

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// Editor-only dynamic resolver for the <b>Dialogic</b> addon's GDScript <c>class_name</c> resource types.
    /// A Class-B extension takes NO compile-time dependency on the addon, so its classes are resolved BY STRING
    /// NAME at runtime through Godot's reflection-ish surface and driven with <c>GodotObject.Set/Get/Call</c>.
    ///
    /// <para>
    /// <b>This whole helper is behind <c>#if TOOLS</c> and is E2E-verified, NOT unit-tested</b>: every method
    /// touches a Godot static facade (<c>ProjectSettings.GetGlobalClassList()</c>, <c>ResourceLoader.Exists</c>,
    /// <c>GD.Load</c>) and/or constructs a <c>Resource</c>, so it can neither compile into the no-GodotSharp test
    /// csproj nor run in a no-Godot xUnit host. What IS pure-managed and unit-tested is the addon's name/member
    /// CONSTANTS (<see cref="DialogicAddon"/>) and the <see cref="AddonGateResult"/> shape.
    /// </para>
    ///
    /// <para>
    /// <b>Namespace-shadow note.</b> The package namespace <c>com.IvanMurzak.Godot.MCP.Dialogic</c> contains
    /// <c>com.IvanMurzak.Godot</c>, so an inline <c>Godot.</c>-qualified type (e.g.
    /// <c>Godot.Collections.Dictionary</c>) would bind to that ancestor namespace and fail to compile. This file
    /// imports <c>Godot.Collections</c> and uses an unqualified <c>Dictionary</c> to dodge it.
    /// </para>
    /// </summary>
    public static class AddonInterop
    {
        /// <summary>Resolve a GDScript <c>class_name</c> to its <c>res://</c> script path via the global class list.</summary>
        public static string? ResolveGlobalClassPath(string className)
        {
            foreach (Dictionary entry in ProjectSettings.GetGlobalClassList())
                if (entry.TryGetValue("class", out var c) && c.AsString() == className)
                    return entry.TryGetValue("path", out var p) ? p.AsString() : null;
            return null;
        }

        /// <summary>True when a GDScript <c>class_name</c> of the given name is registered (the addon is installed).</summary>
        public static bool GlobalClassExists(string className) =>
            ResolveGlobalClassPath(className) != null;

        /// <summary>
        /// Instantiate a GDScript <c>class_name</c> RESOURCE (e.g. <c>DialogicTimeline</c> / <c>DialogicCharacter</c>,
        /// both extend <c>Resource</c>) by name, or null when the addon isn't installed / the script is missing.
        /// </summary>
        public static Resource? InstantiateScriptResource(string className)
        {
            var path = ResolveGlobalClassPath(className);
            if (path == null || !ResourceLoader.Exists(path)) return null;
            var script = GD.Load<GDScript>(path);
            return script?.New().As<Resource>();
        }
    }
}
#endif
