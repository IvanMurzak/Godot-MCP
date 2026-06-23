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
using com.IvanMurzak.Godot.MCP.Connection.DevControl;
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
        ExtensionsPanel? _extensionsPanel;
        SupportFooter? _supportFooter;

        // Log Level selector (header). Only built when a live connection was threaded in (it reads/writes
        // the connection's config). The OptionButton item ids are the GodotMcpLogLevel enum ordinals.
        OptionButton? _logLevelSelector;
        Label? _logLevelOverrideNote;

        // The ConnectionPanel.ConfigChanged → AgentConfiguratorsPanel.Refresh bridge, stored in a field (not an
        // inline lambda) so it can be deterministically removed on teardown. An inline `+= () => ...` was a genuine
        // delegate leak: it was never stored and never `-=`d, so it stayed rooted on the ConnectionPanel's C# event
        // across the dock's lifetime and contributed a leftover managed connection at assembly unload. Wired with
        // the same remove-then-add _EnterTree / -= _ExitTree discipline the connection panels use, so a benign dock
        // reparent re-arms it and a permanent free drops it.
        System.Action? _configChangedHandler;

        /// <summary>
        /// Parameterless constructor required by Godot's C# hot-reload bridge: on a "Build Project" reload
        /// (godotengine/godot#51626) the engine re-instantiates every live <c>[Tool]</c> script via its
        /// parameterless ctor, and a class with only a parameter ctor throws
        /// <c>MissingMemberException: ... does not define a parameterless constructor</c> (which crashed the
        /// editor before this was added). The reloaded plugin re-adds a FRESH, fully-wired dock (see
        /// <c>GodotMcpPlugin</c>'s reload re-entry), so this re-instantiated shell is a discarded orphan — it
        /// only has to exist without faulting. It builds the header-only chrome (null connection) defensively.
        /// </summary>
        public GodotMcpDock() : this(null) { }

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
        /// (Re)arm the dock's own cross-panel wiring on every tree entry — including the re-attach the editor
        /// performs during dock-layout restore (which fires <see cref="_ExitTree"/> → <c>_EnterTree</c>). Currently
        /// just the <see cref="ConnectionPanel.ConfigChanged"/> → <see cref="AgentConfiguratorsPanel.Refresh"/>
        /// bridge. Remove-then-add so a re-entry never double-subscribes; pairs with <see cref="_ExitTree"/>. The
        /// per-control signal/button wiring is owned by each panel's own _EnterTree, not here.
        /// </summary>
        public override void _EnterTree()
        {
            if (_connectionPanel != null && _configChangedHandler != null)
            {
                _connectionPanel.ConfigChanged -= _configChangedHandler;
                _connectionPanel.ConfigChanged += _configChangedHandler;
            }
        }

        /// <summary>
        /// Tear down the dock's own cross-panel wiring on tree exit. Fires both on a benign dock reparent (re-armed
        /// by <see cref="_EnterTree"/>) and on the permanent plugin-unload free; in both cases removing the stored
        /// <see cref="_configChangedHandler"/> from the connection panel's event drops the only delegate this dock
        /// roots directly, so nothing leaks into a C# assembly unload (issue #132). Per-control Godot-signal
        /// connections are OBJECT+METHOD Callables (not delegates), so they need no managed sweep — they are severed
        /// when each child's subtree is freed on the unload path.
        /// </summary>
        public override void _ExitTree()
        {
            if (_connectionPanel != null && _configChangedHandler != null)
                _connectionPanel.ConfigChanged -= _configChangedHandler;
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

            // --- Background ---
            // A flat neutral dark-gray panel filling the whole dock, so the window reads as Unity's darker, grayer
            // background instead of Godot's default bluish editor-dock tint. Everything (scroll + content) nests in it.
            var background = new PanelContainer
            {
                Name = "DockBackground",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            var bgStyle = new StyleBoxFlat { BgColor = DockStyle.Rgb(DockTheme.WindowBackground) };
            background.AddThemeStyleboxOverride("panel", bgStyle);
            AddChild(background);

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
            background.AddChild(scroll);

            // The single child of the ScrollContainer — holds header card + Body. ExpandFill horizontally so
            // it spans the scroll viewport width (the ScrollContainer sizes it to the viewport when horizontal
            // scroll is disabled); its natural (tall) height is what scrolls.
            // A horizontal gutter so sections don't sit flush against the dock edges — the per-section cards that
            // used to supply this margin are gone (Unity separates sections with dividers, not frames; only the
            // MCP-server + AI-agent bodies keep a frame).
            var contentMargin = new MarginContainer { Name = "ContentMargin", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            contentMargin.AddThemeConstantOverride("margin_left", 8);
            contentMargin.AddThemeConstantOverride("margin_right", 8);
            contentMargin.AddThemeConstantOverride("margin_top", 8);
            contentMargin.AddThemeConstantOverride("margin_bottom", 8);
            scroll.AddChild(contentMargin);

            var content = new VBoxContainer
            {
                Name = "ScrollContent",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            content.AddThemeConstantOverride("separation", 8);
            contentMargin.AddChild(content);

            // --- Header: a ROW with a LEFT base-config column (title + Log Level + Version) and a
            // RIGHT-aligned AI-cube logo, mirroring Unity-MCP's MainWindow header (config block + imgLogo).
            var header = new HBoxContainer { Name = "Header", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            header.AddThemeConstantOverride("separation", 8);

            // Left column: title over the base-config fields.
            var headerInfo = new VBoxContainer { Name = "HeaderInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerInfo.AddThemeConstantOverride("separation", 4);
            header.AddChild(headerInfo);

            var title = new Label
            {
                Name = "Title",
                Text = DockTitle
            };
            DockStyle.ApplyHeader(title);
            headerInfo.AddChild(title);

            // Log Level selector — routes the reused framework's verbosity (connection / handshake logs) to
            // the Godot Output. Compact, in the header so it is reachable for diagnostics regardless of which
            // section is open. Only meaningful with a live connection (it binds the connection's config).
            if (_connection != null)
                BuildLogLevelRow(headerInfo, _connection);

            var version = new Label
            {
                Name = "Version",
                Text = $"Version  {AddonVersion}"
            };
            DockStyle.ApplyDescription(version);
            headerInfo.AddChild(version);

            // Right: the AI-cube logo (square, 60px). Omitted silently when the asset is missing / un-imported.
            var logo = DockStyle.HeaderLogo();
            if (logo != null)
                header.AddChild(logo);

            content.AddChild(header);
            content.AddChild(DockStyle.Divider());

            // --- Body ---
            // Sections are NOT each wrapped in a frame anymore — Unity separates them with horizontal dividers and
            // keeps a frame only around the MCP-server config (inside ConnectionPanel) and the AI-agent body (inside
            // AgentConfiguratorsPanel). Order mirrors Unity's MainWindow: Connection → AI agent → MCP features →
            // Extensions → Footer, each followed by a divider.
            Body = new VBoxContainer
            {
                Name = "Body",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            // Wider separation so the dark 1px section dividers (Connection / AI agent / MCP features / …) get
            // visible breathing room above and below — the sections read as distinct bands, not a dense stack.
            Body.AddThemeConstantOverride("separation", 14);
            content.AddChild(Body);

            // Connection + AI-agent + MCP-features sections — only when a live connection was threaded in.
            if (_connection != null)
            {
                _connectionPanel = new ConnectionPanel(_connection);
                Body.AddChild(_connectionPanel);
                Body.AddChild(DockStyle.Divider());

                // AI-agent section — the "AI agent" dropdown (UNframed) + the selected agent's framed body (Skills
                // now lives INSIDE that body, above the MCP config line — Unity's containerSkills order). Wired to the
                // live connection (resolved MCP-client URL + token; persists the selected agent via Save).
                _agentConfiguratorsPanel = new AgentConfiguratorsPanel(_connection);
                Body.AddChild(_agentConfiguratorsPanel);
                Body.AddChild(DockStyle.Divider());

                // When the user changes the server URL / mode / auth / token, re-render the AI-agent section so the
                // selected agent re-checks its on-disk config and surfaces "Reconfiguration Required" + the Reconfigure
                // button (mirrors Unity, where each configurator re-evaluates on a settings change). Stored in a field
                // (see _configChangedHandler) and wired in _EnterTree (remove-then-add) so it is torn down cleanly on
                // teardown rather than leaking as an un-removable inline lambda.
                _configChangedHandler = () => _agentConfiguratorsPanel?.Refresh();

                // MCP-features section — tools/prompts/resources counts + per-item enable/disable windows. Placed
                // AFTER the AI-agent section (Unity order), wired to the live connection's managers.
                _featuresPanel = new FeaturesPanel(_connection);
                Body.AddChild(_featuresPanel);
                Body.AddChild(DockStyle.Divider());
            }

            // Extensions section — install/update more AI tool families via NuGet PackageReference. Reads its state
            // SYNCHRONOUSLY from the consumer .csproj (no live connection), so it builds UNCONDITIONALLY; the registry
            // ships empty today, so it renders an honest "coming soon" placeholder.
            _extensionsPanel = new ExtensionsPanel();
            Body.AddChild(_extensionsPanel);
            Body.AddChild(DockStyle.Divider());

            // Support/footer section — static links + thanks. Holds no live state, so it builds unconditionally.
            _supportFooter = new SupportFooter();
            Body.AddChild(_supportFooter);
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
            // Object+method Callable (not delegate +=) so the connection never enters the ManagedCallable
            // hot-reload registry.
            _logLevelSelector.Connect(OptionButton.SignalName.ItemSelected, new Callable(this, MethodName.OnLogLevelSelected));
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
        public void OnLogLevelSelected(long id)
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
            _extensionsPanel?.Refresh();
            SyncLogLevelSelector();
        }

        // ===================================================================================================
        //  DEV-ONLY inject / control / state API
        // ---------------------------------------------------------------------------------------------------
        //  Surface for the env-gated, 127.0.0.1-bound DevControlServer (Connection/DevControl/) so a terminal
        //  / AI agent can (a) INJECT fake states onto the LIVE dock to test UI rendering and (b) CONTROL it —
        //  simulate user actions (set Server URL, switch agent, click Configure/Connect/Start) — without
        //  clicking by hand. INERT in a shipped addon: the server only starts when GODOT_MCP_DEV_CONTROL=1.
        //
        //  Inject delegates to ConnectionPanel's dev API; control + state walk the live dock tree by node
        //  Name (the Names are stable, assigned in BuildUi here / in ConnectionPanel / AgentConfiguratorsPanel)
        //  and drive the real controls (set Text + EmitSignal, Select + EmitSignal, EmitSignal "pressed"), so
        //  the same code paths a human click would hit run. ALL of these MUST be called on the editor main
        //  thread (the DevControlServer hops via MainThreadDispatcher).
        // ===================================================================================================

        /// <summary>DEV-ONLY: inject a fake connection status (Connected/Connecting/Disconnected) onto the dock and pin it.</summary>
        public void DevInjectConnectionStatus(string status)
        {
            if (!DevControlRouter.TryParseConnectionStatus(status, out var parsed))
                throw new System.ArgumentException($"invalid connection-status '{status}'", nameof(status));
            if (_connectionPanel == null)
                throw new System.InvalidOperationException("connection panel not present (no live connection)");
            _connectionPanel.DevInjectStatus(parsed);
        }

        /// <summary>DEV-ONLY: inject a fake local-server status (Stopped/Starting/Running/Stopping/External) onto the dock.</summary>
        public void DevInjectServerStatus(string status)
        {
            if (!DevControlRouter.TryParseServerStatus(status, out var parsed))
                throw new System.ArgumentException($"invalid server-status '{status}'", nameof(status));
            if (_connectionPanel == null)
                throw new System.InvalidOperationException("connection panel not present (no live connection)");
            _connectionPanel.DevInjectServerStatus(parsed);
        }

        /// <summary>
        /// DEV-ONLY: paint <paramref name="count"/> fake connected AI agents onto the dock's AI-agent point (green dot
        /// + rows + "(N connected)" summary). A count of 0 clears the override (re-converges to the live list).
        /// </summary>
        public void DevInjectAgents(int count)
        {
            if (_connectionPanel == null)
                throw new System.InvalidOperationException("connection panel not present (no live connection)");
            if (count <= 0)
            {
                _connectionPanel.DevClearAgentsOverride();
                return;
            }
            var agents = new System.Collections.Generic.List<com.IvanMurzak.McpPlugin.Common.Model.McpClientData>();
            for (int i = 1; i <= count; i++)
                agents.Add(new com.IvanMurzak.McpPlugin.Common.Model.McpClientData
                {
                    IsConnected = true,
                    ClientTitle = $"AI Agent {i}",
                    ClientVersion = "1.0.0",
                    SessionId = $"dev-session-{i}"
                });
            _connectionPanel.DevInjectAgents(agents);
        }

        /// <summary>
        /// DEV-ONLY: drive the Custom-mode Server URL field as a user would — set the LineEdit text and emit
        /// its <c>text_submitted</c> signal so the panel runs its real commit/validate/reconnect path.
        /// </summary>
        public void DevSetServerUrl(string url)
        {
            var field = FindChild("HostField", recursive: true, owned: false) as LineEdit
                ?? throw new System.InvalidOperationException("HostField not found (Custom mode may be hidden)");
            field.Text = url ?? string.Empty;
            field.EmitSignal(LineEdit.SignalName.TextSubmitted, field.Text);
        }

        /// <summary>
        /// DEV-ONLY: select an AI agent by its display NAME or registry agent-id — sets the OptionButton
        /// selection and emits <c>item_selected</c> so the panel runs its real persist + re-render path.
        /// </summary>
        public bool DevSelectAgent(string idOrName)
        {
            var selector = FindChild("AgentSelector", recursive: true, owned: false) as OptionButton
                ?? throw new System.InvalidOperationException("AgentSelector not found");

            // Resolve by registry agent-id first (stable), then fall back to the visible display name.
            var index = Agents.GodotAgentConfigurators.GetIndexByAgentId(idOrName);
            if (index < 0)
            {
                var names = Agents.GodotAgentConfigurators.AgentNames;
                for (int i = 0; i < names.Count; i++)
                {
                    if (string.Equals(names[i], idOrName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }
            if (index < 0)
                return false;

            // The OptionButton item id IS the registry index (see AgentConfiguratorsPanel.BuildUi).
            var itemIndex = selector.GetItemIndex(index);
            if (itemIndex < 0)
                return false;

            selector.Selected = itemIndex;
            selector.EmitSignal(OptionButton.SignalName.ItemSelected, itemIndex);
            return true;
        }

        /// <summary>
        /// DEV-ONLY: simulate a click on one of the dock's primary buttons by emitting its <c>pressed</c>
        /// signal — running the exact handler a human click would. Returns false when the target's button is
        /// not present (e.g. the Remove button is hidden until the agent is configured, the Start-server
        /// button only exists in Custom mode). Targets: configure|reconfigure|remove|connect|start-server|generate.
        /// </summary>
        public bool DevClick(string target)
        {
            if (!DevControlRouter.TryNormalizeClickTarget(target, out var normalized))
                throw new System.ArgumentException($"invalid click target '{target}'", nameof(target));

            // Map the normalized target to the live button's node Name. "reconfigure" is the same button as
            // "configure" (its label flips to "Reconfigure" once configured — the node Name stays "Configure").
            var nodeName = normalized switch
            {
                "configure" or "reconfigure" => "Configure",
                "remove" => "Remove",
                "connect" => "ConnectButton",
                "start-server" => "LocalServerStartStopButton",
                "generate" => "Generate",                  // Skills panel's "Generate" button
                "generate-token" => "GenerateTokenButton", // Custom-mode auth token "New" button (distinct)
                "authorize" => "AuthorizeButton",
                "revoke" => "RevokeButton",
                "reveal" => "Reveal",
                "copy" => "Copy",
                "check" => "Check",                        // Footer "Check" → opens the Serialization Check window
                _ => null,
            };
            if (nodeName == null)
                return false;

            var button = FindChild(nodeName, recursive: true, owned: false) as BaseButton;
            if (button == null || !IsInstanceValid(button))
                return false;

            button.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        /// <summary>
        /// DEV-ONLY: drive a segmented control as a user tap would — emit the target segment button's
        /// <c>pressed</c> signal so the panel runs its REAL selection handler (persist + re-render). Drives
        /// the connection-mode switch (<c>mode</c>: custom|cloud) and the Custom-mode Authorization
        /// "transport" switch (<c>auth</c>: none|required). Returns false when the control or its segment is
        /// not present (e.g. the auth segmented exists in Custom mode only). This is also the path that proves
        /// the segment's <c>Pressed</c> delegate is rooted (the GC'd-signal-delegate fix).
        /// </summary>
        public bool DevSetSegment(string control, string option)
        {
            if (!DevControlRouter.TryNormalizeSegment(control, option, out var normControl, out var index))
                throw new System.ArgumentException($"invalid segment control/option '{control}'/'{option}'");

            // The inner track HBoxContainer carries the control's Name; the segment buttons are "Segment{i}"
            // children of it (see DockStyle.SegmentedControl). Scope the segment lookup to the track so the
            // mode/auth tracks' identically-named "Segment0/1" children never collide.
            var trackName = normControl switch
            {
                "mode" => "ModeSegmented",
                "auth" => "AuthSegmented",
                _ => null,
            };
            if (trackName == null)
                return false;

            var track = FindChild(trackName, recursive: true, owned: false);
            if (track == null)
                return false;

            var segment = track.FindChild("Segment" + index, recursive: false, owned: false) as BaseButton;
            if (segment == null || !IsInstanceValid(segment))
                return false;

            segment.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        /// <summary>
        /// DEV-ONLY: simulate a successful Cloud authorization — persist <paramref name="token"/> through the
        /// dock's REAL persist+reconnect path (<see cref="ConnectionPanel.DevSimulateCloudAuthorized"/>), so the
        /// auth UI (masked field, Revoke button, alert visibility) and the reconnect/stale-rejection guard can be
        /// exercised without a live browser OAuth round-trip. Returns false when the connection panel is absent.
        /// </summary>
        public bool DevCloudAuthorize(string token)
        {
            if (_connectionPanel == null)
                return false;
            _connectionPanel.DevSimulateCloudAuthorized(token ?? string.Empty);
            return true;
        }

        /// <summary>
        /// DEV-ONLY: a JSON snapshot of what the dock is currently rendering — connection status label,
        /// Connect-button text, the Server URL field, the selected agent name, the agent config status text,
        /// and whether the AI-agent "AgentAlert" node is present + visible. Read by <c>GET /state</c> so a
        /// terminal / AI agent can assert on the live UI without scraping pixels.
        /// </summary>
        public string DevStateJson()
        {
            string? Text(string name) =>
                (FindChild(name, recursive: true, owned: false) as Label)?.Text;
            string? ButtonText(string name) =>
                (FindChild(name, recursive: true, owned: false) as Button)?.Text;
            // A node is "visible" only when present AND its IsVisibleInTree (so a hidden ancestor — e.g. the
            // whole CloudAuthRow — correctly reports its children as not-visible).
            bool VisibleInTree(string name) =>
                FindChild(name, recursive: true, owned: false) is Control c && c.IsVisibleInTree();

            var hostField = FindChild("HostField", recursive: true, owned: false) as LineEdit;
            var cloudTokenField = FindChild("CloudTokenField", recursive: true, owned: false) as LineEdit;
            // Cloud vs Custom is observable from which mode-row is shown (CustomHostRow only in Custom mode).
            var connectionMode = VisibleInTree("CustomHostRow") ? "Custom" : "Cloud";
            var agentSelector = FindChild("AgentSelector", recursive: true, owned: false) as OptionButton;
            string? selectedAgent = null;
            if (agentSelector != null && agentSelector.Selected >= 0)
                selectedAgent = agentSelector.GetItemText(agentSelector.Selected);

            var alert = FindChild("AgentAlert", recursive: true, owned: false) as Control;

            // Hand-built JSON so this file stays free of a System.Text.Json dependency in the dock layer; the
            // DevControlServer embeds this verbatim under a "dock" key. All values are escaped.
            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            // The Godot timeline label (PanelContainer "GodotLabel" → inner Label "Text") now carries the
            // status ("Godot" / "Godot: connecting..." / "Godot: connected"); read it for connectionStatus.
            var godotLabel = FindChild("GodotLabel", recursive: true, owned: false)?
                .FindChild("Text", recursive: false, owned: false) as Label;
            AppendJson(sb, "dockTitle", DockTitle); sb.Append(',');
            AppendJson(sb, "connectionStatus", godotLabel?.Text); sb.Append(',');
            AppendJson(sb, "connectButtonText", ButtonText("ConnectButton")); sb.Append(',');
            AppendJson(sb, "serverUrl", hostField?.Text); sb.Append(',');
            AppendJson(sb, "selectedAgent", selectedAgent); sb.Append(',');
            AppendJson(sb, "agentConfigStatus", Text("Status")); sb.Append(',');
            // Live AI-agent SESSION display (distinct from the agent CONFIGURATOR status above): the muted summary
            // suffix ("(N connected)") + the number of rendered per-agent rows.
            AppendJson(sb, "agentSessionSummary", Text("AgentSuffix")); sb.Append(',');
            sb.Append("\"agentSessionRows\":")
              .Append((FindChild("AgentList", recursive: true, owned: false) as VBoxContainer)?.GetChildCount() ?? 0)
              .Append(',');
            sb.Append("\"agentAlertPresent\":").Append(alert != null ? "true" : "false").Append(',');
            sb.Append("\"agentAlertVisible\":").Append(alert is { Visible: true } ? "true" : "false").Append(',');
            // --- Connection mode + Cloud-auth UI truth (so the dev-control auth flow is fully assertable) ---
            // connectionMode reflects the DISPLAYED/persisted mode (which row is editable). When an env/.env
            // override forces a different ACTIVE mode, the OverrideNote is shown — surface it so a tester knows
            // the live connection mode differs from the displayed one (e.g. testbed .env pins Custom).
            AppendJson(sb, "connectionMode", connectionMode); sb.Append(',');
            sb.Append("\"modeOverrideNoteVisible\":").Append(VisibleInTree("OverrideNote") ? "true" : "false").Append(',');
            sb.Append("\"cloudTokenPresent\":").Append(!string.IsNullOrEmpty(cloudTokenField?.Text) ? "true" : "false").Append(',');
            sb.Append("\"revokeButtonVisible\":").Append(VisibleInTree("RevokeButton") ? "true" : "false").Append(',');
            sb.Append("\"authRequiredAlertVisible\":").Append(VisibleInTree("AuthRequiredAlert") ? "true" : "false").Append(',');
            sb.Append("\"connectionRequiredAlertVisible\":").Append(VisibleInTree("ConnectionRequiredAlert") ? "true" : "false").Append(',');
            AppendJson(sb, "cloudAuthStatus", Text("CloudAuthStatus")); sb.Append(',');
            // --- Dock-registration diagnostics (the godot#51626 "missing AI Game Developer tab" guard) ---
            // After an in-session C# rebuild, Godot's hot-reload frees this dock and (unfixed) never re-adds it.
            // These three fields let a headless dev-control harness ASSERT the dock is actually wired into the
            // editor dock tree — not merely that the bridge answered. diagInsideTree is THE assertion: it is the
            // VBoxContainer dock's IsInsideTree(), true only while the dock is parented under an editor dock slot.
            // diagParentPath / diagParentType surface where it is docked (or "<null>" when orphaned) for diagnosis.
            sb.Append("\"diagInsideTree\":").Append(IsInsideTree() ? "true" : "false").Append(',');
            // GetParentOrNull<Node>() returns null when un-parented (a plain GetParent() would log an error).
            var parent = IsInsideTree() ? GetParentOrNull<Node>() : null;
            AppendJson(sb, "diagParentPath", parent != null ? (string)parent.GetPath() : "<null>"); sb.Append(',');
            AppendJson(sb, "diagParentType", parent != null ? parent.GetClass() : "<null>");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Append a JSON <c>"key":"value"</c> pair (value JSON-escaped; null → JSON null) to <paramref name="sb"/>.</summary>
        static void AppendJson(System.Text.StringBuilder sb, string key, string? value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else
                            sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
#endif
