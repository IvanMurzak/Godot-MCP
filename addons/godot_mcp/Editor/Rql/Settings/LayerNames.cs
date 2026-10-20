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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Settings
{
    /// <summary>
    /// Layer bit masks ⇄ layer names (pure, unit-tested). A property whose hint is one of Godot's layer hints
    /// (<c>collision_layer</c>, <c>collision_mask</c>, <c>visibility_layer</c>, <c>layers</c>, <c>navigation_layers</c> …)
    /// holds a bit mask; the names come from the project settings <c>layer_names/&lt;kind&gt;/layer_&lt;n&gt;</c> (1-based, like
    /// the inspector). A layer is written by its name or its number; an unnamed layer reads as its number.
    /// </summary>
    public static class LayerNames
    {
        /// <summary>One kind of layers: its project-settings section, how many layers it has and the property hint using it.</summary>
        public readonly struct Kind
        {
            public Kind(string name, int count, PropertyHint hint) { Name = name; Count = count; Hint = hint; }
            public string Name { get; }
            public int Count { get; }
            public PropertyHint Hint { get; }
        }

        /// <summary>Every layer kind, in the Project Settings → Layer Names order.</summary>
        public static readonly IReadOnlyList<Kind> Kinds = new[]
        {
            new Kind("2d_render", 20, PropertyHint.Layers2DRender),
            new Kind("2d_physics", 32, PropertyHint.Layers2DPhysics),
            new Kind("2d_navigation", 32, PropertyHint.Layers2DNavigation),
            new Kind("3d_render", 20, PropertyHint.Layers3DRender),
            new Kind("3d_physics", 32, PropertyHint.Layers3DPhysics),
            new Kind("3d_navigation", 32, PropertyHint.Layers3DNavigation),
            new Kind("avoidance", 32, PropertyHint.LayersAvoidance),
        };

        /// <summary>The layer kind a property hint stands for; false for any other hint.</summary>
        public static bool TryKindOf(PropertyHint hint, out Kind kind)
        {
            foreach (var k in Kinds)
                if (k.Hint == hint) { kind = k; return true; }
            kind = default;
            return false;
        }

        /// <summary>The project setting that names a layer: <c>layer_names/3d_physics/layer_2</c>.</summary>
        public static string SettingName(string kind, int layer) => $"layer_names/{kind}/layer_{layer.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>The bit of a layer (1-based).</summary>
        public static long Bit(int layer) => 1L << (layer - 1);

        /// <summary>
        /// The layers set in <paramref name="mask"/>, by name — or by number when unnamed (<paramref name="nameOf"/> returns
        /// null or empty) — lowest first. Bits above the kind's count are not listed (writes keep them).
        /// </summary>
        public static List<string> Names(long mask, Kind kind, Func<int, string?> nameOf)
        {
            var names = new List<string>();
            for (int layer = 1; layer <= kind.Count; layer++)
                if ((mask & Bit(layer)) != 0)
                    names.Add(nameOf(layer) is { Length: > 0 } name ? name : layer.ToString(CultureInfo.InvariantCulture));
            return names;
        }

        /// <summary>
        /// The mask <paramref name="value"/> describes: a list of layers (names or numbers 1…count) or one layer name. Bits
        /// of <paramref name="current"/> above the kind's count are kept. Throws <see cref="ArgumentException"/> naming the
        /// known layers when an entry matches none.
        /// </summary>
        public static long Mask(object? value, long current, Kind kind, Func<int, string?> nameOf)
        {
            IEnumerable entries = value switch
            {
                null => Array.Empty<object>(),
                string s => new object[] { s },
                IEnumerable list => list,
                _ => throw new ArgumentException(
                    $"expected a list of {kind.Name} layers, e.g. [\"world\", 3] (layer names or numbers 1-{kind.Count}); a bare number is ambiguous, write [3]"),
            };
            long mask = 0;
            foreach (var entry in entries) mask |= Bit(Layer(entry, kind, nameOf));
            long all = kind.Count >= 64 ? -1L : (1L << kind.Count) - 1;
            return (current & ~all) | mask;
        }

        /// <summary>The layer number an entry names: a layer name (exact, then case-insensitive), or a number 1…count.</summary>
        public static int Layer(object? entry, Kind kind, Func<int, string?> nameOf)
        {
            switch (entry)
            {
                case string text:
                {
                    var trimmed = text.Trim();
                    int? loose = null;
                    for (int layer = 1; layer <= kind.Count; layer++)
                    {
                        var name = nameOf(layer);
                        if (string.IsNullOrEmpty(name)) continue;
                        if (name == trimmed) return layer;
                        if (loose == null && string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase)) loose = layer;
                    }
                    if (loose is { } found) return found;
                    if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return InRange(number, kind);
                    throw new ArgumentException($"no {kind.Name} layer named '{text}' ({Known(kind, nameOf)})");
                }
                case double or float or int or long:
                {
                    var number = Convert.ToDouble(entry, CultureInfo.InvariantCulture);
                    if (Math.Floor(number) == number) return InRange(number, kind);
                    goto default;
                }
                default:
                    throw new ArgumentException($"a {kind.Name} layer is a name or a number 1-{kind.Count}, not {entry ?? "null"}");
            }
        }

        private static int InRange(double layer, Kind kind)
        {
            if (!(layer >= 1 && layer <= kind.Count))
                throw new ArgumentException($"{kind.Name} layers are numbered 1-{kind.Count}, not {layer.ToString(CultureInfo.InvariantCulture)}");
            return (int)layer;
        }

        // The named layers ("1 world, 2 enemies"), for errors.
        private static string Known(Kind kind, Func<int, string?> nameOf)
        {
            var named = Enumerable.Range(1, kind.Count).Where(l => !string.IsNullOrEmpty(nameOf(l)))
                .Select(l => $"{l.ToString(CultureInfo.InvariantCulture)} {nameOf(l)}").ToList();
            return named.Count == 0
                ? $"no {kind.Name} layer is named yet: use a number 1-{kind.Count}, or name one in the layers source"
                : $"named: {string.Join(", ", named)}; or a number 1-{kind.Count}";
        }
    }
}
#endif
