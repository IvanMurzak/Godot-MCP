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
using System.Linq;
using System.Xml.Linq;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// The install state of a single <see cref="GodotExtensionDescriptor"/> relative to a consumer's
    /// <c>.csproj</c>. The Godot analog of the per-extension state Unity derives from <c>Client.List</c>: drives the
    /// dock row's button (NotInstalled → "Install", UpdateAvailable → "Update", Installed → disabled "Installed").
    /// </summary>
    public enum ExtensionInstallState
    {
        /// <summary>No <c>&lt;PackageReference&gt;</c> for this extension's package id exists in the consumer <c>.csproj</c>.</summary>
        NotInstalled,

        /// <summary>A reference exists and is up to date (no descriptor version pin, or the referenced version is &gt;= the descriptor's).</summary>
        Installed,

        /// <summary>A reference exists but the descriptor pins a NEWER version than the consumer currently references.</summary>
        UpdateAvailable
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) parser of a consumer <c>.csproj</c>'s
    /// <c>&lt;PackageReference&gt;</c> items into an installed <c>{packageId → version}</c> map (the analog of
    /// consuming Unity's <c>Client.List</c> result), plus the per-descriptor <see cref="ExtensionInstallState"/>
    /// decision. All XML is parsed with <see cref="System.Xml.Linq"/> so the result is deterministic and identical
    /// on every host OS (Windows dev + Linux CI); no platform-dependent path / rooting logic is involved.
    ///
    /// <para>
    /// Robust to a missing / empty / malformed <c>.csproj</c> text: those parse to an EMPTY installed map (every
    /// descriptor then reads as <see cref="ExtensionInstallState.NotInstalled"/>), never throwing — the same
    /// "no usable existing config → start fresh" discipline as <c>AgentConfigJson</c>.
    /// </para>
    /// </summary>
    public static class InstalledStateDetector
    {
        /// <summary>
        /// Parse every <c>&lt;PackageReference&gt;</c> in <paramref name="csprojText"/> into a
        /// <c>{packageId → version}</c> map. The key is the <c>Include</c> (NuGet package ids are case-insensitive,
        /// so the map uses <see cref="StringComparer.OrdinalIgnoreCase"/>); the value is the <c>Version</c> attribute
        /// (or, when absent, a child <c>&lt;Version&gt;</c> element — both MSBuild forms), or the empty string when no
        /// version is declared (a floating / centrally-managed reference). A reference with no <c>Include</c> is
        /// skipped. On duplicate ids the LAST one wins (matches MSBuild's last-write evaluation).
        ///
        /// <para>Returns an EMPTY map for null / whitespace / unparseable XML — never throws.</para>
        /// </summary>
        public static IReadOnlyDictionary<string, string> ParsePackageReferences(string? csprojText)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(csprojText))
                return map;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(csprojText);
            }
            catch (System.Xml.XmlException)
            {
                return map;
            }

            if (doc.Root == null)
                return map;

            // Match by LOCAL name so an (unusual) MSBuild XML namespace doesn't hide the items. The csproj uses the
            // SDK-style default (no namespace), but matching on LocalName is the robust, namespace-agnostic choice.
            foreach (var item in doc.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
            {
                var include = (string?)item.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include))
                    continue;

                var version = (string?)item.Attribute("Version");
                if (string.IsNullOrEmpty(version))
                {
                    // MSBuild also allows <PackageReference Include="X"><Version>1.0.0</Version></PackageReference>.
                    var versionElement = item.Elements().FirstOrDefault(e => e.Name.LocalName == "Version");
                    version = versionElement?.Value;
                }

                map[include!.Trim()] = (version ?? string.Empty).Trim();
            }

            return map;
        }

        /// <summary>
        /// Decide the <see cref="ExtensionInstallState"/> of <paramref name="descriptor"/> against the
        /// <paramref name="installed"/> map produced by <see cref="ParsePackageReferences"/>:
        /// <list type="bullet">
        ///   <item><see cref="ExtensionInstallState.NotInstalled"/> — the descriptor's package id is absent.</item>
        ///   <item>
        ///     <see cref="ExtensionInstallState.UpdateAvailable"/> — the id is present, the descriptor pins a concrete
        ///     version, and that version is strictly GREATER than the installed one (an installed reference with no
        ///     version also counts as update-available when the descriptor pins a version, since the descriptor's pin
        ///     is the known-good target).
        ///   </item>
        ///   <item><see cref="ExtensionInstallState.Installed"/> — otherwise (present and up to date, or no descriptor version pin).</item>
        /// </list>
        /// </summary>
        public static ExtensionInstallState StateFor(
            GodotExtensionDescriptor descriptor,
            IReadOnlyDictionary<string, string> installed)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (installed == null)
                throw new ArgumentNullException(nameof(installed));

            if (!installed.TryGetValue(descriptor.PackageId, out var installedVersion))
                return ExtensionInstallState.NotInstalled;

            // No target version on the descriptor → any present reference is "installed" (nothing to compare to).
            if (!descriptor.HasVersion)
                return ExtensionInstallState.Installed;

            // Installed reference has no version pin but the descriptor does → offer the descriptor's pin as an update.
            if (string.IsNullOrEmpty(installedVersion))
                return ExtensionInstallState.UpdateAvailable;

            var cmp = CompareVersions(descriptor.Version!, installedVersion);
            return cmp > 0 ? ExtensionInstallState.UpdateAvailable : ExtensionInstallState.Installed;
        }

        /// <summary>Convenience: <see cref="StateFor(GodotExtensionDescriptor, IReadOnlyDictionary{string, string})"/> straight off raw <c>.csproj</c> text.</summary>
        public static ExtensionInstallState StateFor(GodotExtensionDescriptor descriptor, string? csprojText)
            => StateFor(descriptor, ParsePackageReferences(csprojText));

        /// <summary>
        /// Compare two dotted numeric version strings (e.g. <c>1.2.0</c> vs <c>1.10.0</c>) component-by-component as
        /// integers (so <c>1.10.0</c> &gt; <c>1.2.0</c>, unlike an ordinal string compare). A trailing pre-release /
        /// build suffix on a component (<c>1.0.0-rc1</c>) is tolerated — only the leading integer of each dotted
        /// component is read; a non-numeric component reads as 0. Missing trailing components are treated as 0
        /// (<c>1.0</c> == <c>1.0.0</c>). Returns &gt;0 when <paramref name="a"/> is newer, &lt;0 when older, 0 when
        /// equal. Pure string/integer arithmetic → identical on every host OS.
        /// </summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = SplitComponents(a);
            var pb = SplitComponents(b);
            var n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                var ca = i < pa.Length ? pa[i] : 0;
                var cb = i < pb.Length ? pb[i] : 0;
                if (ca != cb)
                    return ca.CompareTo(cb);
            }
            return 0;
        }

        static int[] SplitComponents(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return Array.Empty<int>();

            var parts = version!.Trim().Split('.');
            var result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = LeadingInt(parts[i]);
            return result;
        }

        static int LeadingInt(string component)
        {
            int end = 0;
            while (end < component.Length && char.IsDigit(component[end]))
                end++;

            if (end == 0)
                return 0;

            return int.TryParse(component.Substring(0, end), out var value) ? value : 0;
        }
    }
}
