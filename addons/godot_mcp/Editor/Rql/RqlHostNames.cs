/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The pure halves of three RQL host hooks (the Godot halves are in <c>RqlGodot.Names.cs</c>): the member names the host's
    /// types declare (<c>RqlEngine.IsKnownMember</c>), the Godot names of C# binding types (<c>RqlEngine.TypeDisplayName</c>)
    /// and the alias names of an enum's entries (<c>RqlEngine.EnumAliases</c>). No Godot engine calls: unit-tested.
    /// </summary>
    internal static class RqlHostNames
    {
        /// <summary>
        /// The public C# property of <paramref name="type"/> that a Godot snake_case property name stands for ("albedo_color"
        /// → AlbedoColor, "sky_material" → SkyMaterial), or null (no underscore, or no such property). Object literals of
        /// Godot classes are built from C# members, and agents write the Godot names they know from the docs.
        /// </summary>
        public static string? CSharpMember(Type type, string snakeName)
        {
            if (snakeName.IndexOf('_') < 0) return null;
            try { return type.GetProperty(snakeName.Replace("_", ""), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.Name; }
            catch (AmbiguousMatchException) { return null; }
        }

        /// <summary>The public instance properties and fields of <paramref name="types"/>, case-insensitive.</summary>
        public static HashSet<string> MemberNames(IEnumerable<Type> types)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in types)
            {
                try
                {
                    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) names.Add(p.Name);
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) names.Add(f.Name);
                }
                catch (Exception) { } // a type whose members cannot load (a missing dependency) adds nothing
            }
            return names;
        }

        /// <summary>
        /// <c>RqlEngine.TypeDisplayName</c>: the Godot (ClassDB) name of a Godot class whose C# binding is spelt differently,
        /// from the binding's <see cref="GodotClassNameAttribute"/> (GPUParticles3D for GpuParticles3D, CSGBox3D for CsgBox3D,
        /// Object for GodotObject); null keeps the C# name (Node3D, project script classes).
        /// </summary>
        public static string? GodotClassName(Type type) =>
            type.GetCustomAttribute<GodotClassNameAttribute>(inherit: false)?.Name is { Length: > 0 } name && name != type.Name ? name : null;

        /// <summary>
        /// Alias → entry name for <paramref name="enumType"/> from other names of its values (Godot's constants
        /// "PRESET_FULL_RECT" and inspector labels "Per-Pixel" for the C# entries FullRect / PerPixel), or null for none. A name
        /// that is already an entry name (any case) is left to RQL; a name that stands for two different entries is dropped.
        /// </summary>
        public static IReadOnlyDictionary<string, string>? EnumAliases(Type enumType, IEnumerable<(string name, long value)> names)
        {
            if (!enumType.IsEnum) return null;
            var entryNames = Enum.GetNames(enumType);
            var entries = new HashSet<string>(entryNames, StringComparer.OrdinalIgnoreCase);
            var byValue = new Dictionary<long, string>();
            foreach (var entry in entryNames)
            {
                long value;
                try { value = Convert.ToInt64(Enum.Parse(enumType, entry)); }
                catch (OverflowException) { continue; } // a ulong value no Godot constant can carry
                if (!byValue.ContainsKey(value)) byValue[value] = entry;
            }
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in names)
            {
                if (string.IsNullOrWhiteSpace(name) || entries.Contains(name) || !byValue.TryGetValue(value, out var entry)) continue;
                if (aliases.TryGetValue(name, out var known) && known != entry) ambiguous.Add(name);
                else aliases[name] = entry;
            }
            foreach (var name in ambiguous) aliases.Remove(name);
            return aliases.Count == 0 ? null : new ReadOnlyDictionary<string, string>(aliases);
        }
    }
}
#endif
