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
using System;
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// One row of the dock's <see cref="FeaturesPanel"/> for a single <see cref="GodotMcpFeatureKind"/>, styled to
    /// Unity-MCP's <c>row-top</c> layout (flex-start, space-between): a LEFT column with the "&lt;Title&gt;: X / Y"
    /// count as a 20px-bold header (<see cref="DockStyle.ApplyHeader"/>) over an optional "~N tokens total" 11px
    /// gray sub-label (tools only), and a RIGHT "Open" button skinned as Unity's <c>.btn-secondary</c>
    /// (<see cref="DockStyle.ApplyOpenButton"/>) that raises the panel-supplied open callback. All text comes from
    /// the pure-managed <see cref="FeaturesPanelView"/> so the formatting is unit-tested. Editor-only
    /// (<c>#if TOOLS</c>): verified via the headless Godot smoke (test.md Suite 3).
    /// </summary>
    [Tool]
    public partial class FeatureRow : HBoxContainer
    {
        /// <summary>The feature kind this row represents.</summary>
        public GodotMcpFeatureKind Kind { get; }

        readonly bool _showTokens;
        readonly Action<GodotMcpFeatureKind> _onOpen;

        Label _countLabel = null!;
        Label? _tokenLabel;

        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live [Tool] script via its parameterless ctor, so a parameter-only
        /// class throws MissingMemberException ("does not define a parameterless constructor") and breaks the
        /// reload (it crashed the editor before this was added). The reloaded plugin re-adds a FRESH, wired
        /// dock (see GodotMcpPlugin's reload re-entry), so this re-instantiated shell is a discarded orphan —
        /// it only has to exist without faulting.
        /// </summary>
        public FeatureRow() { }

        public FeatureRow(GodotMcpFeatureKind kind, bool showTokens, Action<GodotMcpFeatureKind> onOpen)
        {
            Kind = kind;
            _showTokens = showTokens;
            _onOpen = onOpen;
            Name = $"{FeaturesPanelView.Title(kind)}Row";
            BuildUi();
        }

        void BuildUi()
        {
            // Unity's "row-top": align children to the TOP (flex-start) and let the LEFT column expand so the
            // Open button hugs the right edge (space-between).
            AddThemeConstantOverride("separation", 8);
            Alignment = AlignmentMode.Begin;

            // --- LEFT: a column with the count header + (tools only) the "~N tokens total" sub-label. ---
            var leftColumn = new VBoxContainer
            {
                Name = "CountColumn",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            // Pull the "~N tokens total" sub-label up tight under the count header (negative separation halves the
            // intrinsic line gap between the 24px header and the 15px sub-label).
            leftColumn.AddThemeConstantOverride("separation", -4);
            AddChild(leftColumn);

            _countLabel = new Label
            {
                Name = "CountLabel",
                Text = FeaturesPanelView.UnavailableLabel(Kind)
            };
            DockStyle.ApplyHeader(_countLabel);
            leftColumn.AddChild(_countLabel);

            if (_showTokens)
            {
                _tokenLabel = new Label
                {
                    Name = "TokenLabel",
                    Text = FeaturesPanelView.UnavailableTokenTotalLabel()
                };
                _tokenLabel.AddThemeColorOverride("font_color", DockStyle.Rgb(DockTheme.ColorTokenSubLabel));
                _tokenLabel.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeTokenSubLabel);
                leftColumn.AddChild(_tokenLabel);
            }

            // --- RIGHT: the btn-secondary "Open" button, shrunk to the top-right (row-top, space-between). ---
            var openButton = new Button
            {
                Name = "OpenButton",
                Text = "Open",
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            DockStyle.ApplyOpenButton(openButton);
            // Object+method Callable to this row's own instance method (no captured-lambda delegate connection).
            DockStyle.ConnectPressed(openButton, this, MethodName.OnOpenPressed);
            AddChild(openButton);
        }

        /// <summary>Open-button <c>pressed</c> handler (object+method Callable): raise the panel's open callback for this kind.</summary>
        public void OnOpenPressed() => _onOpen(Kind);

        /// <summary>Show the "&lt;Title&gt;: enabled / total" counts (+ "~N tokens total" for tools).</summary>
        public void ShowCounts(int enabled, int total, int enabledTokenCount)
        {
            _countLabel.Text = FeaturesPanelView.CountLabel(Kind, enabled, total);
            if (_tokenLabel != null)
                _tokenLabel.Text = FeaturesPanelView.TokenTotalLabel(enabledTokenCount);
        }

        /// <summary>Show the "—" placeholder (no connection/managers yet).</summary>
        public void ShowUnavailable()
        {
            _countLabel.Text = FeaturesPanelView.UnavailableLabel(Kind);
            if (_tokenLabel != null)
                _tokenLabel.Text = FeaturesPanelView.UnavailableTokenTotalLabel();
        }
    }
}
#endif
