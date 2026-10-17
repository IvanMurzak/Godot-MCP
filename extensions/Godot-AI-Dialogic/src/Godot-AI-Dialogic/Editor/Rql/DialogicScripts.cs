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
using System;
using System.Collections.Generic;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// The project's GDScript global classes (<c>class_name</c> to script path), read from
    /// <c>ProjectSettings.GetGlobalClassList()</c> once per source access (the source creates one per query). A GDScript
    /// node or resource reports its native base from <c>GetClass()</c>, so its Dialogic class comes from its script chain
    /// (<see cref="Chain"/>). The Beehave, Dialogic and PhantomCamera extensions each carry this same class: a shared source
    /// would define the type twice in a consumer that installs two of them.
    /// </summary>
    internal sealed class DialogicScripts
    {
        private readonly Dictionary<string, string> _pathByClass = new Dictionary<string, string>(StringComparer.Ordinal);

        public DialogicScripts()
        {
            foreach (global::Godot.Collections.Dictionary entry in ProjectSettings.GetGlobalClassList())
                if (entry.TryGetValue("class", out var name) && entry.TryGetValue("path", out var path))
                    _pathByClass[name.AsString()] = path.AsString();
        }

        /// <summary>True when the global class is registered (the addon is installed and its scripts parsed).</summary>
        public bool Has(string className) => _pathByClass.ContainsKey(className);

        /// <summary>The script path of a global class, or null.</summary>
        public string? PathOf(string className) => _pathByClass.TryGetValue(className, out var path) ? path : null;

        /// <summary>
        /// The global class names of the object's script and its base scripts, nearest first; null when it has none
        /// (a plain node allocates nothing).
        /// </summary>
        public static List<string>? Chain(GodotObject o)
        {
            List<string>? chain = null;
            for (var script = o.GetScript().As<Script>(); script != null; script = script.GetBaseScript())
            {
                var name = script.GetGlobalName().ToString();
                if (name.Length > 0) (chain ??= new List<string>()).Add(name);
            }
            return chain;
        }

        /// <summary>A new instance of a global resource class (Dialogic resources have no C# type and are not in ClassDB).</summary>
        public Resource New(string className)
        {
            var path = PathOf(className);
            var script = path != null && ResourceLoader.Exists(path) ? GD.Load<GDScript>(path) : null;
            return script?.New().As<Resource>()
                ?? throw new InvalidOperationException($"could not create a {className}: {DialogicNames.InstallHint}");
        }
    }
}
#endif
