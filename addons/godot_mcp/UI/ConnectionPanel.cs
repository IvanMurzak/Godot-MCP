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
using System.Collections.Generic;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The connection section of the Godot-MCP editor dock — the Godot <see cref="Control"/> analog of
    /// Unity-MCP's <c>MainWindowEditor.Connection</c>, ported 1:1 to Unity-MCP's vertical TIMELINE design. A
    /// <see cref="VBoxContainer"/> the <see cref="GodotMcpDock"/> drops into its Body, wired to a
    /// <see cref="GodotMcpConnection"/>. It renders, top to bottom:
    /// <list type="bullet">
    ///   <item>A "Connection" header (20px bold) with a right-aligned Custom|Cloud segmented control.</item>
    ///   <item>Amber alert panels — "Authorization Required" (Cloud, no token) / "Connection Required"
    ///   (ready but not connected).</item>
    ///   <item>A vertical timeline of three points — Godot, MCP server, AI agent — each with a status circle
    ///   (filled green online / green ring connecting / filled orange disconnected) in a 20px indicator column
    ///   joined by a 2px connecting line, an underlined 13px label, and the point's content.</item>
    ///   <item>The Godot point: a right-aligned Connect/Disconnect button.</item>
    ///   <item>The MCP-server point: a frame-group card holding the Server URL (Custom mode) / cloud auth row
    ///   (Cloud mode) and the Authorization segmented + masked token field.</item>
    ///   <item>The AI-agent point: a status circle + label (no connecting line below).</item>
    /// </list>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it builds live Godot UI <see cref="Node"/>s, so it is verified via
    /// the headless Godot smoke (<c>test.md</c> Suite 3), not the plain-xUnit host. ALL presentation
    /// decisions (status reduction, label/button text, circle state, segmented index/selection, alert
    /// visibility, URL validation) live in the pure-managed <see cref="ConnectionPanelView"/> /
    /// <see cref="SegmentedControlModel"/> / <see cref="DockTheme"/> so they ARE unit-tested.
    /// </para>
    /// </summary>
    [Tool]
    public partial class ConnectionPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        // Header: Custom|Cloud mode segmented control.
        Control _modeSegmented = null!;
        static readonly IReadOnlyList<string> ModeOptions = new[] { ModeLabelCustom, ModeLabelCloud };
        const string ModeLabelCustom = "Custom";
        const string ModeLabelCloud = "Cloud";

        // Alert panels (amber WarningFrame): shown/hidden per ConnectionPanelView rules.
        PanelContainer _authRequiredAlert = null!;
        PanelContainer _connectionRequiredAlert = null!;

        // Timeline circles (re-styled in place per status change).
        Panel _timelineGodotCircle = null!;
        Panel _timelineServerCircle = null!;
        Panel _timelineAgentCircle = null!;

        // Godot point: status label + Connect/Disconnect.
        Label _statusLabel = null!;
        Button _connectButton = null!;

        // MCP-server point content.
        Label _agentLabel = null!;

        // Custom-mode server-URL + auth.
        VBoxContainer _customHostRow = null!;
        LineEdit _hostField = null!;
        Label _overrideNote = null!;

        // Custom-mode authorization segmented (none|required) + masked token + Generate.
        Control _authSegmented = null!;
        VBoxContainer _tokenRow = null!;
        LineEdit _tokenField = null!;
        Button _generateTokenButton = null!;
        static readonly IReadOnlyList<string> AuthOptions = new[] { AuthLabelNone, AuthLabelRequired };
        const string AuthLabelNone = "none";
        const string AuthLabelRequired = "required";

        // Cloud-mode auth section (device-code flow): masked token + Authorize/Revoke + status.
        VBoxContainer _cloudAuthRow = null!;
        LineEdit _cloudTokenField = null!;
        Button _authorizeButton = null!;
        Button _revokeButton = null!;
        Label _cloudAuthStatus = null!;

        // The in-flight device-auth flow (null when none has run). Recreated per Authorize click.
        GodotDeviceAuthFlow? _deviceAuthFlow;

        // Local-server hosting (Custom mode): the Start/Stop button on the MCP-server timeline point, the
        // "Local server: …" status line, and the manager that downloads + runs the version-matched
        // godot-mcp-server binary. The server circle (_timelineServerCircle) reflects this LOCAL server's
        // lifecycle (Stopped/Starting/Running/Stopping) — the connection's own hub state is shown by the
        // Godot circle. This is the #1 "server-less client" carve-out reversal: the plugin can now HOST its
        // own server, not only connect to an external/cloud one.
        readonly GodotMcpServerManager _serverManager;
        VBoxContainer _localServerRow = null!;
        Label _serverStatusLabel = null!;
        Button _serverStartStopButton = null!;

        // The last server status the panel rendered, so re-seeds/re-applies are idempotent and quiet.
        GodotMcpServerStatus? _renderedServerStatus;

        // The last status the panel actually RENDERED, so the periodic re-sync only re-applies (and traces)
        // when the live status has drifted from what is on screen — keeping the per-tick check cheap and quiet.
        ConnectionStatus? _renderedStatus;

        // Accumulated frame delta for the periodic re-sync. Reset each time it crosses the interval so the
        // re-sync runs at a steady ~ResyncIntervalSeconds cadence regardless of frame rate.
        double _resyncAccumulator;

        // Registration of the re-sync into the main-thread dispatcher's per-tick hook (disposed in _ExitTree).
        // The dispatcher — NOT this dock Control — is the pump, because Godot skips a dock Control's own
        // _Process while its tab is hidden, whereas the dispatcher (a non-dock editor Node) always ticks.
        System.IDisposable? _resyncRegistration;

        /// <summary>Re-sync cadence: re-read + re-apply the live connection status this often (seconds).</summary>
        const double ResyncIntervalSeconds = 0.5;

        public ConnectionPanel(GodotMcpConnection connection)
        {
            _connection = connection;

            // The local-server manager downloads + runs the version-matched godot-mcp-server binary on
            // demand. Owned by the panel for the panel's lifetime; its StatusChanged is (un)subscribed in
            // _EnterTree/_ExitTree alongside the connection events (same #42/#56 reparent discipline). The
            // PluginVersion is the addon version the server binary must match EXACTLY.
            _serverManager = new GodotMcpServerManager(
                GodotMcpConnection.PluginVersion,
                GD.Print,
                GD.PushWarning,
                GD.PushError);

            Name = "ConnectionPanel";
            BuildUi();

            // Initial mode visibility (the cheap, idempotent part). The connection wiring — event
            // subscription, status seed, and re-sync registration — is done in _EnterTree so that a
            // dock-layout reload (which DETACHES then RE-ATTACHES this Control, firing _ExitTree → _EnterTree)
            // re-arms all of it. Doing it only in the ctor was the residual #42 bug: the editor reparents the
            // dock during "Loading docks" right as the handshake completes, the original wiring was torn down
            // by _ExitTree, and nothing re-seeded the re-attached panel — so it stayed on "Connecting…".
            ApplyModeVisibility(_connection.Config.ActiveMode);
        }

        /// <summary>
        /// (Re)arm the panel's connection wiring every time it enters the editor tree — including the
        /// re-attach the editor performs during dock-layout restore. Subscribes to the connection events,
        /// re-seeds the label from the LIVE status (the event only fires on CHANGE, so a status reached while
        /// detached must be pulled in here), and registers the dispatcher-pumped periodic re-sync. Pairs with
        /// <see cref="_ExitTree"/>, which tears all three down. Idempotent against duplicate subscription:
        /// the handlers are removed first.
        /// </summary>
        public override void _EnterTree()
        {
            // Remove-then-add so a re-entry never double-subscribes. The connection marshals these events onto
            // the editor main thread, so the handlers may touch Controls directly.
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connection.ConnectionStatusChanged += OnConnectionStatusChanged;
            _connection.AuthorizationRejected -= OnAuthorizationRejected;
            _connection.AuthorizationRejected += OnAuthorizationRejected;

            // Same remove-then-add discipline for the local-server manager's status stream (#42/#56): the
            // manager outlives the panel's tree membership, so a status reached while the panel was detached
            // (e.g. the ~5s startup verification completing during a dock reparent) is pulled in by the
            // re-seed below. The manager marshals its raises onto the editor main thread, so the handler may
            // touch Controls directly.
            _serverManager.StatusChanged -= OnServerStatusChanged;
            _serverManager.StatusChanged += OnServerStatusChanged;

            // Re-seed from the LIVE status: a status reached while the panel was detached (e.g. Connected
            // arriving during the dock reparent) is pulled onto the label here, since the change event was
            // missed. This is the load-bearing #42 fix — the panel ALWAYS converges to the real status on
            // (re)entry, independent of event-delivery timing.
            ApplyStatus(_connection.ConnectionStatus);
            ApplyServerStatus(_serverManager.Status);
            ApplyModeVisibility(_connection.Config.ActiveMode);

            // Belt-and-suspenders convergence: register a per-frame re-sync into the main-thread dispatcher's
            // tick hook. Every ResyncIntervalSeconds it re-reads the LIVE connection status off the connection
            // (NOT off the event) and re-applies it if the label has drifted. Reaches the real status within
            // ~0.5s even if a push was lost to the off-thread marshalling / de-dup boundary, and covers a
            // Reconnect settling on the new connection's status. The dispatcher pumps it (not this Control's
            // own _Process), so it ticks even when the dock tab is hidden.
            _resyncAccumulator = 0.0;
            _resyncRegistration?.Dispose();
            _resyncRegistration = MainThreadDispatcher.RegisterProcess(OnResyncTick);
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 8);

            // --- Header row: "Connection" (20px bold) + right-aligned Custom|Cloud segmented ---
            var headerRow = new HBoxContainer { Name = "HeaderRow" };
            AddChild(headerRow);

            var headerLabel = new Label { Name = "HeaderLabel", Text = "Connection" };
            DockStyle.ApplyHeader(headerLabel);
            headerRow.AddChild(headerLabel);

            headerRow.AddChild(new Control { Name = "HeaderSpacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });

            _modeSegmented = DockStyle.SegmentedControl(
                "ModeSegmented",
                ModeOptions,
                SegmentedControlModel.IndexOf(ModeOptions, ModeLabelForMode(_connection.Config.ConnectionMode)),
                OnModeSegmentSelected);
            headerRow.AddChild(_modeSegmented);

            // --- Alert panels (shown/hidden per ConnectionPanelView rules) ---
            _authRequiredAlert = DockStyle.AlertPanel(
                "AuthRequiredAlert",
                ConnectionPanelView.AuthorizationRequiredTitle,
                ConnectionPanelView.AuthorizationRequiredMessage,
                ConnectionPanelView.AuthorizeButtonText,
                OnAuthorizeButtonPressed);
            AddChild(_authRequiredAlert);

            _connectionRequiredAlert = DockStyle.AlertPanel(
                "ConnectionRequiredAlert",
                ConnectionPanelView.ConnectionRequiredTitle,
                ConnectionPanelView.ConnectionRequiredMessage,
                ConnectionPanelView.ButtonTextConnect,
                () => _connection.Connect());
            AddChild(_connectionRequiredAlert);

            // --- Vertical timeline: Godot -> MCP server -> AI agent ---
            var timeline = new VBoxContainer { Name = "Timeline" };
            timeline.AddThemeConstantOverride("separation", 0);
            AddChild(timeline);

            // Point 1 — Godot: label + right-aligned Connect/Disconnect.
            _timelineGodotCircle = DockStyle.TimelineCircle("GodotCircle", ConnectionPanelView.TimelinePointState.Disconnected);
            _statusLabel = new Label { Name = "GodotStatus" };
            DockStyle.ApplySubLabel(_statusLabel);

            _connectButton = new Button { Name = "ConnectButton" };
            _connectButton.Pressed += OnConnectButtonPressed;

            var godotContent = new HBoxContainer { Name = "GodotContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            godotContent.AddChild(DockStyle.TimelineLabel("GodotLabel", "Godot"));
            godotContent.AddChild(_statusLabel);
            godotContent.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            godotContent.AddChild(_connectButton);
            timeline.AddChild(MakeTimelinePoint(_timelineGodotCircle, godotContent, isLast: false));

            // Point 2 — MCP server: a frame-group card with the server URL / cloud auth + authorization rows.
            _timelineServerCircle = DockStyle.TimelineCircle("ServerCircle", ConnectionPanelView.TimelinePointState.Disconnected);
            var serverContent = new VBoxContainer { Name = "ServerContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            serverContent.AddThemeConstantOverride("separation", 4);
            serverContent.AddChild(DockStyle.TimelineLabel("ServerLabel", "MCP server"));
            BuildServerCard(serverContent);
            timeline.AddChild(MakeTimelinePoint(_timelineServerCircle, serverContent, isLast: false));

            // Point 3 — AI agent: circle + label, LAST point (no connecting line below).
            _timelineAgentCircle = DockStyle.TimelineCircle("AgentCircle", ConnectionPanelView.TimelinePointState.Disconnected);
            _agentLabel = new Label { Name = "AgentLabel", Text = ConnectionPanelView.AgentLabel(null, null) };
            DockStyle.ApplySubLabel(_agentLabel);
            var agentContent = new HBoxContainer { Name = "AgentContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            agentContent.AddChild(_agentLabel);
            timeline.AddChild(MakeTimelinePoint(_timelineAgentCircle, agentContent, isLast: true));
        }

        /// <summary>
        /// Build the MCP-server point's frame-group card content: the Custom-mode server-URL row + override
        /// note, the Cloud-mode device-auth row, and the Authorization (none|required) segmented + masked
        /// token field. These are reparented INTO a styled card by <see cref="DockStyle.Card"/>.
        /// </summary>
        void BuildServerCard(VBoxContainer parent)
        {
            var card = new VBoxContainer { Name = "ServerCardContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddThemeConstantOverride("separation", 4);

            // --- Custom-mode server-URL row (shown only in Custom mode) ---
            _customHostRow = new VBoxContainer { Name = "CustomHostRow" };
            _customHostRow.AddThemeConstantOverride("separation", 4);
            card.AddChild(_customHostRow);

            _customHostRow.AddChild(new Label { Name = "HostLabel", Text = "Server URL" });

            _hostField = new LineEdit
            {
                Name = "HostField",
                PlaceholderText = GodotMcpConfig.DefaultCustomHost,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            DockStyle.ApplyInput(_hostField);
            // Commit on Enter and on focus-out (mirrors the Unity reference's FocusOut commit).
            _hostField.TextSubmitted += OnHostSubmitted;
            _hostField.FocusExited += OnHostFocusExited;
            _customHostRow.AddChild(_hostField);

            // --- Authorization (Custom mode only): none | required (segmented) ---
            var authLine = new HBoxContainer { Name = "AuthLine" };
            _customHostRow.AddChild(authLine);

            authLine.AddChild(new Label { Name = "AuthLabel", Text = "Authorization Token" });
            authLine.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

            _authSegmented = DockStyle.SegmentedControl(
                "AuthSegmented",
                AuthOptions,
                SegmentedControlModel.IndexOf(AuthOptions, AuthLabelForOption(_connection.Config.AuthOption)),
                OnAuthSegmentSelected);
            authLine.AddChild(_authSegmented);

            // --- Token row (shown only when Authorization == required): masked field + Generate ---
            _tokenRow = new VBoxContainer { Name = "TokenRow" };
            _customHostRow.AddChild(_tokenRow);

            var tokenLine = new HBoxContainer { Name = "TokenLine" };
            _tokenRow.AddChild(tokenLine);

            _tokenField = new LineEdit
            {
                Name = "TokenField",
                // Masked + read-only: the token is never shown in clear text and is only changed via
                // Generate (never typed/logged). Mirrors the Unity reference's password token field.
                Secret = true,
                Editable = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            tokenLine.AddChild(_tokenField);

            _generateTokenButton = new Button { Name = "GenerateTokenButton", Text = "New" };
            _generateTokenButton.Pressed += OnGenerateTokenPressed;
            tokenLine.AddChild(_generateTokenButton);

            // --- Local-server hosting row (Custom mode only): Start/Stop the version-matched server binary ---
            // The plugin can HOST its own server here (download-if-needed + launch), not just connect to an
            // external/cloud one. Hidden in Cloud mode (no local server is launched against the cloud host).
            _localServerRow = new VBoxContainer { Name = "LocalServerRow" };
            _localServerRow.AddThemeConstantOverride("separation", 4);
            _customHostRow.AddChild(_localServerRow);

            var serverLine = new HBoxContainer { Name = "LocalServerLine", SizeFlagsHorizontal = SizeFlags.ExpandFill };

            _serverStatusLabel = new Label { Name = "LocalServerStatus" };
            DockStyle.ApplySubLabel(_serverStatusLabel);
            serverLine.AddChild(_serverStatusLabel);

            serverLine.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

            _serverStartStopButton = new Button { Name = "LocalServerStartStopButton" };
            _serverStartStopButton.Pressed += OnServerStartStopPressed;
            serverLine.AddChild(_serverStartStopButton);

            _localServerRow.AddChild(serverLine);

            // --- Cloud-mode auth section (shown only in Cloud mode): device-code login ---
            _cloudAuthRow = new VBoxContainer { Name = "CloudAuthRow" };
            _cloudAuthRow.AddThemeConstantOverride("separation", 4);
            card.AddChild(_cloudAuthRow);

            _cloudAuthRow.AddChild(new Label { Name = "CloudTokenLabel", Text = "Cloud Token" });

            var cloudTokenLine = new HBoxContainer { Name = "CloudTokenLine" };
            _cloudAuthRow.AddChild(cloudTokenLine);

            _cloudTokenField = new LineEdit
            {
                Name = "CloudTokenField",
                // Masked + read-only: the access token is never shown in clear text and is only ever set by
                // the device-auth flow (never typed/logged). Mirrors the Custom-mode token field.
                Secret = true,
                Editable = false,
                PlaceholderText = ConnectionPanelView.CloudTokenPlaceholder,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            cloudTokenLine.AddChild(_cloudTokenField);

            _authorizeButton = new Button { Name = "AuthorizeButton", Text = ConnectionPanelView.AuthorizeButtonText };
            _authorizeButton.Pressed += OnAuthorizeButtonPressed;
            cloudTokenLine.AddChild(_authorizeButton);

            _revokeButton = new Button { Name = "RevokeButton", Text = "Revoke" };
            _revokeButton.Pressed += OnRevokeButtonPressed;
            cloudTokenLine.AddChild(_revokeButton);

            _cloudAuthStatus = new Label
            {
                Name = "CloudAuthStatus",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _cloudAuthRow.AddChild(_cloudAuthStatus);

            // --- Env/.env override note (shown when a process env / .env value forces mode or host) ---
            _overrideNote = new Label
            {
                Name = "OverrideNote",
                Text = "Overridden by environment (GODOT_MCP_*) — UI changes won't take effect.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _overrideNote.AddThemeColorOverride("font_color", new Color(0.92f, 0.74f, 0.20f));
            card.AddChild(_overrideNote);

            parent.AddChild(DockStyle.Card(card, "Server"));
        }

        /// <summary>
        /// Compose one timeline point: a 20px indicator column (the status circle, and below it a 2px
        /// connecting line that ExpandFills to span the gap to the next point — hidden on the LAST point) next
        /// to the point's <paramref name="content"/>. Mirrors Unity-MCP's timeline row.
        /// </summary>
        static HBoxContainer MakeTimelinePoint(Panel circle, Control content, bool isLast)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);

            // Indicator column: circle on top, connecting line filling the rest (hidden on the last point).
            var indicator = new VBoxContainer
            {
                Name = "Indicator",
                CustomMinimumSize = new Vector2(DockTheme.TimelineIndicatorWidth, 0)
            };
            indicator.AddThemeConstantOverride("separation", 2);
            indicator.AddChild(circle);

            var line = DockStyle.TimelineLine();
            line.Visible = !isLast;
            indicator.AddChild(line);

            row.AddChild(indicator);
            row.AddChild(content);
            return row;
        }

        static string ModeLabelForMode(GodotMcpConnectionMode mode) =>
            mode == GodotMcpConnectionMode.Cloud ? ModeLabelCloud : ModeLabelCustom;

        static string AuthLabelForOption(GodotMcpAuthOption option) =>
            option == GodotMcpAuthOption.Required ? AuthLabelRequired : AuthLabelNone;

        void OnConnectionStatusChanged(ConnectionStatus status) => ApplyStatus(status);

        /// <summary>
        /// Push a <see cref="ConnectionStatus"/> into the Godot status label, the Connect button, the Godot
        /// timeline circle, and the alert-panel visibility. All derived presentation comes from
        /// <see cref="ConnectionPanelView"/>. The "Godot" circle tracks the hub connection state; the
        /// "MCP server" circle is driven SEPARATELY by the LOCAL server's lifecycle (see
        /// <see cref="ApplyServerStatus"/>) now that the plugin can host its own server; the "AI agent"
        /// circle stays neutral (no live agent-info channel — the label reads "AI agent (connects on demand)").
        /// </summary>
        void ApplyStatus(ConnectionStatus status)
        {
            var pointState = ConnectionPanelView.PointState(status);

            _statusLabel.Text = ConnectionPanelView.StatusLabel(status);
            _connectButton.Text = ConnectionPanelView.ButtonText(status);
            _connectButton.Disabled = ConnectionPanelView.ButtonDisabled(status);
            // Primary (cyan) when the click connects; secondary (gray) when it disconnects.
            if (status == ConnectionStatus.Connected)
                DockStyle.ApplySecondaryButton(_connectButton);
            else
                DockStyle.ApplyPrimaryButton(_connectButton);

            DockStyle.ApplyTimelineCircle(_timelineGodotCircle, pointState);

            _renderedStatus = status;
            ApplyAlertVisibility(status);

            // Trace the actual render so a Trace smoke run shows the terminal Connected reaching the label
            // (pairs with the connection's "status: X -> Y" push trace — see GodotMcpConnection.PublishStatus).
            _connection.LogStatusTrace($"[Godot-MCP] ApplyStatus rendered status: {status}");
        }

        /// <summary>
        /// Show/hide the two amber alert panels per the pure-managed
        /// <see cref="ConnectionPanelView.ShowAuthorizationRequired"/> /
        /// <see cref="ConnectionPanelView.ShowConnectionRequired"/> rules, driven by the live mode, whether a
        /// cloud token is stored, and the current status.
        /// </summary>
        void ApplyAlertVisibility(ConnectionStatus status)
        {
            var isCloud = _connection.Config.ActiveMode == GodotMcpConnectionMode.Cloud;
            var hasCloudToken = !string.IsNullOrEmpty(_connection.Config.CloudToken);

            _authRequiredAlert.Visible = ConnectionPanelView.ShowAuthorizationRequired(isCloud, hasCloudToken);
            _connectionRequiredAlert.Visible = ConnectionPanelView.ShowConnectionRequired(isCloud, hasCloudToken, status);
        }

        void OnServerStatusChanged(GodotMcpServerStatus status) => ApplyServerStatus(status);

        /// <summary>
        /// Render the LOCAL server's lifecycle onto the MCP-server timeline point: the "Local server: …"
        /// status line, the Start/Stop button (text + disabled state from the pure-managed
        /// <see cref="GodotMcpServerView"/>), and the server timeline circle (filled green Running/External,
        /// green ring while Starting/Stopping, orange disc Stopped). Idempotent + quiet: a re-apply of the
        /// already-rendered status is harmless. Runs on the editor main thread (the manager marshals its
        /// raises there).
        /// </summary>
        void ApplyServerStatus(GodotMcpServerStatus status)
        {
            _serverStatusLabel.Text = GodotMcpServerView.ServerStatusLabel(status);
            _serverStartStopButton.Text = GodotMcpServerView.ServerButtonText(status);
            _serverStartStopButton.Disabled = GodotMcpServerView.ServerButtonDisabled(status);

            // Primary (cyan) when the click starts the server; secondary (gray) when it stops it.
            if (status == GodotMcpServerStatus.Running)
                DockStyle.ApplySecondaryButton(_serverStartStopButton);
            else
                DockStyle.ApplyPrimaryButton(_serverStartStopButton);

            DockStyle.ApplyTimelineCircle(_timelineServerCircle, GodotMcpServerView.ServerPointState(status));
            _renderedServerStatus = status;
        }

        /// <summary>
        /// Handle a click on the local-server Start/Stop button. When stopped, downloads the version-matched
        /// binary if needed then launches it with <c>client-transport=streamableHttp</c> on the port parsed
        /// from the configured Custom host URL, passing the bearer token only when auth is required (the
        /// token is never logged). When running, terminates it. The download/launch is fire-and-forget; the
        /// status circle + button converge via the manager's <see cref="GodotMcpServerManager.StatusChanged"/>
        /// stream. The button is disabled by <see cref="ApplyServerStatus"/> during transient states, so a
        /// double-click cannot race a start/stop.
        /// </summary>
        void OnServerStartStopPressed()
        {
            if (_serverManager.Status == GodotMcpServerStatus.Running)
            {
                _serverManager.StopServer();
                return;
            }

            var port = GodotMcpServerView.ResolveServerPort(
                _connection.Config.ResolveCustomHost(),
                com.IvanMurzak.McpPlugin.Common.Consts.Hub.DefaultPort);
            var timeoutMs = com.IvanMurzak.McpPlugin.Common.Consts.Hub.DefaultTimeoutMs;
            var authRequired = _connection.Config.ActiveAuthOption == GodotMcpAuthOption.Required;
            var token = _connection.Config.ResolveCustomToken();

            // Fire-and-forget: StartServerAsync downloads-if-needed then launches; status changes drive the UI.
            _ = _serverManager.StartServerAsync(port, timeoutMs, authRequired, token);
        }

        /// <summary>
        /// Per-frame tick fired by the main-thread dispatcher. Accumulates <paramref name="delta"/> and runs
        /// the cheap <see cref="SyncFromConnection"/> drift check once every <see cref="ResyncIntervalSeconds"/>s.
        /// Driven by the dispatcher (a non-dock editor Node that always ticks) rather than this Control's own
        /// <see cref="Node._Process"/>, which Godot skips while the dock tab is hidden — see the registration
        /// in <see cref="_EnterTree"/>.
        /// </summary>
        void OnResyncTick(double delta)
        {
            _resyncAccumulator += delta;
            if (_resyncAccumulator < ResyncIntervalSeconds)
                return;

            _resyncAccumulator = 0.0;
            SyncFromConnection();
        }

        /// <summary>
        /// Re-read the LIVE connection status DIRECTLY off <see cref="GodotMcpConnection.ConnectionStatus"/>
        /// (bypassing the <see cref="GodotMcpConnection.ConnectionStatusChanged"/> event) and re-apply it
        /// when it differs from what the panel last rendered. Driven by the periodic dispatcher re-sync so the
        /// label converges to the real status within ~<see cref="ResyncIntervalSeconds"/>s even if a status push
        /// was lost to the off-thread marshalling / de-dup boundary OR to a dock-layout reload that
        /// re-instantiated/detached the panel mid-handshake (the root cause of issue #42), or a Reconnect
        /// rebuilt the connection. Cheap and quiet: when the live status already matches the rendered one this
        /// is a single enum comparison and returns without touching any Control. Runs on the editor main thread.
        /// </summary>
        void SyncFromConnection()
        {
            var live = _connection.ConnectionStatus;
            if (_renderedStatus == live)
                return;

            _connection.LogStatusTrace(
                $"[Godot-MCP] re-sync: label '{_renderedStatus}' drifted from live '{live}' — re-applying.");
            ApplyStatus(live);
        }

        void OnConnectButtonPressed()
        {
            if (_connection.ConnectionStatus == ConnectionStatus.Connected)
                _connection.Disconnect();
            else
                _connection.Connect();
        }

        /// <summary>
        /// Handle a Custom|Cloud segment click: persist the chosen PERSISTED mode and reconnect only when the
        /// change actually moves the LIVE active mode (under an env override ActiveMode is pinned, so a
        /// persisted-only edit must not tear down the current connection). Re-renders the segmented selection
        /// and the mode-dependent sections afterward.
        /// </summary>
        void OnModeSegmentSelected(int index)
        {
            var mode = index == SegmentedControlModel.IndexOf(ModeOptions, ModeLabelCloud)
                ? GodotMcpConnectionMode.Cloud
                : GodotMcpConnectionMode.Custom;

            if (_connection.Config.ConnectionMode == mode)
            {
                ApplyModeVisibility(_connection.Config.ActiveMode);
                return;
            }

            var liveModeBefore = _connection.Config.ActiveMode;
            _connection.Config.ConnectionMode = mode;
            _connection.Save();
            ApplyModeVisibility(_connection.Config.ActiveMode);

            if (_connection.Config.ActiveMode != liveModeBefore)
                _connection.Reconnect();
        }

        /// <summary>
        /// Persist the chosen Custom-mode authorization option (none/required) and reconnect so the
        /// bearer-token routing takes effect. When set to <c>required</c> with no token yet, generate one
        /// so the connection has a credential to send. Persists even under an env override (the override
        /// note explains the env value wins live); only reconnects when the live mode is Custom.
        /// </summary>
        void OnAuthSegmentSelected(int index)
        {
            var authOption = index == SegmentedControlModel.IndexOf(AuthOptions, AuthLabelRequired)
                ? GodotMcpAuthOption.Required
                : GodotMcpAuthOption.None;

            if (_connection.Config.AuthOption == authOption)
            {
                ApplyAuthVisibility();
                return;
            }

            _connection.Config.AuthOption = authOption;

            // When switching to required without a stored token, mint one so the connection is usable.
            if (authOption == GodotMcpAuthOption.Required &&
                string.IsNullOrEmpty(_connection.Config.CustomToken))
            {
                _connection.Config.CustomToken = GodotMcpTokenGenerator.Generate();
            }

            _connection.Save();
            ApplyAuthVisibility();

            // Only a live Custom connection is affected by the auth/token routing.
            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Custom)
                _connection.Reconnect();
        }

        /// <summary>
        /// Generate a fresh Custom-mode token, persist it, and reconnect so the new bearer is used. The
        /// token is never logged and is shown only as a masked field. Generating implies the user wants
        /// auth, so this also flips <see cref="GodotMcpAuthOption"/> to <c>Required</c> if it was off.
        /// </summary>
        void OnGenerateTokenPressed()
        {
            _connection.Config.CustomToken = GodotMcpTokenGenerator.Generate();
            if (_connection.Config.AuthOption == GodotMcpAuthOption.None)
                _connection.Config.AuthOption = GodotMcpAuthOption.Required;

            _connection.Save();
            ApplyAuthVisibility();

            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Custom)
                _connection.Reconnect();
        }

        void OnHostSubmitted(string text) => CommitHost(text);

        void OnHostFocusExited() => CommitHost(_hostField.Text);

        /// <summary>
        /// Validate + persist a Custom-mode server URL, then reconnect. Invalid input (not an absolute
        /// http/https URL) is rejected: the field is reverted to the configured host and no write/reconnect
        /// happens. A no-op edit (unchanged value) is ignored so a focus-out without a change does not
        /// needlessly tear down a live connection.
        /// </summary>
        void CommitHost(string text)
        {
            if (!ConnectionPanelView.IsValidServerUrl(text))
            {
                // Reject: restore the displayed value to the current configured host.
                _hostField.Text = _connection.Config.CustomHost;
                GD.PushWarning($"[Godot-MCP] ignored invalid server URL: '{text}' (must be an absolute http/https URL).");
                return;
            }

            var normalized = text.Trim().Trim('"').TrimEnd('/');
            if (_connection.Config.CustomHost == normalized)
                return;

            _connection.Config.CustomHost = normalized;
            _connection.Save();

            // Only a Custom-mode host change warrants a reconnect; in Cloud mode the field is hidden.
            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Custom)
                _connection.Reconnect();
        }

        /// <summary>
        /// Drive the editable Custom section off the PERSISTED <see cref="GodotMcpConfig.ConnectionMode"/>
        /// (so editing the segmented control / URL / auth always targets the layer the user can change), while
        /// the override note surfaces when an env/.env value is forcing the LIVE active mode away from that
        /// persisted choice. The segmented control stays interactive even when overridden — a persisted edit
        /// "does something" (it takes effect once the override is gone) and does NOT corrupt precedence, since
        /// env/.env is read live by the config resolvers. The host field shows the EFFECTIVE custom host (env
        /// override visible) for transparency. Re-renders the mode segmented selection + alert visibility too.
        /// </summary>
        void ApplyModeVisibility(GodotMcpConnectionMode activeMode)
        {
            // Editable controls follow the PERSISTED mode (what the user is editing).
            var persistedMode = _connection.Config.ConnectionMode;
            var persistedCustom = persistedMode == GodotMcpConnectionMode.Custom;

            // Re-render the mode segmented to the persisted mode.
            DockStyle.SetSegmentedSelection(
                _modeSegmented,
                SegmentedControlModel.IndexOf(ModeOptions, ModeLabelForMode(persistedMode)));

            _customHostRow.Visible = persistedCustom;
            _cloudAuthRow.Visible = !persistedCustom;

            if (persistedCustom)
            {
                // Show the EFFECTIVE custom host (env GODOT_MCP_HOST wins over the persisted value).
                _hostField.Text = _connection.Config.ResolveCustomHost();
                ApplyAuthVisibility();
            }
            else
            {
                ApplyCloudAuthState();
            }

            // The active mode differs from the persisted mode only when an env/.env override forced it.
            var overridden = activeMode != persistedMode;
            _overrideNote.Visible = overridden;

            _hostField.Editable = true;
            ApplyAlertVisibility(_connection.ConnectionStatus);
        }

        /// <summary>
        /// Render the Custom-mode authorization controls from the persisted config: the auth segmented
        /// reflects <see cref="GodotMcpConfig.AuthOption"/>, the masked token row is shown only when
        /// <c>Required</c>, and the field carries the stored Custom token (masked). The token is never
        /// shown in clear text or logged. Always reads/writes the PERSISTED layer (env auth override is
        /// surfaced by the override note, not by disabling these controls).
        /// </summary>
        void ApplyAuthVisibility()
        {
            var required = _connection.Config.AuthOption == GodotMcpAuthOption.Required;
            DockStyle.SetSegmentedSelection(
                _authSegmented,
                SegmentedControlModel.IndexOf(AuthOptions, required ? AuthLabelRequired : AuthLabelNone));
            _tokenRow.Visible = required;
            // Masked field carries the stored token; only meaningful when required.
            _tokenField.Text = required ? (_connection.Config.CustomToken ?? string.Empty) : string.Empty;
        }

        /// <summary>
        /// Render the Cloud-mode auth controls from the persisted <see cref="GodotMcpConfig.CloudToken"/>:
        /// the masked field carries the stored token (or shows the placeholder via empty text), and the
        /// Revoke button is visible only when a token is stored. Called on Cloud-mode entry and after every
        /// token change. The token is never shown in clear text or logged.
        /// </summary>
        void ApplyCloudAuthState()
        {
            var token = _connection.Config.CloudToken;
            var hasToken = !string.IsNullOrEmpty(token);

            // Empty text → the masked LineEdit shows its PlaceholderText ("Token — press Authorize").
            _cloudTokenField.Text = hasToken ? token! : string.Empty;
            _revokeButton.Visible = hasToken;
        }

        /// <summary>
        /// Start (or cancel) the device-code authorization flow. While running, the button shows "Cancel"
        /// and a click cancels the in-flight flow. The flow runs on a background task; every state change is
        /// marshalled onto the editor main thread before touching any <see cref="Control"/>. On
        /// <see cref="GodotDeviceAuthFlowState.WaitingForUser"/> the verification URL is opened in the
        /// browser; on <see cref="GodotDeviceAuthFlowState.Authorized"/> the returned token is persisted and
        /// the connection reconnects. The token is never logged.
        /// </summary>
        void OnAuthorizeButtonPressed()
        {
            // A click while a flow is running means "Cancel".
            if (_deviceAuthFlow != null && GodotDeviceAuthFlow.IsRunning(_deviceAuthFlow.State))
            {
                _deviceAuthFlow.Cancel();
                return;
            }

            _deviceAuthFlow?.Cancel();
            var flow = new GodotDeviceAuthFlow();
            _deviceAuthFlow = flow;

            flow.OnStateChanged += state =>
            {
                // OnStateChanged fires on the flow's background task thread; hop to the editor main thread
                // before touching Controls. A missing dispatcher (between editor reloads) degrades to a
                // direct call rather than throwing.
                if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                    MainThreadDispatcher.Enqueue(() => OnAuthFlowStateChanged(flow, state));
                else
                    OnAuthFlowStateChanged(flow, state);
            };

            // Fire-and-forget; the state-change handler drives the status/button/browser UI, and the awaited
            // result persists the token (the token NEVER lives on the flow instance — it only flows out as
            // StartAsync's return value, so config writes stay on the main thread via the dispatcher).
            _ = RunAuthFlowAsync(flow);
        }

        async Task RunAuthFlowAsync(GodotDeviceAuthFlow flow)
        {
            var token = await flow.StartAsync(_connection.CloudBaseUrl, "Godot Editor");
            if (string.IsNullOrEmpty(token))
                return; // Non-Authorized terminal state: nothing to persist (UI already reflects it).

            // Persist + reconnect on the editor main thread (the awaited continuation may run off-thread).
            if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                MainThreadDispatcher.Enqueue(() => PersistAuthorizedToken(flow, token!));
            else
                PersistAuthorizedToken(flow, token!);
        }

        /// <summary>
        /// Apply one device-auth flow state transition to the UI (status line, button label, browser-open).
        /// MUST run on the editor main thread. Ignores events from a stale flow (a newer Authorize click
        /// replaced <see cref="_deviceAuthFlow"/>). Token persistence happens in
        /// <see cref="PersistAuthorizedToken"/> off the awaited result, not here.
        /// </summary>
        void OnAuthFlowStateChanged(GodotDeviceAuthFlow flow, GodotDeviceAuthFlowState state)
        {
            // Drop late events from a flow that has been superseded by a newer one.
            if (!ReferenceEquals(_deviceAuthFlow, flow))
                return;

            // Status line. UserCode is safe to show; the access token never reaches this string.
            _cloudAuthStatus.Text = ConnectionPanelView.CloudAuthStatusMessage(state, flow.UserCode, flow.ErrorMessage);
            _authorizeButton.Text = ConnectionPanelView.CloudAuthButtonText(state);

            // Open the verification URL so the user can approve in the browser.
            if (state == GodotDeviceAuthFlowState.WaitingForUser && !string.IsNullOrEmpty(flow.VerificationUriComplete))
                OS.ShellOpen(flow.VerificationUriComplete);
        }

        /// <summary>
        /// Persist the cloud token produced by an Authorized flow, refresh the masked field, and reconnect.
        /// MUST run on the editor main thread. Ignores a stale flow. The token is written straight to config
        /// and never logged.
        /// </summary>
        void PersistAuthorizedToken(GodotDeviceAuthFlow flow, string token)
        {
            if (!ReferenceEquals(_deviceAuthFlow, flow))
                return;

            _connection.Config.CloudToken = token;
            _connection.Save();
            ApplyCloudAuthState();
            ApplyAlertVisibility(_connection.ConnectionStatus);

            // Reconnect so the new bearer is used — only meaningful when the live mode is Cloud.
            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Cloud)
                _connection.Reconnect();
        }

        /// <summary>
        /// Revoke the stored cloud token: clear it, persist, revert the UI to the Authorize state, and (if
        /// the live mode is Cloud) disconnect so the now-unauthenticated session does not linger.
        /// </summary>
        void OnRevokeButtonPressed()
        {
            _connection.Config.CloudToken = null;
            _connection.Save();
            _cloudAuthStatus.Text = "Token revoked.";
            ApplyCloudAuthState();
            ApplyAlertVisibility(_connection.ConnectionStatus);

            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Cloud)
                _connection.Disconnect();
        }

        /// <summary>
        /// Handle a server-side authorization rejection (the connection's
        /// <see cref="GodotMcpConnection.AuthorizationRejected"/> fired, already on the main thread): drop
        /// the rejected cloud token, persist, revert the UI to Authorize, and warn the user WITHOUT logging
        /// the token (it carries no payload through this event anyway).
        /// </summary>
        void OnAuthorizationRejected()
        {
            // Only relevant to Cloud mode — a Custom-mode rejection is the Custom token's concern.
            if (_connection.Config.ActiveMode != GodotMcpConnectionMode.Cloud)
                return;

            _connection.Config.CloudToken = null;
            _connection.Save();
            _cloudAuthStatus.Text = "Authorization rejected by server — press Authorize.";
            ApplyCloudAuthState();
            ApplyAlertVisibility(_connection.ConnectionStatus);

            GD.PushWarning("[Godot-MCP] server rejected the authorization token; cleared — press Authorize.");
        }

        /// <summary>
        /// Re-render the panel from current connection state. Forwarded from <see cref="GodotMcpDock.Refresh"/>.
        /// Safe to call repeatedly.
        /// </summary>
        public void Refresh()
        {
            ApplyModeVisibility(_connection.Config.ActiveMode);
            ApplyStatus(_connection.ConnectionStatus);
            ApplyServerStatus(_serverManager.Status);
        }

        public override void _ExitTree()
        {
            // Unsubscribe so a freed panel does not receive a late main-thread push.
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connection.AuthorizationRejected -= OnAuthorizationRejected;
            _serverManager.StatusChanged -= OnServerStatusChanged;

            // Unregister the periodic re-sync so the dispatcher no longer ticks into a freed panel.
            _resyncRegistration?.Dispose();
            _resyncRegistration = null;

            // Cancel any in-flight device-auth flow so its background poll loop stops touching a freed panel.
            _deviceAuthFlow?.Cancel();
            _deviceAuthFlow = null;

            // NOTE: the local-server manager is NOT disposed here — _ExitTree fires on every dock reparent
            // (#42/#56), and disposing would kill a running server on a benign layout reload. The manager is
            // disposed only when the panel is permanently freed (NotificationPredelete below), which stops
            // any hosted server so we never leak a process.
        }

        /// <summary>
        /// On permanent free (plugin disabled / editor teardown — NOT a dock reparent, which is _ExitTree),
        /// dispose the local-server manager so any hosted server process is stopped and not orphaned.
        /// </summary>
        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
                _serverManager.Dispose();
        }
    }
}
#endif
