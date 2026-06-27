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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) parser of the SHARED extension catalog JSON
    /// (<c>addons/godot_mcp/extensions.catalog.json</c> — see <c>extensions.catalog.md</c>). It is the dock's half of
    /// the "single source of truth consumed by all three install channels" contract: the same JSON is mirrored by the
    /// CLI (<c>cli/src/utils/extensions-catalog.ts</c>, enforced byte-equal by a parity test).
    ///
    /// <para>
    /// The catalog ships EMBEDDED into the addon assembly as <c>Godot-MCP.extensions.catalog.json</c>
    /// (<c>&lt;EmbeddedResource&gt;</c> in <c>Godot-MCP.csproj</c>), so the <see cref="GodotExtensionRegistry"/> reads
    /// the real catalog at runtime with no <c>res://</c> / filesystem dependency — which keeps the registry static,
    /// pure-managed, and CI-unit-testable. <see cref="Parse"/> is the tested seam; <see cref="LoadEmbedded"/> is the
    /// thin shell that streams the embedded resource into it.
    /// </para>
    ///
    /// <para>
    /// Tolerant by design: a missing resource, empty/whitespace text, malformed JSON, or entries missing the required
    /// <c>name</c> / <c>packageId</c> all collapse to an EMPTY descriptor list rather than throwing — the same
    /// "no usable catalog → ship empty" discipline the rest of the Extensions infrastructure follows.
    /// </para>
    /// </summary>
    public static class GodotExtensionCatalog
    {
        /// <summary>The logical name the catalog JSON is embedded under (see the <c>&lt;EmbeddedResource&gt;</c> LogicalName).</summary>
        public const string EmbeddedResourceName = "Godot-MCP.extensions.catalog.json";

        static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Parse the shared-catalog JSON text into the ordered <see cref="GodotExtensionDescriptor"/> list. Entries are
        /// kept in document order; entries missing a non-empty <c>name</c> or <c>packageId</c> are dropped. Returns an
        /// EMPTY list for null / whitespace / unparseable JSON — never throws.
        /// </summary>
        public static IReadOnlyList<GodotExtensionDescriptor> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<GodotExtensionDescriptor>();

            CatalogDocument? doc;
            try
            {
                doc = JsonSerializer.Deserialize<CatalogDocument>(json, _options);
            }
            catch (JsonException)
            {
                return Array.Empty<GodotExtensionDescriptor>();
            }

            var entries = doc?.Extensions;
            if (entries == null || entries.Count == 0)
                return Array.Empty<GodotExtensionDescriptor>();

            var result = new List<GodotExtensionDescriptor>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;
                if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.PackageId))
                    continue;

                var tools = entry.Tools == null
                    ? Array.Empty<(string, string)>()
                    : entry.Tools
                        .Where(t => t != null && !string.IsNullOrWhiteSpace(t!.Name))
                        .Select(t => (t!.Name!.Trim(), (t.Description ?? string.Empty).Trim()))
                        .ToArray();

                result.Add(new GodotExtensionDescriptor(
                    Name: entry.Name!.Trim(),
                    Description: (entry.Description ?? string.Empty).Trim(),
                    PackageId: entry.PackageId!.Trim(),
                    Version: string.IsNullOrWhiteSpace(entry.Version) ? null : entry.Version!.Trim(),
                    GitUrl: string.IsNullOrWhiteSpace(entry.GitUrl) ? null : entry.GitUrl!.Trim(),
                    Tools: tools));
            }

            return result;
        }

        /// <summary>
        /// Load + parse the catalog JSON embedded in <paramref name="assembly"/> (defaults to the assembly hosting
        /// this type). Returns an EMPTY list when the resource is absent or unreadable — never throws.
        /// </summary>
        public static IReadOnlyList<GodotExtensionDescriptor> LoadEmbedded(Assembly? assembly = null)
        {
            assembly ??= typeof(GodotExtensionCatalog).Assembly;
            try
            {
                using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
                if (stream == null)
                    return Array.Empty<GodotExtensionDescriptor>();

                using var reader = new StreamReader(stream);
                return Parse(reader.ReadToEnd());
            }
            catch (Exception)
            {
                return Array.Empty<GodotExtensionDescriptor>();
            }
        }

        // --- JSON DTOs (deserialization shape; mapped to GodotExtensionDescriptor above) -----------------------

        sealed class CatalogDocument
        {
            [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
            [JsonPropertyName("extensions")] public List<CatalogEntry?>? Extensions { get; set; }
        }

        sealed class CatalogEntry
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("description")] public string? Description { get; set; }
            [JsonPropertyName("packageId")] public string? PackageId { get; set; }
            [JsonPropertyName("version")] public string? Version { get; set; }
            [JsonPropertyName("gitUrl")] public string? GitUrl { get; set; }
            [JsonPropertyName("tools")] public List<CatalogTool?>? Tools { get; set; }
        }

        sealed class CatalogTool
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("description")] public string? Description { get; set; }
        }
    }
}
