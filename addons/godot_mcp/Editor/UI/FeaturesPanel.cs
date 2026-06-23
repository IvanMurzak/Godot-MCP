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

        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live [Tool] script via its parameterless ctor, so a parameter-only
        /// class throws MissingMemberException ("does not define a parameterless constructor") and breaks the
        /// reload (it crashed the editor before this was added). The reloaded plugin re-adds a FRESH, wired
        /// dock (see GodotMcpPlugin's reload re-entry), so this re-instantiated shell is a discarded orphan —
        /// it only has to exist without faulting.
        /// </summary>
        public FeaturesPanel() { }

        public FeaturesPanel(GodotMcpConnection connection)
        {
            _connection = connection;
            Name = "FeaturesPanel";
            BuildUi();

            // The connection wiring — event subscription + the live count re-seed — is done in _EnterTree
            // (NOT here) so that a dock-layout reload (which DETACHES then RE-ATTACHES this Control, firing
            // _ExitTree → _EnterTree) re-arms it. Subscribing only in the ctor was the #56 bug, the same one
            // #42 fixed for ConnectionPanel: the editor reparents the dock during "Loading docks", _ExitTree
            // tore the subscription down, nothing re-subscribed the re-attached panel, so a later toggle's
            // FeaturesUpdated never reached it and the counts stayed stale (e.g. "36 / 36").
        }

        /// <summary>
        /// (Re)arm the panel's feature wiring every time it enters the editor tree — including the re-attach the
        /// editor performs during dock-layout restore. Subscribes to the connection events and re-seeds the
        /// counts from LIVE state (the events only fire on change, so a registry/managers change reached while
        /// detached must be pulled in here). Pairs with <see cref="_ExitTree"/>, which tears both down.
        /// Idempotent against duplicate subscription: the handlers are removed first (remove-then-add), mirroring
        /// the proven <c>ConnectionPanel</c> (#42) pattern.
        /// </summary>
        public override void _EnterTree()
        {
            // Remove-then-add so a re-entry never double-subscribes. The connection marshals these events onto
            // the editor main thread, so the handlers may touch Controls directly.
            _connection.FeaturesUpdated -= OnFeaturesUpdated;
            _connection.FeaturesUpdated += OnFeaturesUpdated;
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connection.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Re-seed from LIVE state: the events only fire on change, so any registry/managers change that
            // happened while the panel was detached (e.g. during the dock reparent) is pulled onto the labels
            // here. This is the load-bearing #56 fix — the panel ALWAYS converges to the real counts on
            // (re)entry, independent of event-delivery timing.
            RefreshAll();
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            // (No "MCP Features" section title — removed per design; the Tools/Prompts/Resources rows stand alone.)

            _toolsRow = new FeatureRow(GodotMcpFeatureKind.Tools, showTokens: true, OnOpenPressed);
            AddChild(_toolsRow);

            _promptsRow = new FeatureRow(GodotMcpFeatureKind.Prompts, showTokens: false, OnOpenPressed);
            AddChild(_promptsRow);

            _resourcesRow = new FeatureRow(GodotMcpFeatureKind.Resources, showTokens: false, OnOpenPressed);
            AddChild(_resourcesRow);

            // (No default HSeparator — it draws a light/white line; the dock already places a dark DockStyle.Divider
            //  between sections, which is the one we want.)
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
