/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) holder for the static copy + URLs the dock's
    /// <c>ExtensionsPanel</c> shows — the header, the "coming soon" placeholder line (rendered while the
    /// <see cref="GodotExtensionRegistry"/> is empty), and the docs / template-repo link. Kept out of the editor
    /// guard so the strings are CI-unit-tested (mirroring <c>SupportFooterLinks</c>); the editor Control wiring is
    /// <c>#if TOOLS</c> and verified via the headless Godot smoke (test.md Suite 3).
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
    }
}
