/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) holder for the static copy + URLs the dock's
    /// <c>ExtensionsPanel</c> shows — the header, the "coming soon" placeholder line (rendered while the
    /// <see cref="GodotExtensionRegistry"/> is empty), the docs / template-repo link, and the CLASS-B
    /// "requires the &lt;X&gt; addon" note + link derivation (<see cref="AddonRequiredNotice"/> /
    /// <see cref="AddonRequiredUrl"/>). Kept out of the editor guard so the strings + URL logic are CI-unit-tested
    /// (mirroring <c>SupportFooterLinks</c>); the editor Control wiring is <c>#if TOOLS</c> and verified via the
    /// headless Godot smoke (test.md Suite 3).
    /// </summary>
    public static class ExtensionsPanelText
    {
        /// <summary>The section header shown above the extension rows / placeholder.</summary>
        public const string Header = "Extensions";

        /// <summary>
        /// The honest placeholder shown while no extension package exists yet (the registry ships empty). Matches
        /// Unity-MCP's extensions look but does not pretend an installable extension exists.
        /// </summary>
        public const string ComingSoonText =
            "Extensions add more AI tool families to Godot — coming soon.";

        /// <summary>The text of the docs / template-repo link shown under the placeholder.</summary>
        public const string DocsLinkText = "Learn more";

        /// <summary>
        /// The docs / template-repo URL the placeholder link opens. Points at the Godot-MCP repository for now (the
        /// dedicated <c>Godot-AI-Tools-Template</c> repo + extensions docs are a SEPARATE follow-up task — swap this
        /// to that repo / docs page once it exists).
        /// </summary>
        public const string DocsUrl = "https://github.com/IvanMurzak/Godot-MCP";

        /// <summary>
        /// The notice shown (in place of an enabled Install button) when no consumer <c>.csproj</c> could be located —
        /// e.g. a pure-GDScript project, or the published-addon-only context where there is nothing to install into.
        /// </summary>
        public const string NoProjectFileNotice =
            "No project .csproj found — open this addon inside a Godot C# project to install extensions.";

        /// <summary>The link text shown next to a CLASS-B extension's "requires the &lt;X&gt; addon" note.</summary>
        public const string AddonRequiredLinkText = "View addon";

        /// <summary>
        /// The "requires the &lt;Name&gt; addon" note shown under a CLASS-B (addon-dependent) extension's description
        /// (derived from the catalog <c>addonRequired</c> metadata). The wrapped third-party addon is the consumer's
        /// own responsibility — the installer never vendors or downloads it — so the note tells the user to add it
        /// separately. CLASS-A extensions (which wrap a built-in Godot feature) have no <c>AddonRequired</c> and show
        /// no such note.
        /// </summary>
        public static string AddonRequiredNotice(GodotAddonRequirement addon)
            => $"Requires the {addon.Name} addon — install it in your project separately.";

        /// <summary>
        /// The best "get the required addon" link for a CLASS-B extension: the Godot AssetLib asset page when an
        /// <see cref="GodotAddonRequirement.AssetLibId"/> is known (the in-editor install location), else the upstream
        /// <see cref="GodotAddonRequirement.Repo"/> — a bare <c>owner/name</c> is expanded to a GitHub URL, an already
        /// absolute <c>http(s)</c> repo is used verbatim. Returns <c>null</c> when neither is available (the row then
        /// shows the note without a link).
        /// </summary>
        public static string? AddonRequiredUrl(GodotAddonRequirement addon)
        {
            if (!string.IsNullOrWhiteSpace(addon.AssetLibId))
                return $"https://godotengine.org/asset-library/asset/{addon.AssetLibId!.Trim()}";

            var repo = addon.Repo?.Trim();
            if (string.IsNullOrEmpty(repo))
                return null;

            if (repo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || repo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return repo;

            return $"https://github.com/{repo}";
        }
    }
}
