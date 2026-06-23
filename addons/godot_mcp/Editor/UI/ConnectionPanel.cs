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
using McpClientData = com.IvanMurzak.McpPlugin.Common.Model.McpClientData;

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

        /// <summary>
        /// Raised after the user changes any connection setting that affects the RESOLVED MCP-client URL or token
        /// (server URL, Custom/Cloud mode, auth option, generated/cloud token). The dock subscribes so the AI-agent
        /// section re-evaluates its config state and shows the "Reconfiguration Required" alert + the Reconfigure
        /// button when the written agent config no longer matches the new URL — mirroring Unity, where each
        /// configurator re-checks IsReconfigureNeeded() whenever the connection settings change.
        /// </summary>
        public event System.Action? ConfigChanged;

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

        // The MCP-server timeline point (circle + server card), captured so the WHOLE point can be hidden in
        // Cloud mode. The Unity gold reference shows NO "MCP server" point in Cloud — just the Godot connection
        // status + the AI-agent point (the cloud token/Authorize row lives above the timeline, under the header).
        // The MCP-server point (Server URL, Start, Transport, Authorization) is Custom-mode only.
        HBoxContainer _serverTimelinePoint = null!;

        // Godot point: the underlined "Godot" label (now carries the status — "Godot" / "Godot: connecting..." /
        // "Godot: connected") + a single Connect/Stop toggle button.
        PanelContainer _godotUnderline = null!;
        Button _connectButton = null!;

        // MCP-server point content.
        Label _agentLabel = null!;

        // AI-agent point: the muted summary suffix is _agentLabel above; this VBox holds the live per-agent rows
        // (one per connected MCP client / AI session), rebuilt by RefreshAgents from the connection's ActiveAgents.
        VBoxContainer _agentListContainer = null!;

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
        // "Local server: …" status line, and the manager that downloads + runs the pinned shared
        // gamedev-mcp-server binary. The server circle (_timelineServerCircle) reflects this LOCAL server's
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

        // DEV-ONLY status override (set by the dev-control bridge's inject endpoint). When non-null the
        // periodic re-sync (SyncFromConnection) is short-circuited so an injected status STICKS on screen
        // instead of being reverted to the live connection status within ~0.5s. Cleared by
        // DevClearStatusOverride, after which the panel re-converges to the real live status. This field is
        // ONLY ever written by the DevControlServer (env-gated, 127.0.0.1) — it has no effect in a shipped
        // addon (the server never starts unless GODOT_MCP_DEV_CONTROL=1).
        ConnectionStatus? _devStatusOverride;

        // DEV-ONLY connected-agent override (set by the dev-control bridge's inject endpoint). When non-null,
        // RefreshAgents renders THIS list instead of the live connection's ActiveAgents — so the smoke harness / a
        // terminal can paint a fake AI-agent session list onto the live dock without a real external MCP client.
        // Cleared by DevClearAgentsOverride. ONLY ever written by the env-gated, 127.0.0.1 DevControlServer.
        IReadOnlyList<McpClientData>? _devAgentsOverride;

        // Accumulated frame delta for the periodic re-sync. Reset each time it crosses the interval so the
        // re-sync runs at a steady ~ResyncIntervalSeconds cadence regardless of frame rate.
        double _resyncAccumulator;

        // Registration of the re-sync into the main-thread dispatcher's per-tick hook (disposed in _ExitTree).
        // The dispatcher — NOT this dock Control — is the pump, because Godot skips a dock Control's own
        // _Process while its tab is hidden, whereas the dispatcher (a non-dock editor Node) always ticks.
        System.IDisposable? _resyncRegistration;

        /// <summary>Re-sync cadence: re-read + re-apply the live connection status this often (seconds).</summary>
        const double ResyncIntervalSeconds = 0.5;

        /// <summary>
        /// Vertical offset (px) that drops a timeline status dot down so its CENTER lines up with the middle of the
        /// underlined point label next to it. Derived from the label/dot sizes — roughly
        /// (underlined-label line height − dot diameter) / 2 — so it tracks <see cref="DockTheme.FontSizeUnderlinedLabel"/>
        /// (a bigger label needs a larger drop). Tuned live against the dock.
        /// </summary>
        const int TimelineCircleTopOffset = 13;

        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live [Tool] script via its parameterless ctor, so a parameter-only
        /// class throws MissingMemberException ("does not define a parameterless constructor") and breaks the
        /// reload (it crashed the editor before this was added). The reloaded plugin re-adds a FRESH, wired
        /// dock (see GodotMcpPlugin's reload re-entry), so this re-instantiated shell is a discarded orphan —
        /// it only has to exist without faulting.
        /// </summary>
        public ConnectionPanel() { }

        public ConnectionPanel(GodotMcpConnection connection)
        {
            _connection = connection;

            // The local-server manager downloads + runs the shared gamedev-mcp-server binary on demand,
            // pinned to GodotMcpServerView.ServerVersion (NOT the addon version — the two diverge). Owned
            // by the panel for the panel's lifetime; its StatusChanged is (un)subscribed in
            // _EnterTree/_ExitTree alongside the connection events (same #42/#56 reparent discipline).
            _serverManager = new GodotMcpServerManager(
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

            // Same remove-then-add discipline for the connection's live AI-agent stream: an agent that connected
            // while the panel was detached (e.g. during a dock reparent) is pulled in by the RefreshAgents re-seed
            // below. AgentsUpdated is marshalled onto the editor main thread, so the handler may touch Controls.
            _connection.AgentsUpdated -= OnAgentsUpdated;
            _connection.AgentsUpdated += OnAgentsUpdated;

            // Re-seed from the LIVE status: a status reached while the panel was detached (e.g. Connected
            // arriving during the dock reparent) is pulled onto the label here, since the change event was
            // missed. This is the load-bearing #42 fix — the panel ALWAYS converges to the real status on
            // (re)entry, independent of event-delivery timing.
            ApplyStatus(_connection.ConnectionStatus);
            ApplyServerStatus(_serverManager.Status);
            ApplyModeVisibility(_connection.Config.ActiveMode);
            RefreshAgents();

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

            // --- Cloud-mode auth row (masked token + Revoke/Authorize), DIRECTLY under the header and ABOVE
            //     the timeline (Unity gold reference). Shown only in Cloud mode; in Cloud the MCP-server
            //     timeline point is hidden entirely, so this row must NOT live inside the server card. ---
            BuildCloudAuthRow();

            // --- Vertical timeline: Godot -> MCP server -> AI agent ---
            var timeline = new VBoxContainer { Name = "Timeline" };
            timeline.AddThemeConstantOverride("separation", 0);
            AddChild(timeline);

            // Point 1 — Godot: a single underlined label that CARRIES the status + a right-aligned Connect/Stop toggle.
            _timelineGodotCircle = DockStyle.TimelineCircle("GodotCircle", ConnectionPanelView.TimelinePointState.Disconnected);

            _connectButton = new Button { Name = "ConnectButton" };
            DockStyle.ConnectPressed(_connectButton, this, MethodName.OnConnectButtonPressed);

            var godotContent = new HBoxContainer { Name = "GodotContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            godotContent.AddThemeConstantOverride("separation", 8);
            // The underlined "Godot" label now folds in the status (ApplyStatus retitles it to
            // "Godot: connecting..." / "Godot: connected") — there is no longer a separate status suffix label.
            _godotUnderline = DockStyle.UnderlinedSubLabel("GodotLabel", ConnectionPanelView.GodotLineLabel(ConnectionStatus.Disconnected));
            godotContent.AddChild(_godotUnderline);
            godotContent.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            godotContent.AddChild(_connectButton);
            timeline.AddChild(MakeTimelinePoint(_timelineGodotCircle, godotContent, isLast: false, circleTopOffset: TimelineCircleTopOffset));

            // Point 2 — MCP server: a frame-group card with the server URL / cloud auth + authorization rows.
            _timelineServerCircle = DockStyle.TimelineCircle("ServerCircle", ConnectionPanelView.TimelinePointState.Disconnected);
            var serverContent = new VBoxContainer { Name = "ServerContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            serverContent.AddThemeConstantOverride("separation", 4);
            // The "MCP server" label now lives INSIDE the card (see BuildServerCard) so it's framed by the rounded
            // rectangle, like Unity's frame-mcp-server. The server circle is offset down to line up with it.
            BuildServerCard(serverContent);
            _serverTimelinePoint = MakeTimelinePoint(_timelineServerCircle, serverContent, isLast: false,
                circleTopOffset: DockTheme.CardMargin + DockTheme.CardContentPadding + TimelineCircleTopOffset);
            timeline.AddChild(_serverTimelinePoint);

            // Point 3 — AI agent: circle + a header row (underlined label + live session summary) over a list of
            // connected agents (Copilot / Claude / …), driven by the connection's AgentsUpdated. LAST point (no line).
            _timelineAgentCircle = DockStyle.TimelineCircle("AgentCircle", ConnectionPanelView.TimelinePointState.Disconnected);

            var agentContent = new VBoxContainer { Name = "AgentContent", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            agentContent.AddThemeConstantOverride("separation", 2);

            // Header row: underlined "AI agent" + the muted "(N connected)" / "(connects on demand)" summary.
            var agentHeader = new HBoxContainer { Name = "AgentHeader", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            agentHeader.AddThemeConstantOverride("separation", 6);
            agentHeader.Alignment = BoxContainer.AlignmentMode.Begin;
            agentHeader.AddChild(DockStyle.UnderlinedSubLabel("AgentLabel", "AI agent"));
            _agentLabel = new Label { Name = "AgentSuffix", Text = AgentSessionView.Summary(0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            DockStyle.ApplyDescription(_agentLabel);
            _agentLabel.AutowrapMode = TextServer.AutowrapMode.Off; // single line — never wrap to one char per line in the narrow row
            agentHeader.AddChild(_agentLabel);
            agentContent.AddChild(agentHeader);

            // Live connected-agent rows (one per active MCP client session). Rebuilt by RefreshAgents; hidden empty.
            _agentListContainer = new VBoxContainer { Name = "AgentList", SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
            _agentListContainer.AddThemeConstantOverride("separation", 0);
            agentContent.AddChild(_agentListContainer);

            timeline.AddChild(MakeTimelinePoint(_timelineAgentCircle, agentContent, isLast: true, circleTopOffset: TimelineCircleTopOffset));
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

            // The "MCP server" underlined title — INSIDE the framed card (Unity's frame-mcp-server includes it).
            card.AddChild(DockStyle.UnderlinedSubLabel("ServerLabel", "MCP server"));

            // --- Custom-mode server-URL row (shown only in Custom mode) ---
            _customHostRow = new VBoxContainer { Name = "CustomHostRow" };
            _customHostRow.AddThemeConstantOverride("separation", 4);
            card.AddChild(_customHostRow);

            // Server URL on ONE inline row — Unity's "Server URL <input>" (label left, input filling the rest),
            // not a stacked label-over-field.
            var hostLine = new HBoxContainer { Name = "HostLine", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            hostLine.AddThemeConstantOverride("separation", 8);
            hostLine.Alignment = BoxContainer.AlignmentMode.Center;
            _customHostRow.AddChild(hostLine);

            hostLine.AddChild(new Label { Name = "HostLabel", Text = "Server URL" });

            _hostField = new LineEdit
            {
                Name = "HostField",
                PlaceholderText = GodotMcpConfig.DefaultCustomHost,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            DockStyle.ApplyInput(_hostField);
            // Commit on Enter and on focus-out (mirrors the Unity reference's FocusOut commit). Connected via
            // object+method Callables (not delegate +=) so they never enter the ManagedCallable hot-reload registry.
            _hostField.Connect(LineEdit.SignalName.TextSubmitted, new Callable(this, MethodName.OnHostSubmitted));
            _hostField.Connect(Control.SignalName.FocusExited, new Callable(this, MethodName.OnHostFocusExited));
            hostLine.AddChild(_hostField);

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
            DockStyle.ConnectPressed(_generateTokenButton, this, MethodName.OnGenerateTokenPressed);
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
            DockStyle.ConnectPressed(_serverStartStopButton, this, MethodName.OnServerStartStopPressed);
            serverLine.AddChild(_serverStartStopButton);

            _localServerRow.AddChild(serverLine);

            // --- Env/.env override note (shown when a process env / .env value forces mode or host) ---
            _overrideNote = new Label
            {
                Name = "OverrideNote",
                Text = "Overridden by environment (GODOT_MCP_*) — UI changes won't take effect.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _overrideNote.AddThemeColorOverride("font_color", new Color(0.92f, 0.74f, 0.20f));
            card.AddChild(_overrideNote);

            // The MCP-server config IS wrapped in the blue frame-group card — this is one of the only two frames
            // the dock keeps (Unity's `frame-mcp-server`). The Server URL row inside is now inline (label + input).
            parent.AddChild(DockStyle.Card(card, "Server"));
        }

        /// <summary>
        /// Build the Cloud-mode auth row — masked cloud token field + Revoke/Authorize + a status line — and add
        /// it DIRECTLY to the panel, under the header and above the timeline (Unity gold reference). Shown only in
        /// Cloud mode (toggled by <see cref="ApplyModeVisibility"/>). It deliberately lives OUTSIDE the MCP-server
        /// card because in Cloud mode that entire timeline point is hidden — the cloud token UI must survive.
        /// </summary>
        void BuildCloudAuthRow()
        {
            _cloudAuthRow = new VBoxContainer { Name = "CloudAuthRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _cloudAuthRow.AddThemeConstantOverride("separation", 4);
            AddChild(_cloudAuthRow);

            var cloudTokenLine = new HBoxContainer { Name = "CloudTokenLine", SizeFlagsHorizontal = SizeFlags.ExpandFill };
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

            _revokeButton = new Button { Name = "RevokeButton", Text = "Revoke" };
            DockStyle.ConnectPressed(_revokeButton, this, MethodName.OnRevokeButtonPressed);
            cloudTokenLine.AddChild(_revokeButton);

            _authorizeButton = new Button { Name = "AuthorizeButton", Text = ConnectionPanelView.AuthorizeButtonText };
            DockStyle.ConnectPressed(_authorizeButton, this, MethodName.OnAuthorizeButtonPressed);
            cloudTokenLine.AddChild(_authorizeButton);

            _cloudAuthStatus = new Label
            {
                Name = "CloudAuthStatus",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Visible = false // hidden until there's a message — an empty label would otherwise reserve a line and
                                // open a large gap between the token row and the timeline in Cloud mode.
            };
            _cloudAuthRow.AddChild(_cloudAuthStatus);
        }

        /// <summary>
        /// Set the Cloud-auth status line text and collapse the label when the text is empty (so an empty status
        /// does not reserve a blank line that pushes the timeline down in Cloud mode). Single sink for every
        /// status update (device-auth flow, revoke, server rejection).
        /// </summary>
        void SetCloudAuthStatusText(string text)
        {
            _cloudAuthStatus.Text = text;
            _cloudAuthStatus.Visible = !string.IsNullOrEmpty(text);
        }

        /// <summary>
        /// Compose one timeline point: a 20px indicator column (the status circle, and below it a 2px
        /// connecting line that ExpandFills to span the gap to the next point — hidden on the LAST point) next
        /// to the point's <paramref name="content"/>. Mirrors Unity-MCP's timeline row.
        /// </summary>
        static HBoxContainer MakeTimelinePoint(Panel circle, Control content, bool isLast, int circleTopOffset = 4)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);

            // Indicator column: circle on top, connecting line filling the rest (hidden on the last point).
            var indicator = new VBoxContainer
            {
                Name = "Indicator",
                CustomMinimumSize = new Vector2(DockTheme.TimelineIndicatorWidth, 0)
            };
            indicator.AddThemeConstantOverride("separation", 0);

            // Push the circle down by circleTopOffset so its center lines up with the point's TITLE text (the title
            // sits at the content top for Godot/AI-agent, or inside the card's margin+padding for MCP server).
            var circleWrap = new MarginContainer { Name = "CircleWrap" };
            circleWrap.AddThemeConstantOverride("margin_top", circleTopOffset);
            circleWrap.AddChild(circle);
            indicator.AddChild(circleWrap);

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

            // The underlined "Godot" label carries the status now ("Godot" / "Godot: connecting..." / "Godot: connected").
            DockStyle.SetUnderlinedSubLabelText(_godotUnderline, ConnectionPanelView.GodotLineLabel(status));
            _connectButton.Text = ConnectionPanelView.ButtonText(status);
            // A single always-enabled toggle: primary (cyan) "Connect" when disconnected; secondary (gray) "Stop"
            // when connected OR connecting.
            if (status == ConnectionStatus.Disconnected)
                DockStyle.ApplyPrimaryButton(_connectButton);
            else
                DockStyle.ApplySecondaryButton(_connectButton);

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
        public void OnServerStartStopPressed()
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
            // A dev-injected status pins the label: skip the live re-sync so the injection sticks on screen
            // (see _devStatusOverride). DEV-ONLY — never set in a shipped addon.
            if (_devStatusOverride != null)
                return;

            var live = _connection.ConnectionStatus;
            if (_renderedStatus == live)
                return;

            _connection.LogStatusTrace(
                $"[Godot-MCP] re-sync: label '{_renderedStatus}' drifted from live '{live}' — re-applying.");
            ApplyStatus(live);
        }

        public void OnConnectButtonPressed()
        {
            // Single toggle: "Connect" only when fully disconnected; otherwise "Stop" — disconnect when connected,
            // or cancel the in-flight attempt when connecting (Disconnect() stops the client's retry loop).
            if (_connection.ConnectionStatus == ConnectionStatus.Disconnected)
                _connection.Connect();
            else
                _connection.Disconnect();
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

            ConfigChanged?.Invoke(); // mode change can swap the resolved URL → agent section re-checks config
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

            ConfigChanged?.Invoke(); // auth option / token changed → agent section re-checks config
        }

        /// <summary>
        /// Generate a fresh Custom-mode token, persist it, and reconnect so the new bearer is used. The
        /// token is never logged and is shown only as a masked field. Generating implies the user wants
        /// auth, so this also flips <see cref="GodotMcpAuthOption"/> to <c>Required</c> if it was off.
        /// </summary>
        public void OnGenerateTokenPressed()
        {
            _connection.Config.CustomToken = GodotMcpTokenGenerator.Generate();
            if (_connection.Config.AuthOption == GodotMcpAuthOption.None)
                _connection.Config.AuthOption = GodotMcpAuthOption.Required;

            _connection.Save();
            ApplyAuthVisibility();

            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Custom)
                _connection.Reconnect();

            ConfigChanged?.Invoke(); // new token → agent section re-checks config
        }

        public void OnHostSubmitted(string text) => CommitHost(text);

        public void OnHostFocusExited() => CommitHost(_hostField.Text);

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

            // The resolved MCP URL changed → the AI-agent section must re-check its config (show Reconfigure).
            ConfigChanged?.Invoke();
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

            // The ENTIRE MCP-server timeline point (circle + Server URL / Start / Transport / Authorization card)
            // is Custom-mode only. In Cloud mode it is hidden, leaving the timeline as Godot -> AI agent and the
            // cloud token/Authorize row (above the timeline) as the only server-side UI (Unity gold reference).
            _serverTimelinePoint.Visible = persistedCustom;
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
        public void OnAuthorizeButtonPressed()
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
            SetCloudAuthStatusText(ConnectionPanelView.CloudAuthStatusMessage(state, flow.UserCode, flow.ErrorMessage));
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

            ApplyAuthorizedToken(token);
        }

        /// <summary>
        /// Persist a freshly-obtained Cloud token, refresh the masked field + alerts, and reconnect so the new
        /// bearer is used (Cloud mode only). Shared by the real device-auth flow
        /// (<see cref="PersistAuthorizedToken"/>) and the DEV-ONLY <see cref="DevSimulateCloudAuthorized"/> so
        /// both drive the identical persist → reconnect path. MUST run on the editor main thread.
        /// </summary>
        void ApplyAuthorizedToken(string token)
        {
            _connection.Config.CloudToken = token;
            _connection.Save();
            ApplyCloudAuthState();
            ApplyAlertVisibility(_connection.ConnectionStatus);

            // Reconnect so the new bearer is used — only meaningful when the live mode is Cloud.
            if (_connection.Config.ActiveMode == GodotMcpConnectionMode.Cloud)
                _connection.Reconnect();

            ConfigChanged?.Invoke(); // cloud token changed → agent section re-checks config
        }

        /// <summary>
        /// DEV-ONLY: simulate a successful Cloud device-authorization by persisting <paramref name="token"/>
        /// through the EXACT same path the real flow uses (<see cref="ApplyAuthorizedToken"/>) — set + save the
        /// token, refresh the masked field / Revoke button / alerts, and Reconnect(). Lets the dev-control
        /// bridge exercise the "authorized → persist → reconnect" path (and the stale-rejection guard) without a
        /// live browser OAuth round-trip. No-op behavior is identical to the real flow; never logs the token.
        /// </summary>
        public void DevSimulateCloudAuthorized(string token) => ApplyAuthorizedToken(token);

        /// <summary>
        /// Revoke the stored cloud token: clear it, persist, revert the UI to the Authorize state, and (if
        /// the live mode is Cloud) disconnect so the now-unauthenticated session does not linger.
        /// </summary>
        public void OnRevokeButtonPressed()
        {
            _connection.Config.CloudToken = null;
            _connection.Save();
            SetCloudAuthStatusText("Token revoked.");
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
            SetCloudAuthStatusText("Authorization rejected by server — press Authorize.");
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
            RefreshAgents();
        }

        void OnAgentsUpdated() => RefreshAgents();

        /// <summary>
        /// Re-render the AI-agent timeline point from the connection's live <see cref="GodotMcpConnection.ActiveAgents"/>:
        /// the muted summary suffix ("(N connected)" / "(connects on demand)"), one list row per connected MCP client
        /// (Copilot / Claude / …), and the agent circle (filled green when ≥1 agent is connected). All text/dot
        /// decisions come from the pure-managed <see cref="AgentSessionView"/>. Runs on the editor main thread
        /// (<see cref="GodotMcpConnection.AgentsUpdated"/> is marshalled there); cheap + idempotent so re-seeds are quiet.
        /// </summary>
        void RefreshAgents()
        {
            var agents = _devAgentsOverride ?? _connection.ActiveAgents;
            var count = agents?.Count ?? 0;

            _agentLabel.Text = AgentSessionView.Summary(count);
            DockStyle.ApplyTimelineCircle(_timelineAgentCircle, AgentSessionView.DotState(count));

            // Rebuild the per-agent rows. Detach + free synchronously so a stale row never lingers (QueueFree alone
            // would defer to the next idle frame and briefly double the list).
            foreach (var child in _agentListContainer.GetChildren())
            {
                _agentListContainer.RemoveChild(child);
                child.QueueFree();
            }

            if (count > 0)
            {
                foreach (var agent in agents!)
                {
                    var row = new Label { Name = "AgentRow", Text = "• " + AgentSessionView.RowLabel(agent) };
                    DockStyle.ApplyDescription(row);
                    row.AutowrapMode = TextServer.AutowrapMode.Off;
                    _agentListContainer.AddChild(row);
                }
            }

            _agentListContainer.Visible = count > 0;
        }

        // --- DEV-ONLY inject API (driven by the env-gated, 127.0.0.1 DevControlServer) ------------------------
        //
        // These exist purely so a terminal / AI agent can paint a FAKE state onto the LIVE dock for test +
        // AI-driven development. They are no-ops in a shipped addon because the server that calls them only
        // starts when GODOT_MCP_DEV_CONTROL=1. ALL of them must run on the editor main thread (the caller
        // hops via MainThreadDispatcher).

        /// <summary>
        /// DEV-ONLY: paint a fake <see cref="ConnectionStatus"/> onto the dock and PIN it — the periodic
        /// re-sync is suppressed (see <see cref="_devStatusOverride"/>) so the injected status sticks instead
        /// of being reverted to the live status within ~<see cref="ResyncIntervalSeconds"/>s. Clear it with
        /// <see cref="DevClearStatusOverride"/>.
        /// </summary>
        public void DevInjectStatus(ConnectionStatus status)
        {
            _devStatusOverride = status;
            ApplyStatus(status);
        }

        /// <summary>
        /// DEV-ONLY: drop the injected-status pin and re-converge to the LIVE connection status (so the dock
        /// resumes reflecting reality). Pairs with <see cref="DevInjectStatus"/>.
        /// </summary>
        public void DevClearStatusOverride()
        {
            _devStatusOverride = null;
            ApplyStatus(_connection.ConnectionStatus);
        }

        /// <summary>
        /// DEV-ONLY: paint a fake local-server <see cref="GodotMcpServerStatus"/> onto the MCP-server timeline
        /// point. No override is needed — the server status has no periodic re-sync (unlike the connection
        /// status), so the injected value persists until the next real <c>StatusChanged</c>.
        /// </summary>
        public void DevInjectServerStatus(GodotMcpServerStatus status) => ApplyServerStatus(status);

        /// <summary>
        /// DEV-ONLY: paint a fake connected-agent list onto the AI-agent timeline point (green dot + per-agent rows
        /// + "(N connected)" summary) and PIN it (RefreshAgents renders the override until cleared), so the smoke
        /// harness can exercise the live agent display without a real external MCP client. Pairs with
        /// <see cref="DevClearAgentsOverride"/>. No-op in a shipped addon (the caller only runs when GODOT_MCP_DEV_CONTROL=1).
        /// </summary>
        public void DevInjectAgents(IReadOnlyList<McpClientData> agents)
        {
            _devAgentsOverride = agents ?? System.Array.Empty<McpClientData>();
            RefreshAgents();
        }

        /// <summary>DEV-ONLY: drop the injected agent list and re-converge to the LIVE <see cref="GodotMcpConnection.ActiveAgents"/>.</summary>
        public void DevClearAgentsOverride()
        {
            _devAgentsOverride = null;
            RefreshAgents();
        }

        public override void _ExitTree()
        {
            // Unsubscribe so a freed panel does not receive a late main-thread push.
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connection.AuthorizationRejected -= OnAuthorizationRejected;
            _serverManager.StatusChanged -= OnServerStatusChanged;
            _connection.AgentsUpdated -= OnAgentsUpdated;

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
