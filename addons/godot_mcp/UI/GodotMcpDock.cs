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
    /// Root editor-dock <see cref="Control"/> for the Godot-MCP addon — the "AI Game Developer" panel
    /// the user docks in the Godot editor. This FOUNDATION scaffold builds only the header section (title
    /// + addon version) and exposes a <see cref="Body"/> container plus a <see cref="Refresh"/> hook that
    /// later tasks fill in (connection status / mode toggle, features list, footer, cloud auth). It is
    /// registered/unregistered by <see cref="GodotMcpPlugin"/> via
    /// <c>AddControlToDock</c>/<c>RemoveControlFromDocks</c>.
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it constructs live Godot UI <see cref="Node"/>s, so it is verified
    /// via the headless Godot smoke (see <c>test.md</c> Suite 3), not the plain-xUnit host.
    /// </para>
    /// </summary>
    [Tool]
    public partial class GodotMcpDock : VBoxContainer
    {
        /// <summary>
        /// Addon version shown in the dock header. Sourced from the SAME single source of truth as the MCP
        /// handshake / local-server pin — <see cref="Connection.GodotMcpConnection.PluginVersion"/>, which is
        /// parsed once from <c>res://addons/godot_mcp/plugin.cfg</c> (present on the resource path in every
        /// install, since Godot needs it to enable the addon). Deriving it here means the header can never
        /// drift from <c>plugin.cfg</c> — the bug that previously left it pinned at a stale literal (issue #94).
        /// </summary>
        public static readonly string AddonVersion = Connection.GodotMcpConnection.PluginVersion;

        /// <summary>The dock's display title (also its tab name in the editor dock).</summary>
        public const string DockTitle = "AI Game Developer";

        /// <summary>
        /// Container into which later tasks insert the connection / features / footer / cloud-auth
        /// sections. Populated by <see cref="BuildUi"/> with the <see cref="ConnectionPanel"/>; later tasks
        /// add their sections here rather than re-parenting the whole dock.
        /// </summary>
        public VBoxContainer? Body { get; private set; }

        readonly GodotMcpConnection? _connection;
        ConnectionPanel? _connectionPanel;
        FeaturesPanel? _featuresPanel;
        AgentConfiguratorsPanel? _agentConfiguratorsPanel;
        SkillsPanel? _skillsPanel;
        ExtensionsPanel? _extensionsPanel;
        SupportFooter? _supportFooter;

        // Log Level selector (header). Only built when a live connection was threaded in (it reads/writes
        // the connection's config). The OptionButton item ids are the GodotMcpLogLevel enum ordinals.
        OptionButton? _logLevelSelector;
        Label? _logLevelOverrideNote;

        /// <summary>
        /// Construct the dock wired to the live <paramref name="connection"/> so its connection panel can
        /// show status and drive Connect/Disconnect/mode/URL. <see cref="GodotMcpPlugin"/> owns the
        /// connection and threads it through here. A null connection (defensive / design-preview) builds the
        /// header-only chrome with no connection panel.
        /// </summary>
        public GodotMcpDock(GodotMcpConnection? connection)
        {
            _connection = connection;
            Name = DockTitle;
            BuildUi();
        }

        /// <summary>
        /// Build the static dock chrome: a header (title + version) and the (currently empty)
        /// <see cref="Body"/> placeholder. Logo is intentionally omitted in this foundation (no committed
        /// logo asset yet) — a later task can add it to the header without changing this layout.
        ///
        /// <para>
        /// All chrome (header card + <see cref="Body"/>) is nested inside a vertical <see cref="ScrollContainer"/>
        /// so the dock can be resized SHORTER than its content height — the content then scrolls vertically
        /// instead of forcing the whole editor dock to stay tall. Horizontal scrolling is disabled so the
        /// content fits the dock width (the ScrollContainer sizes its child to the viewport width when the
        /// horizontal scroll mode is Disabled). The dock root keeps no vertical minimum of its own, so the
        /// editor lets the panel shrink (a ScrollContainer does NOT propagate its child's tall combined
        /// min-height up its own vertical axis).
        /// </para>
        /// </summary>
        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            // Let the dock be resized shorter than its content — the ScrollContainer (below) takes over the
            // overflow. Without this the VBoxContainer's combined child min-height would floor the panel tall.
            CustomMinimumSize = Vector2.Zero;

            // --- Scroll viewport ---
            // Wrap the whole dock body so a short panel scrolls vertically instead of forcing the dock tall.
            // Vertical scroll auto-shows; horizontal scroll is disabled so the inner content fits the width.
            var scroll = new ScrollContainer
            {
                Name = "DockScroll",
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            AddChild(scroll);

            // The single child of the ScrollContainer — holds header card + Body. ExpandFill horizontally so
            // it spans the scroll viewport width (the ScrollContainer sizes it to the viewport when horizontal
            // scroll is disabled); its natural (tall) height is what scrolls.
            var content = new VBoxContainer
            {
                Name = "ScrollContent",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            scroll.AddChild(content);

            // --- Header card (title + version + Log Level), wrapped in the styled card chrome. ---
            var header = new VBoxContainer { Name = "Header" };
            header.AddThemeConstantOverride("separation", 4);

            var title = new Label
            {
                Name = "Title",
                Text = DockTitle
            };
            DockStyle.ApplyHeader(title);
            header.AddChild(title);

            var version = new Label
            {
                Name = "Version",
                Text = $"v{AddonVersion}"
            };
            DockStyle.ApplyDescription(version);
            header.AddChild(version);

            // Log Level selector — routes the reused framework's verbosity (connection / handshake logs) to
            // the Godot Output. Compact, in the header so it is reachable for diagnostics regardless of which
            // section is open. Only meaningful with a live connection (it binds the connection's config).
            if (_connection != null)
                BuildLogLevelRow(header, _connection);

            content.AddChild(DockStyle.Card(header, "Header"));

            // --- Body ---
            // Hosts the connection section now; later tasks (features list, footer, cloud auth) add their
            // sections as further children of Body. Each major section is wrapped in a styled card so the dock
            // mimics Unity-MCP's MainWindow (dark-blue rounded frame-groups).
            Body = new VBoxContainer
            {
                Name = "Body",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            content.AddChild(Body);

            // Connection section — only when a live connection was threaded in.
            if (_connection != null)
            {
                _connectionPanel = new ConnectionPanel(_connection);
                Body.AddChild(DockStyle.Card(_connectionPanel, "Connection"));

                // MCP-features section — tools/prompts/resources counts + per-item enable/disable windows.
                // Inserted BETWEEN the connection panel and the support footer, wired to the live connection
                // (it reads the plugin's managers and subscribes to their update streams). Only meaningful with
                // a live connection, so it shares the connection-null guard with the connection panel.
                _featuresPanel = new FeaturesPanel(_connection);
                Body.AddChild(DockStyle.Card(_featuresPanel, "Features"));

                // AI-agent section — the dropdown of AI-agent configurators + the selected agent's Configure /
                // Remove (or, for Custom, the HTTP-config snippet). Inserted BETWEEN the features panel and the
                // support footer, wired to the live connection (it reads the resolved MCP-client URL + token off
                // the config and persists the selected agent via Save). Only meaningful with a live connection,
                // so it shares the connection-null guard with the connection + features panels.
                _agentConfiguratorsPanel = new AgentConfiguratorsPanel(_connection);
                Body.AddChild(DockStyle.Card(_agentConfiguratorsPanel, "AiAgent"));

                // Skills section — auto-generated SKILL.md output for the selected skills-capable agent: the resolved
                // skills path, an Auto-generate toggle (persisted via GenerateSkillFiles), and an on-demand Generate
                // button. Inserted BETWEEN the AI-agent card and the support footer, wired to the live connection (it
                // reads the persisted selected agent + drives the live plugin's GenerateSkillFiles). Only meaningful
                // with a live connection, so it shares the connection-null guard with the panels above.
                _skillsPanel = new SkillsPanel(_connection);
                Body.AddChild(DockStyle.Card(_skillsPanel, "Skills"));

                // The Skills card's supported-state + output path follow the selected AI agent, so re-render it when
                // the AI-agent dropdown selection changes (the panel persists the new SelectedAgentId first).
                _agentConfiguratorsPanel.AgentSelectionChanged += () => _skillsPanel?.Refresh();
            }

            // Extensions section — install/update more AI tool families into the consumer's Godot project via NuGet
            // PackageReference (read-modify-write the consumer .csproj). Inserted BETWEEN the Skills card and the
            // support footer. It reads its state SYNCHRONOUSLY from the consumer .csproj (no live connection /
            // subscriptions), so — like the footer — it builds UNCONDITIONALLY (independent of the connection); the
            // registry ships empty today, so it renders an honest "coming soon" placeholder.
            _extensionsPanel = new ExtensionsPanel();
            Body.AddChild(DockStyle.Card(_extensionsPanel, "Extensions"));

            // Support/footer section — static links + thanks, appended BELOW the connection panel. It holds
            // no live state / subscriptions, so it builds unconditionally (independent of the connection).
            _supportFooter = new SupportFooter();
            Body.AddChild(DockStyle.Card(_supportFooter, "Footer"));
        }

        /// <summary>
        /// Build the compact "Log Level" row in the header: a label + an <see cref="OptionButton"/> listing
        /// every <see cref="GodotMcpLogLevel"/> value (item id = enum ordinal), bound to the connection's
        /// EFFECTIVE level. On change → write the PERSISTED <see cref="GodotMcpConfig.LogLevel"/> + Save; the
        /// logger provider reads the level live, so no reconnect is needed. An <see cref="GodotMcpConfig.EnvLogLevel"/>
        /// (env/.env) override is surfaced by a note and disables the selector (editing it would not take
        /// effect live, unlike the connection-mode controls which write a layer the user can later re-enable).
        /// </summary>
        void BuildLogLevelRow(Container parent, GodotMcpConnection connection)
        {
            var row = new HBoxContainer { Name = "LogLevelRow" };
            parent.AddChild(row);

            row.AddChild(new Label { Name = "LogLevelLabel", Text = "Log Level" });

            _logLevelSelector = new OptionButton { Name = "LogLevelSelector" };
            foreach (GodotMcpLogLevel level in System.Enum.GetValues(typeof(GodotMcpLogLevel)))
                _logLevelSelector.AddItem(level.ToString(), (int)level);
            _logLevelSelector.ItemSelected += OnLogLevelSelected;
            row.AddChild(_logLevelSelector);

            _logLevelOverrideNote = new Label
            {
                Name = "LogLevelOverrideNote",
                Text = "Overridden by environment (GODOT_MCP_LOG_LEVEL).",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _logLevelOverrideNote.AddThemeColorOverride("font_color", new Color(0.92f, 0.74f, 0.20f));
            parent.AddChild(_logLevelOverrideNote);

            SyncLogLevelSelector();
        }

        /// <summary>
        /// Persist the chosen log level and Save. The selector id IS the enum ordinal. No reconnect — the
        /// logger provider reads the level live on every call. No-op when an env override pins the live level
        /// (the selector is disabled in that case, so this should not fire, but it is guarded defensively).
        /// </summary>
        void OnLogLevelSelected(long id)
        {
            if (_connection == null)
                return;

            var level = (GodotMcpLogLevel)(int)id;
            if (_connection.Config.LogLevel == level)
                return;

            _connection.Config.LogLevel = level;
            _connection.Save();
        }

        /// <summary>
        /// Reflect the EFFECTIVE log level in the selector and surface/disable on an env override. The
        /// selector shows <see cref="GodotMcpConfig.ActiveLogLevel"/> (env wins); when an env/.env value
        /// forces it away from the persisted <see cref="GodotMcpConfig.LogLevel"/>, the override note shows
        /// and the selector is disabled (a UI edit would not take effect live).
        /// </summary>
        void SyncLogLevelSelector()
        {
            if (_connection == null || _logLevelSelector == null)
                return;

            var active = _connection.Config.ActiveLogLevel;
            _logLevelSelector.Selected = _logLevelSelector.GetItemIndex((int)active);

            var overridden = active != _connection.Config.LogLevel;
            _logLevelSelector.Disabled = overridden;
            if (_logLevelOverrideNote != null)
                _logLevelOverrideNote.Visible = overridden;
        }

        /// <summary>
        /// Re-render the dock from current state. No-op in this foundation scaffold — there is no dynamic
        /// state to show yet. The later connection task fills this in (status line, mode indicator) and
        /// the connection layer calls it on the editor main thread (e.g. via the main-thread dispatcher)
        /// when the connection state changes. Safe to call any number of times.
        /// </summary>
        public void Refresh()
        {
            _connectionPanel?.Refresh();
            _featuresPanel?.Refresh();
            _agentConfiguratorsPanel?.Refresh();
            _skillsPanel?.Refresh();
            _extensionsPanel?.Refresh();
            SyncLogLevelSelector();
        }
    }
}
#endif
