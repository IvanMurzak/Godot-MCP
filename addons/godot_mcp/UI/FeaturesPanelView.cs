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
        /// token analog.
        /// </summary>
        public static string TokenLabel(int enabledTokenCount) => $"~{enabledTokenCount} tokens";

        /// <summary>The token sub-label shown when the plugin/managers are not yet available — "~— tokens".</summary>
        public static string UnavailableTokenLabel() => $"~{Unavailable} tokens";
    }
}
