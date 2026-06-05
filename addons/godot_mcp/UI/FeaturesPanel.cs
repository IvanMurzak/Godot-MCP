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
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The MCP-features section of the Godot-MCP editor dock — the Godot <see cref="Control"/> analog of
    /// Unity-MCP's <c>MainWindowEditor.McpFeatures</c> (its Tools/Prompts/Resources count rows + "Open"
    /// buttons). A <see cref="VBoxContainer"/> the <see cref="GodotMcpDock"/> drops into its Body BETWEEN the
    /// connection panel and the support footer, wired to a <see cref="GodotMcpConnection"/>. It renders three
    /// rows (tools / prompts / resources); each row shows a "&lt;Title&gt;: enabled / total" count label (plus,
    /// for tools, a "~N tokens" sub-label) and an "Open" button that opens a <see cref="FeatureListWindow"/>
    /// for per-item enable/disable.
    ///
    /// <para>
    /// Counts update live: the panel subscribes to the connection's <see cref="GodotMcpConnection.FeaturesUpdated"/>
    /// (any tool/prompt/resource registry change) and <see cref="GodotMcpConnection.ConnectionStatusChanged"/>
    /// (a (re)build swaps the managers), both marshalled onto the editor main thread by the connection, so the
    /// handlers touch Controls directly. Before a connection/managers exist, the counts show the "—" placeholder
    /// and refresh once the plugin is built.
    /// </para>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it builds live Godot UI <see cref="Node"/>s, so it is verified via the
    /// headless Godot smoke (<c>test.md</c> Suite 3), not the plain-xUnit host. ALL label formatting lives in
    /// the pure-managed <see cref="FeaturesPanelView"/> so it IS unit-tested.
    /// </para>
    /// </summary>
    [Tool]
    public partial class FeaturesPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        FeatureRow _toolsRow = null!;
        FeatureRow _promptsRow = null!;
        FeatureRow _resourcesRow = null!;

        public FeaturesPanel(GodotMcpConnection connection)
        {
            _connection = connection;
            Name = "FeaturesPanel";
            BuildUi();

            // Subscribe AFTER the UI exists so the first push has controls to write to. The connection
            // marshals these onto the editor main thread, so the handlers may touch Controls directly.
            _connection.FeaturesUpdated += OnFeaturesUpdated;
            _connection.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Render current state immediately (the events only fire on change).
            RefreshAll();
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            var header = new Label { Name = "FeaturesHeader", Text = "MCP Features" };
            DockStyle.ApplySectionTitle(header);
            AddChild(header);

            _toolsRow = new FeatureRow(GodotMcpFeatureKind.Tools, showTokens: true, OnOpenPressed);
            AddChild(_toolsRow);

            _promptsRow = new FeatureRow(GodotMcpFeatureKind.Prompts, showTokens: false, OnOpenPressed);
            AddChild(_promptsRow);

            _resourcesRow = new FeatureRow(GodotMcpFeatureKind.Resources, showTokens: false, OnOpenPressed);
            AddChild(_resourcesRow);

            AddChild(new HSeparator { Name = "FeaturesSeparator" });
        }

        void OnFeaturesUpdated() => RefreshAll();

        void OnConnectionStatusChanged(ConnectionStatus _) => RefreshAll();

        void OnOpenPressed(GodotMcpFeatureKind kind)
        {
            // Open (or focus) a list window for this kind. The window reads the live items off the connection
            // and persists toggles back through it. Parent it to this panel so it lives in the editor tree and
            // is freed with the dock if still open.
            var window = new FeatureListWindow(_connection, kind);
            AddChild(window);
            window.PopupCenteredAndShow();
        }

        /// <summary>Re-read counts for all three rows from the connection's managers and update the labels.</summary>
        void RefreshAll()
        {
            RefreshRow(_toolsRow);
            RefreshRow(_promptsRow);
            RefreshRow(_resourcesRow);
        }

        void RefreshRow(FeatureRow row)
        {
            var counts = _connection.GetFeatureCounts(row.Kind);
            if (counts == null)
            {
                row.ShowUnavailable();
                return;
            }

            var (enabled, total, tokenCount) = counts.Value;
            row.ShowCounts(enabled, total, tokenCount);
        }

        /// <summary>Forwarded from <see cref="GodotMcpDock.Refresh"/>. Re-reads counts. Safe to call repeatedly.</summary>
        public void Refresh() => RefreshAll();

        public override void _ExitTree()
        {
            // Unsubscribe so a freed panel does not receive a late main-thread push.
            _connection.FeaturesUpdated -= OnFeaturesUpdated;
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }
    }
}
#endif
