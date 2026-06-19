/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Globalization;
using com.IvanMurzak.Godot.MCP.Connection;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>, no McpPlugin dependency) presentation logic
    /// for the dock's MCP-features section: the "&lt;Title&gt;: X / Y" count label, the "~N tokens" sub-label,
    /// the per-kind titles, and the placeholder shown before a connection exists. Keeping these here (rather
    /// than inline in the <c>#if TOOLS</c> <see cref="FeaturesPanel"/>) makes every label decision
    /// unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host without constructing a Godot
    /// <see cref="Godot.Control"/>. The Godot analog of Unity-MCP's <c>SubscribeToFeatureStats</c> count
    /// formatting (its "{enabled} / {total}" label + "~{tokens} tokens" token label).
    /// </summary>
    public static class FeaturesPanelView
    {
        /// <summary>Shown for a count value when no connection/managers exist yet (plugin null before connect).</summary>
        public const string Unavailable = "—";

        /// <summary>The human title for each feature kind (used in the row label and the list window heading).</summary>
        public static string Title(GodotMcpFeatureKind kind) => kind switch
        {
            GodotMcpFeatureKind.Tools => "Tools",
            GodotMcpFeatureKind.Prompts => "Prompts",
            _ => "Resources"
        };

        /// <summary>
        /// Format the "&lt;Title&gt;: X / Y" count row where X = enabled, Y = total. Mirrors the Unity
        /// reference's "{enabledCount} / {totalCount}" label, prefixed with the kind title for the dock row.
        /// </summary>
        public static string CountLabel(GodotMcpFeatureKind kind, int enabled, int total) =>
            $"{Title(kind)}: {enabled} / {total}";

        /// <summary>The count row shown when the plugin/managers are not yet available — "&lt;Title&gt;: — / —".</summary>
        public static string UnavailableLabel(GodotMcpFeatureKind kind) =>
            $"{Title(kind)}: {Unavailable} / {Unavailable}";

        /// <summary>
        /// Format the tools-only "~N tokens" sub-label (from <c>EnabledToolsTokenCount</c>). Mirrors the Unity
        /// reference's "~{tokens} tokens" token label. Only the Tools row shows this; prompts/resources have no
        /// token analog. Counts of 1000+ are abbreviated with a <c>k</c> suffix (one decimal, trailing
        /// <c>.0</c> trimmed): <c>999</c> → "~999 tokens", <c>1000</c> → "~1k tokens", <c>1500</c> →
        /// "~1.5k tokens", <c>12345</c> → "~12.3k tokens".
        /// </summary>
        public static string TokenLabel(int enabledTokenCount) => $"~{FormatTokenCount(enabledTokenCount)} tokens";

        /// <summary>
        /// Format the tools-only "~N tokens total" sub-label shown under the count in the restyled MCP-features
        /// row (Unity's "~N tokens total" 11px gray sub-label). Uses the same k-abbreviated
        /// <see cref="FormatTokenCount"/> as <see cref="TokenLabel"/>, with a "total" suffix: <c>1500</c> →
        /// "~1.5k tokens total".
        /// </summary>
        public static string TokenTotalLabel(int enabledTokenCount) => $"~{FormatTokenCount(enabledTokenCount)} tokens total";

        /// <summary>The "~N tokens total" sub-label shown when the plugin/managers are not yet available — "~— tokens total".</summary>
        public static string UnavailableTokenTotalLabel() => $"~{Unavailable} tokens total";

        /// <summary>
        /// Abbreviate a token count: values under 1000 render as-is; 1000+ render as <c>{value/1000}k</c> with
        /// one decimal place and a trimmed trailing <c>.0</c> (e.g. 1000 → "1k", 1500 → "1.5k", 2000 → "2k").
        /// Uses the invariant culture so the decimal separator is always a dot regardless of editor locale.
        /// </summary>
        public static string FormatTokenCount(int enabledTokenCount)
        {
            if (enabledTokenCount < 1000)
                return enabledTokenCount.ToString(CultureInfo.InvariantCulture);

            var thousands = enabledTokenCount / 1000.0;
            return thousands.ToString("0.#", CultureInfo.InvariantCulture) + "k";
        }

        /// <summary>The token sub-label shown when the plugin/managers are not yet available — "~— tokens".</summary>
        public static string UnavailableTokenLabel() => $"~{Unavailable} tokens";
    }
}
