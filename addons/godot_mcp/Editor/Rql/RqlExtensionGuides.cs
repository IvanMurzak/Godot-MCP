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
using System.Linq;
using System.Text;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The installed extensions' RQL guides (each module's AddGuide topic and markdown) for an MCP client that has only the
    /// tools: rql-schema lists the topics with their first sentence, and rql-schema "&lt;topic&gt;" prints a topic's guide.
    /// Without this, a module that adds only adapters (Particles, Tilemap, CSG …) is invisible to an agent that cannot read
    /// the generated skill file. Pure: unit-tested.
    /// </summary>
    internal static class RqlExtensionGuides
    {
        private const int SummaryLength = 180;

        /// <summary>The overview block, or "" when no extension added a guide.</summary>
        public static string Overview(IReadOnlyList<KeyValuePair<string, string>> guides)
        {
            if (guides.Count == 0) return "";
            var sb = new StringBuilder("installed extensions (rql-schema \"<name>\" prints its guide with examples):\n");
            foreach (var g in guides)
                sb.Append("  ").Append(g.Key).Append(" — ").Append(Summary(g.Value)).Append('\n');
            return sb.ToString();
        }

        /// <summary>The guide whose topic is <paramref name="name"/> (case-insensitive; spaces ignored), or null.</summary>
        public static string? Find(IReadOnlyList<KeyValuePair<string, string>> guides, string name)
        {
            var key = Normalize(name);
            foreach (var g in guides)
                if (Normalize(g.Key) == key) return "guide: " + g.Key + " (extension)\n" + g.Value.Trim() + "\n";
            return null;
        }

        /// <summary>The first sentence of a guide's markdown, without code spans' backticks, shortened.</summary>
        public static string Summary(string markdown)
        {
            var text = markdown.TrimStart();
            var end = text.IndexOf(". ", StringComparison.Ordinal);
            while (end > 0 && (EndsWith(text, end, "e.g") || EndsWith(text, end, "i.e"))) end = text.IndexOf(". ", end + 2, StringComparison.Ordinal);
            var newline = text.IndexOf('\n');
            if (newline >= 0 && (end < 0 || newline < end)) end = newline - 1;
            var sentence = (end >= 0 ? text.Substring(0, end + 1) : text).Replace("`", "").Trim();
            return sentence.Length <= SummaryLength ? sentence : sentence.Substring(0, SummaryLength - 1).TrimEnd() + "…";
        }

        private static bool EndsWith(string text, int end, string abbreviation) =>
            end >= abbreviation.Length && string.CompareOrdinal(text, end - abbreviation.Length, abbreviation, 0, abbreviation.Length) == 0;

        private static string Normalize(string name) => new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
    }
}
#endif
