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
    /// <b>Ships EMPTY for now.</b> No Godot-MCP extension package exists on nuget.org yet — creating the first real
    /// extension package + a <c>Godot-AI-Tools-Template</c> repo + the NuGet publish is a SEPARATE follow-up task. The
    /// dock renders an honest "coming soon" placeholder while <see cref="All"/> is empty (see the
    /// <c>ExtensionsPanel</c>). To add a real extension once it is published: append ONE
    /// <see cref="GodotExtensionDescriptor"/> to <see cref="_descriptors"/> below — no other file needs to change.
    /// </para>
    /// </summary>
    public static class GodotExtensionRegistry
    {
        // The extension descriptor list. EMPTY until the first extension package is published (follow-up task). To
        // add one, append a `new GodotExtensionDescriptor(...)` line here.
        static readonly IReadOnlyList<GodotExtensionDescriptor> _descriptors = new GodotExtensionDescriptor[]
        {
            // (none yet — see the type doc-comment)
        };

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
