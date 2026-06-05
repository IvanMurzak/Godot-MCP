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
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The connection section of the Godot-MCP editor dock — the Godot <see cref="Control"/> analog of
    /// Unity-MCP's <c>MainWindowEditor.Connection</c>. A <see cref="VBoxContainer"/> the
    /// <see cref="GodotMcpDock"/> drops into its Body, wired to a <see cref="GodotMcpConnection"/>. It
    /// renders, top to bottom:
    /// <list type="bullet">
    ///   <item>A status row — a coloured dot + a "Godot: …" label, live from
    ///   <see cref="GodotMcpConnection.ConnectionStatus"/>.</item>
    ///   <item>A Connect/Disconnect button whose text + enabled-state reflect the status.</item>
    ///   <item>A Custom|Cloud mode selector that persists the choice and reconnects.</item>
    ///   <item>A Server URL field (Custom mode only) bound to the custom host, with validation.</item>
    ///   <item>A simplified Godot → MCP server → AI agent timeline of status dots.</item>
    /// </list>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it builds live Godot UI <see cref="Node"/>s, so it is verified via
    /// the headless Godot smoke (<c>test.md</c> Suite 3), not the plain-xUnit host. ALL presentation
    /// decisions (status reduction, label/button text, dot colour, URL validation) live in the
    /// pure-managed <see cref="ConnectionPanelView"/> so they ARE unit-tested.
    /// </para>
    /// </summary>
    [Tool]
    public partial class ConnectionPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        // Status row.
        ColorRect _statusDot = null!;
        Label _statusLabel = null!;
        Button _connectButton = null!;

        // Mode selector + custom-host row.
        OptionButton _modeSelector = null!;
        VBoxContainer _customHostRow = null!;
        LineEdit _hostField = null!;
        Label _overrideNote = null!;

        // Cloud-mode auth section (device-code flow): masked token + Authorize/Revoke + status.
        VBoxContainer _cloudAuthRow = null!;
        LineEdit _cloudTokenField = null!;
        Button _authorizeButton = null!;
        Button _revokeButton = null!;
        Label _cloudAuthStatus = null!;

        // The in-flight device-auth flow (null when none has run). Recreated per Authorize click.
        GodotDeviceAuthFlow? _deviceAuthFlow;

        // Custom-mode authorization row (auth option + masked token + Generate).
        OptionButton _authSelector = null!;
        VBoxContainer _tokenRow = null!;
        LineEdit _tokenField = null!;
        Button _generateTokenButton = null!;

        // Index of the two Authorization OptionButton entries — kept stable so a selection maps to a value.
        const int AuthIdNone = 0;
        const int AuthIdRequired = 1;

        // Timeline dots.
        ColorRect _timelineGodotDot = null!;
        ColorRect _timelineServerDot = null!;
        ColorRect _timelineAgentDot = null!;

        // Index of the two OptionButton entries — kept stable so a selection maps back to a mode.
        const int ModeIdCustom = 0;
        const int ModeIdCloud = 1;

        public ConnectionPanel(GodotMcpConnection connection)
        {
            _connection = connection;
            Name = "ConnectionPanel";
            BuildUi();

            // Subscribe AFTER the UI exists so the first push has controls to write to. The connection
            // marshals these events onto the editor main thread, so the handlers may touch Controls directly.
            _connection.ConnectionStatusChanged += OnConnectionStatusChanged;
            _connection.AuthorizationRejected += OnAuthorizationRejected;

            // Render the current state immediately (the event only fires on CHANGE).
            ApplyStatus(_connection.ConnectionStatus);
            ApplyModeVisibility(_connection.Config.ActiveMode);
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            // --- Status row: dot + label ---
            var statusRow = new HBoxContainer { Name = "StatusRow" };
            AddChild(statusRow);

            _statusDot = MakeDot("StatusDot");
            statusRow.AddChild(_statusDot);

            _statusLabel = new Label { Name = "StatusLabel" };
            statusRow.AddChild(_statusLabel);

            // --- Connect/Disconnect button ---
            _connectButton = new Button { Name = "ConnectButton" };
            _connectButton.Pressed += OnConnectButtonPressed;
            AddChild(_connectButton);

            // --- Mode selector ---
            var modeRow = new HBoxContainer { Name = "ModeRow" };
            AddChild(modeRow);

            modeRow.AddChild(new Label { Name = "ModeLabel", Text = "Mode" });

            _modeSelector = new OptionButton { Name = "ModeSelector" };
            _modeSelector.AddItem("Custom", ModeIdCustom);
            _modeSelector.AddItem("Cloud", ModeIdCloud);
            _modeSelector.ItemSelected += OnModeSelected;
            modeRow.AddChild(_modeSelector);

            // --- Custom-mode server-URL row (shown only in Custom mode) ---
            _customHostRow = new VBoxContainer { Name = "CustomHostRow" };
            AddChild(_customHostRow);

            _customHostRow.AddChild(new Label { Name = "HostLabel", Text = "Server URL" });

            _hostField = new LineEdit
            {
                Name = "HostField",
                PlaceholderText = GodotMcpConfig.DefaultCustomHost,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            // Commit on Enter and on focus-out (mirrors the Unity reference's FocusOut commit).
            _hostField.TextSubmitted += OnHostSubmitted;
            _hostField.FocusExited += OnHostFocusExited;
            _customHostRow.AddChild(_hostField);

            // --- Authorization (Custom mode only): none | required ---
            var authRow = new HBoxContainer { Name = "AuthRow" };
            _customHostRow.AddChild(authRow);

            authRow.AddChild(new Label { Name = "AuthLabel", Text = "Authorization" });

            _authSelector = new OptionButton { Name = "AuthSelector" };
            _authSelector.AddItem("none", AuthIdNone);
            _authSelector.AddItem("required", AuthIdRequired);
            _authSelector.ItemSelected += OnAuthOptionSelected;
            authRow.AddChild(_authSelector);

            // --- Token row (shown only when Authorization == required): masked field + Generate ---
            _tokenRow = new VBoxContainer { Name = "TokenRow" };
            _customHostRow.AddChild(_tokenRow);

            _tokenRow.AddChild(new Label { Name = "TokenLabel", Text = "Token" });

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

            // --- Cloud-mode auth section (shown only in Cloud mode): device-code login ---
            _cloudAuthRow = new VBoxContainer { Name = "CloudAuthRow" };
            AddChild(_cloudAuthRow);

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
            AddChild(_overrideNote);

            AddChild(new HSeparator { Name = "TimelineSeparator" });

            // --- Simplified timeline: Godot -> MCP server -> AI agent ---
            var timeline = new VBoxContainer { Name = "Timeline" };
            AddChild(timeline);

            _timelineGodotDot = MakeDot("TimelineGodotDot");
            timeline.AddChild(MakeTimelineRow(_timelineGodotDot, "Godot"));

            _timelineServerDot = MakeDot("TimelineServerDot");
            timeline.AddChild(MakeTimelineRow(_timelineServerDot, "MCP server"));

            _timelineAgentDot = MakeDot("TimelineAgentDot");
            timeline.AddChild(MakeTimelineRow(_timelineAgentDot, "AI agent (connects on demand)"));

            // Sync the mode selector to the current config.
            SyncModeSelector(_connection.Config.ActiveMode);
        }

        static ColorRect MakeDot(string name) => new ColorRect
        {
            Name = name,
            CustomMinimumSize = new Vector2(10, 10),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Color = ToColor(ConnectionPanelView.ColorDisconnected)
        };

        static HBoxContainer MakeTimelineRow(ColorRect dot, string label)
        {
            var row = new HBoxContainer();
            row.AddChild(dot);
            row.AddChild(new Label { Text = label });
            return row;
        }

        static Color ToColor((float R, float G, float B) rgb) => new Color(rgb.R, rgb.G, rgb.B);

        void OnConnectionStatusChanged(ConnectionStatus status) => ApplyStatus(status);

        /// <summary>
        /// Push a <see cref="ConnectionStatus"/> into the status row, button, and the Godot + MCP-server
        /// timeline dots. All derived presentation comes from <see cref="ConnectionPanelView"/>. The
        /// "Godot" and "MCP server" timeline dots track the same hub state (a single connection); the
        /// "AI agent" dot is informational and stays neutral (the cloud-auth / agent-info task fills it in).
        /// </summary>
        void ApplyStatus(ConnectionStatus status)
        {
            var color = ToColor(ConnectionPanelView.StatusColor(status));

            _statusDot.Color = color;
            _statusLabel.Text = ConnectionPanelView.StatusLabel(status);

            _connectButton.Text = ConnectionPanelView.ButtonText(status);
            _connectButton.Disabled = ConnectionPanelView.ButtonDisabled(status);

            _timelineGodotDot.Color = color;
            _timelineServerDot.Color = color;
        }

        void OnConnectButtonPressed()
        {
            if (_connection.ConnectionStatus == ConnectionStatus.Connected)
                _connection.Disconnect();
            else
                _connection.Connect();
        }

        void OnModeSelected(long index)
        {
            var mode = index == ModeIdCloud
                ? GodotMcpConnectionMode.Cloud
                : GodotMcpConnectionMode.Custom;

            if (_connection.Config.ConnectionMode == mode)
            {
                // No persisted change selected; just re-render.
                ApplyModeVisibility(_connection.Config.ActiveMode);
                return;
            }

            // Persist the user's chosen PERSISTED mode regardless of any env/.env override. The selector
            // is always enabled (the change "does something" — it updates the persisted layer that takes
            // effect once the override is removed). Only RECONNECT when the change actually moves the LIVE
            // active mode: under an env override ActiveMode is pinned, so a persisted-only edit must not
            // tear down the current connection.
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
        void OnAuthOptionSelected(long index)
        {
            var authOption = index == AuthIdRequired
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
        /// (so editing the selector / URL / auth always targets the layer the user can change), while the
        /// override note surfaces when an env/.env value is forcing the LIVE active mode away from that
        /// persisted choice. The selector + host field stay ENABLED even when overridden — a persisted
        /// edit "does something" (it takes effect once the override is gone) and does NOT corrupt
        /// precedence, since env/.env is read live by the config resolvers. The host field shows the
        /// EFFECTIVE custom host (env override visible) for transparency.
        /// </summary>
        void ApplyModeVisibility(GodotMcpConnectionMode activeMode)
        {
            // Editable controls follow the PERSISTED mode (what the user is editing); the cloud-vs-custom
            // SECTION the user can configure is the persisted one, not the env-forced live one.
            var persistedMode = _connection.Config.ConnectionMode;
            var persistedCustom = persistedMode == GodotMcpConnectionMode.Custom;

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

            // Keep BOTH the mode selector and the host field ENABLED even when an env/.env value currently
            // forces the live mode/host: editing them updates the PERSISTED layer (it "does something"),
            // which takes effect once the override is removed. The override note explains that the env
            // value still wins for the live connection.
            _hostField.Editable = true;
            _modeSelector.Disabled = false;
        }

        /// <summary>
        /// Render the Custom-mode authorization controls from the persisted config: the auth selector
        /// reflects <see cref="GodotMcpConfig.AuthOption"/>, the masked token row is shown only when
        /// <c>Required</c>, and the field carries the stored Custom token (masked). The token is never
        /// shown in clear text or logged. Always reads/writes the PERSISTED layer (env auth override is
        /// surfaced by the override note, not by disabling these controls).
        /// </summary>
        void ApplyAuthVisibility()
        {
            var required = _connection.Config.AuthOption == GodotMcpAuthOption.Required;
            _authSelector.Selected = required ? AuthIdRequired : AuthIdNone;
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

            GD.PushWarning("[Godot-MCP] server rejected the authorization token; cleared — press Authorize.");
        }

        void SyncModeSelector(GodotMcpConnectionMode activeMode)
        {
            // Reflect the PERSISTED mode the user edits (not the env-forced live mode); the override note
            // explains when the two diverge.
            _modeSelector.Selected = _connection.Config.ConnectionMode == GodotMcpConnectionMode.Cloud
                ? ModeIdCloud
                : ModeIdCustom;
        }

        /// <summary>
        /// Re-render the panel from current connection state. Forwarded from <see cref="GodotMcpDock.Refresh"/>.
        /// Safe to call repeatedly.
        /// </summary>
        public void Refresh()
        {
            SyncModeSelector(_connection.Config.ActiveMode);
            ApplyModeVisibility(_connection.Config.ActiveMode);
            ApplyStatus(_connection.ConnectionStatus);
        }

        public override void _ExitTree()
        {
            // Unsubscribe so a freed panel does not receive a late main-thread push.
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connection.AuthorizationRejected -= OnAuthorizationRejected;

            // Cancel any in-flight device-auth flow so its background poll loop stops touching a freed panel.
            _deviceAuthFlow?.Cancel();
            _deviceAuthFlow = null;
        }
    }
}
#endif
