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

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// Dialogic's GDScript global classes (<c>class_name</c>s), found in <c>ProjectSettings.GetGlobalClassList()</c>: whether
    /// one is registered (the addon is installed) and a new instance of one (Dialogic resources have no C# type and are not
    /// in ClassDB, so <c>editor | call CreateResource</c> cannot make them).
    /// </summary>
    internal static class DialogicScripts
    {
        public static string? PathOf(string className)
        {
            foreach (global::Godot.Collections.Dictionary entry in ProjectSettings.GetGlobalClassList())
                if (entry.TryGetValue("class", out var c) && c.AsString() == className)
                    return entry.TryGetValue("path", out var p) ? p.AsString() : null;
            return null;
        }

        public static bool Has(string className) => PathOf(className) != null;

        public static Resource New(string className)
        {
            var path = PathOf(className);
            var script = path != null && ResourceLoader.Exists(path) ? GD.Load<GDScript>(path) : null;
            return script?.New().As<Resource>()
                ?? throw new System.InvalidOperationException($"could not create a {className}: {DialogicNames.InstallHint}");
        }
    }
}
#endif
