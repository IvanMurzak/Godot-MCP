/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The support/footer section of the Godot-MCP editor dock — the Godot <see cref="Control"/> analog of
    /// Unity-MCP's window footer. A <see cref="VBoxContainer"/> the <see cref="GodotMcpDock"/> appends to its
    /// Body BELOW the connection panel. It renders, top to bottom:
    /// <list type="bullet">
    ///   <item>A "Found an issue?" prompt label.</item>
    ///   <item>An HBox of buttons that open external URLs via <see cref="OS.ShellOpen"/>:
    ///   Help/Talk → Discord, Bug Report → GitHub issues, Star → the repository.</item>
    ///   <item>A short "Thanks for using AI Game Developer" line.</item>
    /// </list>
    ///
    /// <para>
    /// STATIC links only — no live state, no connection coupling, no subscriptions or timers. The footer is
    /// a plain child <see cref="Control"/> freed with the dock, so it needs no special <c>_ExitTree</c>
    /// teardown. All URLs/copy come from the pure-managed <see cref="SupportFooterLinks"/> so they are
    /// CI-unit-tested; this Control wiring is editor-only (<c>#if TOOLS</c>) and verified via the headless
    /// Godot smoke (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    [Tool]
    public partial class SupportFooter : VBoxContainer
    {
        public SupportFooter()
        {
            Name = "SupportFooter";
            BuildUi();
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            // --- "Found an issue?" prompt (default text colour, like Unity's .section-text). ---
            var prompt = new Label
            {
                Name = "Prompt",
                Text = SupportFooterLinks.PromptText
            };
            AddChild(prompt);

            // --- Support buttons: secondary icon buttons (Discord "Help / Talk", GitHub "Bug Report").
            // Styled like Unity's .btn-secondary.btn-with-icon (gray bordered, leading icon). Open externally;
            // no live state. (Unity's "Check" serialization button has no Godot equivalent — intentionally omitted.)
            var buttonRow = new HBoxContainer { Name = "SupportButtons" };
            buttonRow.AddThemeConstantOverride("separation", 6);
            buttonRow.AddChild(DockStyle.IconButton(
                "DiscordHelp", "Help / Talk", DockTheme.DiscordIconFileName,
                () => OS.ShellOpen(SupportFooterLinks.DiscordUrl)));
            buttonRow.AddChild(DockStyle.IconButton(
                "GitHubIssue", "Bug Report", DockTheme.GitHubIconFileName,
                () => OS.ShellOpen(SupportFooterLinks.IssuesUrl)));
            AddChild(buttonRow);

            // --- Divider between the support buttons and the thanks block (Unity's .divider). ---
            AddChild(DockStyle.Divider("SupportDivider"));

            // --- Thanks line (RichTextLabel so the product name can be emphasised, like Unity's red "AI"). ---
            var thanks = new RichTextLabel
            {
                Name = "Thanks",
                BbcodeEnabled = true,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = SupportFooterLinks.ThanksBbcode
            };
            AddChild(thanks);

            // --- Gold "GitHub Star" button (Unity's .btn-golden.btn-with-icon), right-aligned. ---
            var starRow = new HBoxContainer { Name = "StarRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            starRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            starRow.AddChild(DockStyle.GoldenButton(
                "GitHubStar", "GitHub Star", DockTheme.StarIconFileName,
                () => OS.ShellOpen(SupportFooterLinks.RepositoryUrl)));
            AddChild(starRow);
        }
    }
}
#endif
