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
        // Footer text size — the "Found an issue?" prompt, the thanks paragraph, and the sign-off. Normal body
        // size (was an undersized 16) so the footer reads at the same scale as the rest of the dock.
        const int FooterFontSize = 18;

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
            prompt.AddThemeFontSizeOverride("font_size", FooterFontSize);
            AddChild(prompt);

            // --- Support buttons: secondary icon buttons (Discord "Help / Talk", GitHub "Bug Report", and the
            // "Check" serialization tool). Styled like Unity's .btn-secondary.btn-with-icon (gray bordered,
            // leading icon). Discord/GitHub open external URLs; "Check" opens the in-editor Serialization Check
            // window (the Godot port of Unity's "Check" button — ReflectorNet is in-process here, so this DOES
            // have a Godot equivalent: it was previously omitted only because the dock had no window for it yet).
            var buttonRow = new HBoxContainer { Name = "SupportButtons" };
            buttonRow.AddThemeConstantOverride("separation", 6);
            buttonRow.AddChild(DockStyle.IconButton(
                "DiscordHelp", "Help / Talk", DockTheme.DiscordIconFileName,
                () => OS.ShellOpen(SupportFooterLinks.DiscordUrl)));
            buttonRow.AddChild(DockStyle.IconButton(
                "GitHubIssue", "Bug Report", DockTheme.GitHubIconFileName,
                () => OS.ShellOpen(SupportFooterLinks.IssuesUrl)));
            buttonRow.AddChild(DockStyle.IconButton(
                "Check", "Check", iconFileName: null, OnCheckPressed));
            AddChild(buttonRow);

            // --- Divider between the support buttons and the thanks block (Unity's .divider). ---
            AddChild(DockStyle.Divider("SupportDivider"));

            // --- Thanks PARAGRAPH (RichTextLabel so the product name can be emphasised, like Unity's red "AI").
            //     Smaller font so the footer is compact like Unity (Godot's default label font is larger). ---
            var thanks = new RichTextLabel
            {
                Name = "Thanks",
                BbcodeEnabled = true,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = SupportFooterLinks.ThanksParagraphBbcode
            };
            thanks.AddThemeFontSizeOverride("normal_font_size", FooterFontSize);
            thanks.AddThemeFontSizeOverride("bold_font_size", FooterFontSize);
            // Strip the editor theme's default RichTextLabel panel background so the thanks paragraph blends with
            // the dock instead of sitting in a custom-coloured text box.
            thanks.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            AddChild(thanks);

            // --- Sign-off + Gold "GitHub Star" on ONE row: "Sincerely, Ivan Murzak" on the LEFT and the star
            //     button on the RIGHT, vertically centered — mirrors Unity, where the star floats on the sign-off line.
            var signRow = new HBoxContainer { Name = "SignRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            signRow.Alignment = BoxContainer.AlignmentMode.Center;

            var sincerely = new Label
            {
                Name = "Sincerely",
                Text = SupportFooterLinks.SincerelyText,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            sincerely.AddThemeFontSizeOverride("font_size", FooterFontSize);
            signRow.AddChild(sincerely);

            signRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

            var star = DockStyle.GoldenButton(
                "GitHubStar", "GitHub Star", DockTheme.StarIconFileName,
                () => OS.ShellOpen(SupportFooterLinks.RepositoryUrl));
            star.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            signRow.AddChild(star);

            AddChild(signRow);
        }

        /// <summary>
        /// Open the <see cref="SerializationCheckWindow"/> — the Godot port of Unity's "Check" button. A fresh
        /// window is parented under the footer (a Godot <see cref="Window"/> renders as its own OS window
        /// regardless of where it sits in the tree) and popped centred; it frees itself on close (mirrors how
        /// <c>FeaturesPanel</c> opens its <c>FeatureListWindow</c>).
        /// </summary>
        void OnCheckPressed()
        {
            var window = new SerializationCheckWindow();
            AddChild(window);
            window.PopupCenteredAndShow();
        }
    }
}
#endif
