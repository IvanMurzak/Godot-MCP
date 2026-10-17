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
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Reflection;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The types an agent names (rql-schema "Node3D"): the Godot C# API (GodotSharp / GodotSharpEditor) plus the Godot
    /// object types and public structs / enums of the project assembly (and anything else loaded beside it, extensions
    /// included). One index per editor session, built on first use; lookups by short or full name, case-insensitive (the C#
    /// binding writes some Godot classes differently, e.g. CsgBox3D for CSGBox3D), best match first (exact case, then the
    /// Godot namespace, then project types).
    /// </summary>
    public static class GodotTypes
    {
        private static Dictionary<string, Type[]>? _index;

        private static string[]? _classes;

        /// <summary>Drop the index and the class list (plugin teardown: the index holds types of the collectible project assembly).</summary>
        public static void Reset()
        {
            _index = null;
            _classes = null;
        }

        /// <summary>Every Godot class (ClassDB), sorted ordinally; read once per editor session.</summary>
        public static string[] GodotClasses() =>
            _classes ??= ClassDB.GetClassList().OrderBy(c => c, StringComparer.Ordinal).ToArray();

        /// <summary>Candidate types for a name, best first.</summary>
        public static IReadOnlyList<Type> Candidates(string name) =>
            Index().TryGetValue(name, out var hits)
                ? hits.OrderBy(t => t.Name == name || t.FullName == name ? 0 : 1).ThenBy(t => IsGodot(t) ? 0 : 1).ToList()
                : (IReadOnlyList<Type>)Array.Empty<Type>();

        /// <summary>Every indexed short name (for "did you mean" suggestions).</summary>
        public static IReadOnlyCollection<string> ShortNames() => Index().Keys.Where(k => k.IndexOf('.') < 0).ToList();

        private static Dictionary<string, Type[]> Index()
        {
            if (_index != null) return _index;
            var byName = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
            void Add(Type t)
            {
                if (!t.IsVisible || t.IsGenericTypeDefinition) return;
                foreach (var key in new[] { t.Name, t.FullName })
                {
                    if (key == null) continue;
                    if (!byName.TryGetValue(key, out var list)) byName[key] = list = new List<Type>(1);
                    if (!list.Contains(t)) list.Add(t);
                }
            }
            foreach (var t in TypeModel.LoadableTypes(typeof(GodotObject).Assembly))
                if (t.Namespace == "Godot" && (typeof(GodotObject).IsAssignableFrom(t) || t.IsValueType)) Add(t);
            foreach (var t in TypeModel.LoadableTypes(typeof(EditorInterface).Assembly))
                if (t.Namespace == "Godot" && typeof(GodotObject).IsAssignableFrom(t)) Add(t);
            // The project assembly (this addon, the user's scripts, extensions compiled into it) and default-context
            // libraries: their Godot objects, plus this assembly's public structs and enums.
            foreach (var assembly in GodotAssemblyUtils.AllAssemblies)
            {
                if (GodotAssemblyUtils.IsSkippedForDiscovery(assembly, typeof(GodotTypes).Assembly)) continue;
                foreach (var t in TypeModel.LoadableTypes(assembly))
                    if (typeof(GodotObject).IsAssignableFrom(t) || (t.IsValueType && t.IsPublic && assembly == typeof(GodotTypes).Assembly)) Add(t);
            }
            return _index = byName.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsGodot(Type t) => t.Namespace == "Godot";
    }
}
#endif
