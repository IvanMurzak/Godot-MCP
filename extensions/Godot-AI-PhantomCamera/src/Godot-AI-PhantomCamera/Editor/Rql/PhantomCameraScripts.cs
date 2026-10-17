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
using System.Collections.Generic;
using Godot;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// The GDScript global classes (<c>class_name</c>s) of the project, read once per query from
    /// <c>ProjectSettings.GetGlobalClassList()</c>. A GDScript node reports its native base from <c>GetClass()</c>
    /// ("Node"), so its Phantom Camera class comes from its script and the script's base scripts.
    /// </summary>
    internal sealed class PhantomCameraScripts
    {
        private readonly Dictionary<string, string> _classByPath = new Dictionary<string, string>();
        private readonly HashSet<string> _classes = new HashSet<string>();

        public PhantomCameraScripts()
        {
            foreach (global::Godot.Collections.Dictionary entry in ProjectSettings.GetGlobalClassList())
            {
                if (!entry.TryGetValue("class", out var cls)) continue;
                var name = cls.AsString();
                _classes.Add(name);
                if (entry.TryGetValue("path", out var path)) _classByPath[path.AsString()] = name;
            }
        }

        /// <summary>True when the global class is registered (the addon is installed and its scripts parsed).</summary>
        public bool Has(string className) => _classes.Contains(className);

        /// <summary>The global classes of the object's script and its base scripts, nearest first (empty without a script).</summary>
        public List<string> Chain(GodotObject o)
        {
            var chain = new List<string>();
            var script = o.GetScript().As<Script>();
            while (script != null)
            {
                if (_classByPath.TryGetValue(script.ResourcePath, out var cls)) chain.Add(cls);
                script = script.GetBaseScript();
            }
            return chain;
        }
    }
}
#endif
