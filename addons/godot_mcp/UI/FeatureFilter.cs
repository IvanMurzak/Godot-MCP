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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>Which enabled-status a feature list window currently shows. The Godot analog of Unity-MCP's
    /// <c>McpFilterType</c> (its Tools/Prompts/Resources list windows' All/Enabled/Disabled dropdown).</summary>
    public enum FeatureStatusFilter
    {
        /// <summary>Show every item regardless of enabled-state.</summary>
        All,

        /// <summary>Show only items whose <see cref="FeatureRowItem.Enabled"/> is <c>true</c>.</summary>
        Enabled,

        /// <summary>Show only items whose <see cref="FeatureRowItem.Enabled"/> is <c>false</c>.</summary>
        Disabled
    }

    /// <summary>
    /// One argument of a tool (input schema) or prompt — a name + optional description, parsed out of the
    /// item's JSON input/output schema. The Godot analog of Unity-MCP's <c>ArgumentData</c>. Pure-managed (no
    /// Godot native types) so the window's per-row argument foldout content is unit-testable.
    /// </summary>
    public sealed record FeatureArgument(string Name, string? Description);

    /// <summary>
    /// The pure-managed row view-model for ONE MCP feature item (a tool, prompt, or resource). Carries every
    /// field the list window renders: the common <see cref="Name"/> / <see cref="Title"/> / <see cref="Description"/>
    /// / <see cref="Enabled"/>, plus the type-specific metadata — tools: <see cref="TokenCount"/> +
    /// <see cref="Inputs"/>; prompts: <see cref="Role"/> + <see cref="Arguments"/>; resources: <see cref="Uri"/>
    /// + <see cref="MimeType"/>. The Godot analog of Unity-MCP's per-window <c>ToolViewModel</c> /
    /// <c>PromptViewModel</c> / <c>ResourceViewModel</c>, collapsed to one record so the filter/window can address
    /// any kind generically.
    ///
    /// <para>
    /// No Godot native types and no <c>#if TOOLS</c>: the live descriptors are read off the McpPlugin managers in
    /// the editor-only <see cref="Connection.FeatureManagerAdapters"/> wiring, which maps each one into this
    /// record — keeping the filter/format logic (<see cref="FeatureFilter"/>) pure and unit-testable.
    /// </para>
    /// </summary>
    public sealed record FeatureRowItem
    {
        /// <summary>The item's stable id (tool/prompt/resource name) — always present, also the toggle key.</summary>
        public required string Name { get; init; }

        /// <summary>Optional human title; falls back to <see cref="Name"/> for display when null/empty.</summary>
        public string? Title { get; init; }

        /// <summary>Optional human description (shown in the per-row Description foldout).</summary>
        public string? Description { get; init; }

        /// <summary>The item's current enabled-state on the live manager (drives the status filter + toggle).</summary>
        public bool Enabled { get; init; }

        /// <summary>Tools only: approximate token cost of this tool's schema (0 for prompts/resources).</summary>
        public int TokenCount { get; init; }

        /// <summary>Tools only: the parsed input-schema arguments (empty for prompts/resources).</summary>
        public IReadOnlyList<FeatureArgument> Inputs { get; init; } = Array.Empty<FeatureArgument>();

        /// <summary>Prompts only: the prompt role (e.g. "User" / "Assistant"); null for tools/resources.</summary>
        public string? Role { get; init; }

        /// <summary>Prompts only: the parsed prompt arguments (empty for tools/resources).</summary>
        public IReadOnlyList<FeatureArgument> Arguments { get; init; } = Array.Empty<FeatureArgument>();

        /// <summary>Resources only: the resource URI / route; null for tools/prompts. ALSO matched by text filter.</summary>
        public string? Uri { get; init; }

        /// <summary>Resources only: the resource MIME type; null for tools/prompts.</summary>
        public string? MimeType { get; init; }

        /// <summary>The label shown as the row title — <see cref="Title"/> when set, else <see cref="Name"/>.</summary>
        public string DisplayTitle => string.IsNullOrEmpty(Title) ? Name : Title!;
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) filter + stats logic for the MCP feature list
    /// windows (tools / prompts / resources). The Godot analog of Unity-MCP's <c>McpListWindowBase.FilterItems</c>
    /// + <c>FilterByText</c> + the "Filtered: X, Total: Y" stats label, collapsed into one kind-parameterized
    /// static surface so the whole filter chain is unit-testable in the plain-xUnit host (the editor Control
    /// wiring in <see cref="FeatureListWindow"/> is verified via the headless Godot smoke instead).
    /// </summary>
    public static class FeatureFilter
    {
        /// <summary>
        /// Apply the STATUS filter first (Enabled → only enabled, Disabled → only disabled, All → unchanged) then
        /// the live TEXT filter (case-insensitive ordinal <c>Contains</c> over Name / Title / Description; for the
        /// <see cref="GodotMcpFeatureKind.Resources"/> kind ALSO over the resource <see cref="FeatureRowItem.Uri"/>).
        /// An empty/whitespace <paramref name="text"/> applies the status filter only. Returns a new list in source
        /// order; never mutates <paramref name="items"/>. Mirrors the Unity reference's filter chain (status then
        /// text), with the resource-URI text match that the Unity <c>ResourceViewModel.FilterByText</c> adds.
        /// </summary>
        public static IReadOnlyList<FeatureRowItem> Apply(
            IEnumerable<FeatureRowItem> items,
            FeatureStatusFilter status,
            string? text,
            GodotMcpFeatureKind kind)
        {
            IEnumerable<FeatureRowItem> filtered = items ?? Enumerable.Empty<FeatureRowItem>();

            filtered = status switch
            {
                FeatureStatusFilter.Enabled => filtered.Where(i => i.Enabled),
                FeatureStatusFilter.Disabled => filtered.Where(i => !i.Enabled),
                _ => filtered
            };

            var trimmed = text?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                bool matchUri = kind == GodotMcpFeatureKind.Resources;
                filtered = filtered.Where(i => MatchesText(i, trimmed!, matchUri));
            }

            return filtered.ToList();
        }

        /// <summary>
        /// Whether <paramref name="item"/> matches <paramref name="text"/> by a case-insensitive ordinal substring
        /// over Name / Title / Description (and, when <paramref name="matchUri"/>, the resource URI). Used by
        /// <see cref="Apply"/>; <paramref name="text"/> is assumed already trimmed and non-empty.
        /// </summary>
        public static bool MatchesText(FeatureRowItem item, string text, bool matchUri)
        {
            return Contains(item.Name, text)
                || Contains(item.Title, text)
                || Contains(item.Description, text)
                || (matchUri && Contains(item.Uri, text));
        }

        static bool Contains(string? value, string text) =>
            value != null && value.Contains(text, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Format the right-aligned "Filtered: X, Total: Y" stat shown beside the filter bar — X = visible after
        /// filtering, Y = total items of this kind. Mirrors Unity-MCP's <c>"Filtered: {0}, Total: {1}"</c>. Uses
        /// the invariant culture so the integers render identically regardless of editor locale.
        /// </summary>
        public static string FormatStats(int filteredCount, int totalCount) =>
            string.Format(CultureInfo.InvariantCulture, "Filtered: {0}, Total: {1}", filteredCount, totalCount);

        /// <summary>
        /// Parse a tool/prompt JSON input (or output) schema into its named arguments — one
        /// <see cref="FeatureArgument"/> per entry under the schema's <c>properties</c> object, carrying the
        /// property name and (when present) its <c>description</c>. A null schema, a non-object schema, or a schema
        /// with no <c>properties</c> object yields an empty list. The Godot analog of Unity-MCP's
        /// <c>ParseSchemaArguments</c>; pure-managed (System.Text.Json.Nodes + ReflectorNet's
        /// <see cref="JsonSchema"/> key constants), so it is unit-testable without a Godot binary.
        /// </summary>
        public static IReadOnlyList<FeatureArgument> ParseSchemaArguments(JsonNode? schema)
        {
            if (schema is not JsonObject schemaObject)
                return Array.Empty<FeatureArgument>();

            if (!schemaObject.TryGetPropertyValue(JsonSchema.Properties, out var propertiesNode))
                return Array.Empty<FeatureArgument>();

            if (propertiesNode is not JsonObject propertiesObject)
                return Array.Empty<FeatureArgument>();

            var arguments = new List<FeatureArgument>();
            foreach (var (name, element) in propertiesObject)
            {
                string? description = null;
                if (element is JsonObject propertyObject &&
                    propertyObject.TryGetPropertyValue(JsonSchema.Description, out var descriptionNode) &&
                    descriptionNode != null)
                {
                    description = descriptionNode.ToString();
                }

                arguments.Add(new FeatureArgument(name, description));
            }

            return arguments;
        }
    }
}
