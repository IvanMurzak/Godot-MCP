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
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// What a rename (<c>set name = …</c>) may assign. Godot itself would silently change a name with a character it does not
    /// allow ("a.b" → "a_b") or one a sibling already has ("N2" → "N3"), and the receipt would show a name the node does not
    /// have; the rename is refused instead, like the Scene dock refuses an invalid name. Pure (unit-tested).
    /// </summary>
    public static class NodeNameRules
    {
        /// <summary>The characters Godot does not allow in a node name (<c>String.get_invalid_node_name_characters</c>).</summary>
        public const string InvalidCharacters = ".:@/\"%";

        /// <summary>
        /// How to give siblings names some of them hold now (the auto names of Duplicate copies, a swap): the names are checked
        /// against the siblings as they are before the set.
        /// </summary>
        public const string ShiftHint = "to swap or renumber siblings, rename them to temporary names first, then to the final names in a second call";

        /// <summary>Why <paramref name="name"/> cannot be a node's name (<paramref name="siblingHas"/>: a sibling has it); null when it can.</summary>
        public static string? Problem(string? name, Func<string, bool> siblingHas)
        {
            if (string.IsNullOrWhiteSpace(name)) return "a node name cannot be empty";
            if (name!.IndexOfAny(InvalidCharacters.ToCharArray()) >= 0)
            {
                var bad = name.Where(c => InvalidCharacters.IndexOf(c) >= 0).Distinct().Select(c => "'" + c + "'");
                return $"a node name cannot contain {string.Join(" ", bad)} (not allowed: {string.Join(" ", InvalidCharacters.ToCharArray())})";
            }
            if (siblingHas(name))
                return $"a sibling is already named '{name}' (node names are unique among siblings; " + ShiftHint + ")";
            return null;
        }
    }
}
#endif
