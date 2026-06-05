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
        Label _cloudNote = null!;
        Label _overrideNote = null!;

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
            // marshals this event onto the editor main thread, so the handler may touch Controls directly.
            _connection.ConnectionStatusChanged += OnConnectionStatusChanged;

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

            // --- Cloud-mode note (shown only in Cloud mode; cloud auth is a later task) ---
            _cloudNote = new Label
            {
                Name = "CloudNote",
                Text = "Cloud auth is configured separately.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            AddChild(_cloudNote);

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
                // No persisted change (e.g. an env override is forcing the active mode); just re-render.
                ApplyModeVisibility(_connection.Config.ActiveMode);
                return;
            }

            _connection.Config.ConnectionMode = mode;
            _connection.Save();
            ApplyModeVisibility(_connection.Config.ActiveMode);
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
        /// Show the Server-URL row only in Custom mode and the cloud note only in Cloud mode. Also surfaces
        /// the env/.env override note when a process env / <c>.env</c> value is forcing the active mode away
        /// from the persisted <see cref="GodotMcpConfig.ConnectionMode"/> (so the user understands why a UI
        /// change may not take effect). Reflects the EFFECTIVE host in the field (env override shown).
        /// </summary>
        void ApplyModeVisibility(GodotMcpConnectionMode activeMode)
        {
            var isCustom = activeMode == GodotMcpConnectionMode.Custom;
            _customHostRow.Visible = isCustom;
            _cloudNote.Visible = !isCustom;

            if (isCustom)
            {
                // Show the EFFECTIVE custom host (env GODOT_MCP_HOST wins over the persisted value).
                _hostField.Text = _connection.Config.ResolveCustomHost();
            }

            // The active mode differs from the persisted mode only when an env/.env override forced it.
            var overridden = activeMode != _connection.Config.ConnectionMode;
            _overrideNote.Visible = overridden;
            // When overridden, the persisted-mode UI cannot change the effective mode: disable the field.
            _hostField.Editable = !overridden;
            _modeSelector.Disabled = overridden;
        }

        void SyncModeSelector(GodotMcpConnectionMode activeMode)
        {
            _modeSelector.Selected = activeMode == GodotMcpConnectionMode.Cloud ? ModeIdCloud : ModeIdCustom;
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
            // Unsubscribe so a freed panel does not receive a late main-thread status push.
            _connection.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }
    }
}
#endif
