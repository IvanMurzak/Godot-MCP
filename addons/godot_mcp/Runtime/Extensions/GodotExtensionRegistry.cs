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
using System.Collections.Generic;
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// Static registry of every <see cref="GodotExtensionDescriptor"/> the dock's "Extensions" section offers to
    /// install. The Godot analog of Unity-MCP's hardcoded extension list in <c>MainWindowEditor.Extensions.cs</c>.
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the list + lookups are CI-unit-tested, and so the
    /// dock panel binds to <see cref="All"/> generically.
    ///
    /// <para>
    /// The list is sourced from the SHARED catalog <c>addons/godot_mcp/extensions.catalog.json</c> (the single source
    /// of truth consumed by the dock, the CLI, and — later — the app; see <c>extensions.catalog.md</c>). That JSON is
    /// embedded into the addon assembly and parsed once by <see cref="GodotExtensionCatalog.LoadEmbedded"/>. To add a
    /// real extension once it is published: append ONE entry to <c>extensions.catalog.json</c> — no code changes here.
    /// </para>
    ///
    /// <para>
    /// <b>Ships EMPTY for now.</b> No Godot-MCP extension package exists on nuget.org yet, so the catalog's
    /// <c>extensions</c> array is empty and the dock renders an honest "coming soon" placeholder while
    /// <see cref="All"/> is empty (see the <c>ExtensionsPanel</c>).
    /// </para>
    /// </summary>
    public static class GodotExtensionRegistry
    {
        // The extension descriptor list, loaded once from the shared embedded catalog JSON (single source of truth).
        // EMPTY until the first extension package is published — add one by appending to extensions.catalog.json.
        static readonly IReadOnlyList<GodotExtensionDescriptor> _descriptors = GodotExtensionCatalog.LoadEmbedded();

        /// <summary>Every registered extension descriptor, in display order. Empty until the first package ships.</summary>
        public static IReadOnlyList<GodotExtensionDescriptor> All => _descriptors;

        /// <summary>True when no extensions are registered yet — drives the dock's "coming soon" placeholder.</summary>
        public static bool IsEmpty => _descriptors.Count == 0;

        /// <summary>
        /// The descriptor whose <see cref="GodotExtensionDescriptor.PackageId"/> equals <paramref name="packageId"/>
        /// (ordinal-ignore-case, matching NuGet's case-insensitive package ids), or null when absent / id is empty.
        /// </summary>
        public static GodotExtensionDescriptor? GetByPackageId(string? packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return null;

            return _descriptors.FirstOrDefault(
                d => string.Equals(d.PackageId, packageId, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
