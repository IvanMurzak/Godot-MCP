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

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) holder for the static URLs and copy used
    /// by the dock's <c>SupportFooter</c>. Kept out of the editor guard so the constants are CI-unit-tested
    /// (the editor Control wiring in <c>SupportFooter.cs</c> is <c>#if TOOLS</c> and verified via the
    /// headless Godot smoke — see <c>test.md</c> Suite 3). The footer is a static link strip with no live
    /// state, so this is the only "logic" worth asserting.
    /// </summary>
    public static class SupportFooterLinks
    {
        /// <summary>
        /// The Discord invite opened by the footer's "Help / Talk" button.
        /// NOTE: this is the SHARED ai-game.dev / Unity-MCP community Discord invite (the same one used by
        /// the Unity-MCP docs badges) — Godot-MCP has no separate server. The repo's README/CLAUDE.md did
        /// not declare a Godot-specific invite at the time this was written; if a dedicated Godot-MCP invite
        /// is later created, swap it here.
        /// </summary>
        public const string DiscordUrl = "https://discord.gg/cfbdMZX99G";

        /// <summary>GitHub issues page — the footer's "Bug Report" button.</summary>
        public const string IssuesUrl = "https://github.com/IvanMurzak/Godot-MCP/issues";

        /// <summary>Repository home — the footer's "Star" button.</summary>
        public const string RepositoryUrl = "https://github.com/IvanMurzak/Godot-MCP";

        /// <summary>Heading shown above the support buttons.</summary>
        public const string PromptText = "Found an issue?";

        /// <summary>Closing thank-you line (plain text — kept for callers/tests that want the un-marked-up copy).</summary>
        public const string ThanksText =
            "Thanks for using AI Game Developer. If you like it, please give the project a star on GitHub.\n\nSincerely,\nIvan Murzak";

        /// <summary>
        /// The closing thank-you block as Godot BBCode (for a <c>RichTextLabel</c>), mirroring Unity-MCP's
        /// footer copy where the product name "AI Game Developer" is emphasised with a red "AI". Godot BBCode
        /// uses <c>[color=red]…[/color]</c> + <c>[b]…[/b]</c> (the Godot analog of Unity rich-text
        /// <c>&lt;color=red&gt;</c>/<c>&lt;b&gt;</c>).
        /// </summary>
        public const string ThanksBbcode =
            "Thanks for using [b][color=red]AI[/color] Game Developer[/b]. If you like it, please give the project a star on GitHub.\n\nSincerely,\nIvan Murzak";

        /// <summary>
        /// The thank-you PARAGRAPH only (no "Sincerely" sign-off) as BBCode — the footer renders this above a row
        /// that pairs the sign-off with the GitHub Star button (Unity puts the star on the "Sincerely, Ivan Murzak"
        /// line). Kept separate from <see cref="ThanksBbcode"/> so existing callers/tests are unaffected.
        /// </summary>
        public const string ThanksParagraphBbcode =
            "Thanks for using [b][color=red]AI[/color] Game Developer[/b]. If you like it, please give the project a star on GitHub.";

        /// <summary>The sign-off shown on the same line as the GitHub Star button.</summary>
        public const string SincerelyText = "Sincerely,\nIvan Murzak";
    }
}
