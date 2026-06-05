/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Connection;
using Microsoft.AspNetCore.SignalR.Client;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The simplified, editor-facing connection status the dock renders, derived from the reused
    /// McpPlugin client's <see cref="HubConnectionState"/> plus its <c>KeepConnected</c> flag. Three
    /// buckets (instead of SignalR's four) because the dock only needs to show the user one of: not
    /// trying to connect, trying/handshaking, or fully connected.
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>Not connected and not attempting to (KeepConnected is off, or the hub is down).</summary>
        Disconnected,

        /// <summary>Attempting to connect / reconnect / handshake (KeepConnected on, not yet Connected).</summary>
        Connecting,

        /// <summary>Connected to the MCP server and the application-level handshake succeeded.</summary>
        Connected
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) presentation logic for the connection
    /// panel: the <see cref="HubConnectionState"/> + <c>keepConnected</c> → <see cref="ConnectionStatus"/>
    /// reduction, the status/label/button text mappings, the status-dot colour mapping, and server-URL
    /// validation. Keeping this here (rather than inline in the <c>#if TOOLS</c> <c>ConnectionPanel</c>)
    /// makes every decision unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host without
    /// constructing a single Godot <see cref="Godot.Control"/>.
    ///
    /// <para>
    /// The Godot analog of Unity-MCP's <c>MainWindowEditor.CreateGUI</c> status helpers
    /// (<c>GetConnectionStatusText</c>/<c>GetButtonText</c>/<c>GetConnectionStatusClass</c>), condensed to
    /// the single <see cref="ConnectionStatus"/> enum the dock binds to.
    /// </para>
    /// </summary>
    public static class ConnectionPanelView
    {
        // --- Status dot colours (RGB tuples; the #if TOOLS panel maps these to a Godot Color). ---

        /// <summary>Green — fully connected.</summary>
        public static readonly (float R, float G, float B) ColorConnected = (0.30f, 0.78f, 0.35f);

        /// <summary>Amber/yellow — connecting / reconnecting.</summary>
        public static readonly (float R, float G, float B) ColorConnecting = (0.92f, 0.74f, 0.20f);

        /// <summary>Gray — disconnected / idle.</summary>
        public static readonly (float R, float G, float B) ColorDisconnected = (0.50f, 0.50f, 0.50f);

        /// <summary>Button label shown when the plugin is disconnected (clicking connects).</summary>
        public const string ButtonTextConnect = "Connect";

        /// <summary>Button label shown when the plugin is connected (clicking disconnects).</summary>
        public const string ButtonTextDisconnect = "Disconnect";

        /// <summary>Button label shown mid-connect (the button is disabled while this shows).</summary>
        public const string ButtonTextConnecting = "Connecting…";

        /// <summary>
        /// Reduce SignalR's four-state <see cref="HubConnectionState"/> plus the client's
        /// <paramref name="keepConnected"/> intent into the three buckets the dock renders. Mirrors the
        /// Unity reference's <c>GetConnectionStatusClass</c>: <see cref="ConnectionStatus.Connected"/> only
        /// when the hub reports <see cref="HubConnectionState.Connected"/> AND the client still wants to be
        /// connected; any other state while <paramref name="keepConnected"/> is on reads as
        /// <see cref="ConnectionStatus.Connecting"/> (the client is retrying); otherwise
        /// <see cref="ConnectionStatus.Disconnected"/>.
        /// </summary>
        public static ConnectionStatus Reduce(HubConnectionState state, bool keepConnected) => state switch
        {
            HubConnectionState.Connected when keepConnected => ConnectionStatus.Connected,
            _ when keepConnected => ConnectionStatus.Connecting,
            _ => ConnectionStatus.Disconnected
        };

        /// <summary>The "Godot: …" status line text for a given <see cref="ConnectionStatus"/>.</summary>
        public static string StatusLabel(ConnectionStatus status) => status switch
        {
            ConnectionStatus.Connected => "Godot: Connected",
            ConnectionStatus.Connecting => "Godot: Connecting…",
            _ => "Godot: Disconnected"
        };

        /// <summary>The connect/disconnect button text for a given <see cref="ConnectionStatus"/>.</summary>
        public static string ButtonText(ConnectionStatus status) => status switch
        {
            ConnectionStatus.Connected => ButtonTextDisconnect,
            ConnectionStatus.Connecting => ButtonTextConnecting,
            _ => ButtonTextConnect
        };

        /// <summary>
        /// True when the connect/disconnect button should be disabled — only while
        /// <see cref="ConnectionStatus.Connecting"/>, where neither "Connect" nor "Disconnect" is a clean
        /// action (the client is mid-handshake / retrying).
        /// </summary>
        public static bool ButtonDisabled(ConnectionStatus status) => status == ConnectionStatus.Connecting;

        /// <summary>The status-dot RGB for a given <see cref="ConnectionStatus"/>.</summary>
        public static (float R, float G, float B) StatusColor(ConnectionStatus status) => status switch
        {
            ConnectionStatus.Connected => ColorConnected,
            ConnectionStatus.Connecting => ColorConnecting,
            _ => ColorDisconnected
        };

        /// <summary>
        /// Validate a user-entered server URL for Custom mode: it must be a non-empty, absolute
        /// <c>http</c>/<c>https</c> URL. Reuses <see cref="Connection.GodotMcpConfig.IsValidHttpUrl"/> so the
        /// dock's accept/reject rule matches exactly what the connection layer would accept (no drift
        /// between what the field allows and what the config resolver honours). Surrounding whitespace /
        /// a single pair of wrapping quotes are normalized away first, matching the env/.env sanitization.
        /// </summary>
        public static bool IsValidServerUrl(string? url)
        {
            var normalized = Connection.GodotMcpConfig.NormalizeUrl(url);
            return !string.IsNullOrEmpty(normalized) && Connection.GodotMcpConfig.IsValidHttpUrl(normalized!);
        }

        // --- Cloud device-auth presentation (pure-managed, unit-tested). ---

        /// <summary>Cloud-token field placeholder shown when no token is stored (the field is masked + read-only).</summary>
        public const string CloudTokenPlaceholder = "Token — press Authorize";

        /// <summary>Authorize-button label while the flow is idle / finished (clicking starts a new flow).</summary>
        public const string AuthorizeButtonText = "Authorize";

        /// <summary>Authorize-button label while the flow is running (clicking cancels it).</summary>
        public const string AuthorizeButtonCancelText = "Cancel";

        /// <summary>
        /// The status line for a given device-auth flow state. Mirrors Unity-MCP's
        /// <c>GetAuthFlowStatusMessage</c>. The <paramref name="userCode"/> is fine to show (it is the
        /// short device code the user types into the browser); the access TOKEN is NEVER passed here or
        /// shown anywhere except masked in the token field. <paramref name="errorMessage"/> is the flow's
        /// non-secret diagnostic text. Pure-managed → unit-tested.
        /// </summary>
        public static string CloudAuthStatusMessage(
            GodotDeviceAuthFlowState state, string? userCode, string? errorMessage) => state switch
        {
            GodotDeviceAuthFlowState.Initiating => "Initiating…",
            GodotDeviceAuthFlowState.WaitingForUser => $"Code: {userCode} — Authorize in browser",
            GodotDeviceAuthFlowState.Polling => $"Code: {userCode} — Waiting…",
            GodotDeviceAuthFlowState.Authorized => "Authorized!",
            GodotDeviceAuthFlowState.Failed => $"Failed: {errorMessage}",
            GodotDeviceAuthFlowState.Expired => "Expired — try again",
            GodotDeviceAuthFlowState.Cancelled => "Cancelled",
            _ => string.Empty
        };

        /// <summary>The Authorize/Cancel button text for a given flow state (Cancel while running).</summary>
        public static string CloudAuthButtonText(GodotDeviceAuthFlowState state) =>
            GodotDeviceAuthFlow.IsRunning(state) ? AuthorizeButtonCancelText : AuthorizeButtonText;
    }
}
