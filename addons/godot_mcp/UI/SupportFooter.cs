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
            AddThemeConstantOverride("separation", 4);

            var prompt = new Label
            {
                Name = "Prompt",
                Text = SupportFooterLinks.PromptText
            };
            DockStyle.ApplyDescription(prompt);
            AddChild(prompt);

            // --- Support links: open externally; no live state. Styled as flat link buttons separated by "•". ---
            AddChild(DockStyle.LinkRow("Links", new System.Collections.Generic.List<(string, string, string)>
            {
                ("Discord", "Help / Talk", SupportFooterLinks.DiscordUrl),
                ("Issues", "Bug Report", SupportFooterLinks.IssuesUrl),
                ("Star", "Star", SupportFooterLinks.RepositoryUrl),
            }));

            // --- Thanks line (RichTextLabel so the product name can be emphasised). ---
            var thanks = new RichTextLabel
            {
                Name = "Thanks",
                BbcodeEnabled = true,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = $"[i]{SupportFooterLinks.ThanksText}[/i]"
            };
            AddChild(thanks);
        }
    }
}
#endif
