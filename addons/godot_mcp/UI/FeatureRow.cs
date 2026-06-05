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
    /// One row of the dock's <see cref="FeaturesPanel"/> for a single <see cref="GodotMcpFeatureKind"/>: a
    /// "&lt;Title&gt;: enabled / total" count label, an optional "~N tokens" sub-label (tools only), and an
    /// "Open" button that raises the panel-supplied open callback. All text comes from the pure-managed
    /// <see cref="FeaturesPanelView"/> so the formatting is unit-tested. Editor-only (<c>#if TOOLS</c>):
    /// verified via the headless Godot smoke (test.md Suite 3).
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
            AddThemeConstantOverride("separation", 8);

            _countLabel = new Label
            {
                Name = "CountLabel",
                Text = FeaturesPanelView.UnavailableLabel(Kind),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(_countLabel);

            if (_showTokens)
            {
                _tokenLabel = new Label
                {
                    Name = "TokenLabel",
                    Text = FeaturesPanelView.UnavailableTokenLabel()
                };
                _tokenLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.60f, 0.60f));
                AddChild(_tokenLabel);
            }

            var openButton = new Button { Name = "OpenButton", Text = "Open" };
            openButton.Pressed += () => _onOpen(Kind);
            AddChild(openButton);
        }

        /// <summary>Show the "&lt;Title&gt;: enabled / total" counts (+ "~N tokens" for tools).</summary>
        public void ShowCounts(int enabled, int total, int enabledTokenCount)
        {
            _countLabel.Text = FeaturesPanelView.CountLabel(Kind, enabled, total);
            if (_tokenLabel != null)
                _tokenLabel.Text = FeaturesPanelView.TokenLabel(enabledTokenCount);
        }

        /// <summary>Show the "—" placeholder (no connection/managers yet).</summary>
        public void ShowUnavailable()
        {
            _countLabel.Text = FeaturesPanelView.UnavailableLabel(Kind);
            if (_tokenLabel != null)
                _tokenLabel.Text = FeaturesPanelView.UnavailableTokenLabel();
        }
    }
}
#endif
