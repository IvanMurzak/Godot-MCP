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
using System.Globalization;
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Enum values written by name where Godot stores an int: an enum-hinted property (<c>grow_horizontal</c>, hint string
    /// <c>"Begin,End,Both"</c>) or a method argument of a class enum (<c>Control.LayoutPreset</c>). Accepts the name as the
    /// inspector shows it (<c>"Both"</c>), the C++ / C# constant (<c>"GROW_DIRECTION_BOTH"</c>, <c>"PresetFullRect"</c>) or
    /// the short form (<c>"FullRect"</c>). Pure managed: no Godot types (unit-tested).
    /// </summary>
    internal static class GodotEnumNames
    {
        /// <summary>The options of an enum hint string: "A,B,C" (0, 1, 2) or "A:3,B,C:10" (a value continues from the last).</summary>
        public static List<(string name, long value)> ParseHint(string hintString)
        {
            var options = new List<(string, long)>();
            long next = 0;
            foreach (var part in (hintString ?? "").Split(','))
            {
                var item = part.Trim();
                if (item.Length == 0) continue;
                var colon = item.LastIndexOf(':');
                if (colon > 0 && long.TryParse(item.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var explicitValue))
                {
                    options.Add((item.Substring(0, colon).Trim(), explicitValue));
                    next = explicitValue + 1;
                }
                else options.Add((item, next++));
            }
            return options;
        }

        /// <summary>
        /// The value of the option <paramref name="text"/> names: the same name (case-insensitive), the same letters without
        /// separators, or the one option whose name the text ends with or that ends with the text ("GROW_DIRECTION_BOTH" →
        /// "Both", "FullRect" → "PRESET_FULL_RECT"). False when nothing or more than one option matches.
        /// </summary>
        public static bool TryMatch(IReadOnlyList<(string name, long value)> options, string text, out long value)
        {
            value = 0;
            var key = Normalize(text);
            if (key.Length == 0) return false;
            foreach (var (name, v) in options)
                if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase) || Normalize(name) == key) { value = v; return true; }
            var hits = options.Where(o => Normalize(o.name) is { Length: > 0 } n && (key.EndsWith(n, StringComparison.Ordinal) || n.EndsWith(key, StringComparison.Ordinal)))
                              .Select(o => o.value).Distinct().ToList();
            if (hits.Count != 1) return false;
            value = hits[0];
            return true;
        }

        /// <summary>"one of: Begin, End, Both" for an error message.</summary>
        public static string Describe(IReadOnlyList<(string name, long value)> options) =>
            "one of: " + string.Join(", ", options.Select(o => o.name)) + " (or its number)";

        private static string Normalize(string s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
#endif
