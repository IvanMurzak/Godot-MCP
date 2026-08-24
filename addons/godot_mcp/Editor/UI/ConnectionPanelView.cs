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

        /// <summary>
        /// Button label shown when the plugin is connected OR connecting — a single "Stop" that disconnects
        /// (or cancels the in-flight connect attempt). The Godot line is one toggle button: "Connect" when
        /// idle, "Stop" otherwise, enabled in every state.
        /// </summary>
        public const string ButtonTextStop = "Stop";

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

        /// <summary>
        /// The Godot timeline point's (underlined) label text, which now CARRIES the connection status:
        /// "Godot" when disconnected, "Godot: connecting..." mid-handshake, "Godot: connected" when connected.
        /// Replaces the old separate status suffix so the one underlined label conveys both the point name and
        /// its live state.
        /// </summary>
        public static string GodotLineLabel(ConnectionStatus status) => status switch
        {
            ConnectionStatus.Connected => "Godot: connected",
            ConnectionStatus.Connecting => "Godot: connecting...",
            _ => "Godot"
        };

        /// <summary>
        /// The Godot-line toggle button text: "Connect" when disconnected, "Stop" when connected OR connecting
        /// (a single always-enabled button — clicking "Stop" disconnects / cancels the in-flight attempt).
        /// </summary>
        public static string ButtonText(ConnectionStatus status) =>
            status == ConnectionStatus.Disconnected ? ButtonTextConnect : ButtonTextStop;

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

        // --- Cloud sign-in / account state (mcp-authorize e1 · PR 5, design 06/09). Pure-managed, unit-tested. ---

        /// <summary>Account-state chip text shown when a cloud credential is stored (signed in via the device flow / machine store).</summary>
        public const string SignedInLabel = "Signed in to ai-game.dev";

        /// <summary>Account-state chip text shown when no cloud credential is stored (the default path prompts sign-in, not a raw token).</summary>
        public const string SignedOutLabel = "Not signed in — sign in to ai-game.dev";

        /// <summary>
        /// The Cloud account-state chip text, driven by whether a cloud credential is stored (the device-flow /
        /// machine-store sign-in state from PR 2). With the machine credential store the token is no longer a
        /// user-entered field on the golden path — the plugin surfaces this signed-in / signed-out state instead.
        /// </summary>
        public static string CloudSignInStatusLabel(bool signedIn) => signedIn ? SignedInLabel : SignedOutLabel;

        /// <summary>The Cloud account-state chip colour: green when signed in, gray when not (reuses the status-dot palette).</summary>
        public static (float R, float G, float B) CloudSignInStatusColor(bool signedIn) =>
            signedIn ? ColorConnected : ColorDisconnected;

        /// <summary>
        /// The ONE cloud signed-in predicate the panel renders from (unified-machine-auth task f1): signed in
        /// when the MACHINE STORE holds a usable account credential (<paramref name="accountSignedIn"/> — the
        /// F1 golden path), OR when the legacy <c>user://</c> config still carries a
        /// <paramref name="legacyCloudToken"/> (the O8 read-fallback window, one release). Pure-managed →
        /// unit-tested; the panel must never re-derive this from <c>Config.CloudToken</c> alone, or a
        /// machine-store sign-in would render as signed-out.
        /// </summary>
        public static bool IsCloudSignedIn(bool accountSignedIn, string? legacyCloudToken) =>
            accountSignedIn || !string.IsNullOrEmpty(legacyCloudToken);

        /// <summary>
        /// The F6 sign-out confirmation copy ("signs out all tools on this machine" — design 03 F6.1). Shown
        /// BEFORE the machine-wide sign-out revokes every stored credential family and deletes the shared
        /// machine store.
        /// </summary>
        public const string SignOutConfirmText =
            "Sign out of ai-game.dev on this machine?\nThis signs out ALL AI-Game-Dev tools on this machine (Unity, Godot, Unreal, CLI, desktop app).";

        /// <summary>Cloud-auth status line after a completed machine-wide sign-out.</summary>
        public const string SignedOutStatusText = "Signed out on this machine.";

        /// <summary>Cloud-auth status line when the sign-out could not delete the store (credential lock busy).</summary>
        public const string SignOutBusyStatusText = "Sign-out is busy (another tool holds the credential lock) — try again.";

        /// <summary>
        /// The Cloud-auth status line for a finished sign-in attempt (the machine-store F1 commit outcome —
        /// distinct from <see cref="CloudAuthStatusMessage"/>, which renders the DEVICE-FLOW states). Never
        /// carries token material; <see cref="GodotAccountSignInResult.Detail"/> is non-secret by contract.
        /// </summary>
        public static string SignInOutcomeMessage(GodotAccountSignInResult outcome) => outcome.Status switch
        {
            GodotAccountSignInStatus.SignedIn => "Signed in — all AI-Game-Dev tools on this machine are authorized.",
            // The retry already ran (GodotAccountAuth's bounded second phase) and any abandoned mint was
            // revoked — a fresh Authorize re-runs the whole flow, so say "retry", never promise "finish".
            GodotAccountSignInStatus.PartiallyAuthorized => "Partially authorized — sign-in didn't complete; press Authorize to retry.",
            GodotAccountSignInStatus.NotAuthorized => string.Empty, // the device-flow state line already rendered the terminal state
            GodotAccountSignInStatus.Busy => "Another tool holds the credential lock — try again.",
            GodotAccountSignInStatus.SubjectConflict => "This machine is signed in as a different account — sign out first.",
            _ => string.IsNullOrEmpty(outcome.Detail) ? "Sign-in failed — try again." : $"Sign-in failed: {outcome.Detail}",
        };

        // --- Authorization-rejected / sign-in-required presentation (oauth-client-error-hygiene e2). ---

        /// <summary>
        /// What the panel's authorization-rejected handler renders. Pure decision (unit-tested) so the
        /// <c>#if TOOLS</c> panel handler is a thin switch — and so the e2 silent-red fix (signed-in +
        /// auth-rejected must RENDER, never early-return to silence) is pinned by a plain-xUnit test.
        /// </summary>
        public enum AuthRejectedPresentation
        {
            /// <summary>Not a Cloud-credential concern (Custom mode) — render nothing.</summary>
            None,

            /// <summary>
            /// Signed in via the machine store: render the sign-in-required state. The store/sink are NOT
            /// wiped (recovery belongs to the connection's reactive refresh; the store is never written on
            /// a rejection) — but the user must SEE the state instead of a silent red (the e2 fix).
            /// </summary>
            SignInRequired,

            /// <summary>
            /// Signed out of the machine store (legacy-sink token only): drop the rejected legacy token,
            /// persist, and prompt for a fresh Authorize (the pre-e2 behavior, unchanged).
            /// </summary>
            ClearLegacyTokenAndPrompt,
        }

        /// <summary>
        /// Decide the authorization-rejected presentation. The signed-in case MUST NOT be silent: before e2
        /// the panel early-returned for signed-in users, so a dead machine-store credential showed nothing
        /// (the silent-red bug found in review — same as Unity's). Signed-in + rejected now renders
        /// <see cref="AuthRejectedPresentation.SignInRequired"/>.
        /// </summary>
        public static AuthRejectedPresentation AuthorizationRejectedPresentation(bool isCloudMode, bool accountSignedIn)
        {
            if (!isCloudMode)
                return AuthRejectedPresentation.None;

            return accountSignedIn
                ? AuthRejectedPresentation.SignInRequired
                : AuthRejectedPresentation.ClearLegacyTokenAndPrompt;
        }

        /// <summary>
        /// The generic sign-in-required status line (D4). Rendered for every terminal credential verdict —
        /// reason-class-specific rendering is not wired yet.
        /// </summary>
        // reason-class slot: the pinned McpPlugin (8.3.0+) exposes SignInRequiredReason — render
        // reason-class-specific status here (e.g. a "server configuration error" line for
        // invalid_target) instead of this generic text when the reason-class UX lands.
        public const string SignInRequiredStatusText =
            "Sign in required — your session expired. Press Authorize.";

        /// <summary>The status line for a legacy-sink token the server rejected (signed-out path).</summary>
        public const string AuthorizationRejectedPromptText =
            "Authorization rejected by server — press Authorize.";

        /// <summary>
        /// True when <paramref name="statusText"/> is the sign-in-required line — used by the panel to clear
        /// a now-disproved "sign in required" (the hub reconnected, or the account state reached SignedIn)
        /// without clobbering an unrelated status message.
        /// </summary>
        public static bool IsSignInRequiredStatus(string? statusText) =>
            statusText == SignInRequiredStatusText;

        /// <summary>The "Advanced: use access token" opt-in label — reveals the legacy masked token field on the Cloud path.</summary>
        public const string AdvancedUseAccessTokenLabel = "Advanced: use access token";

        /// <summary>
        /// Whether the raw (masked) cloud access-token field is shown in the connection view. The default
        /// (device-flow / OAuth) path HIDES it — the sign-in chip conveys the state instead — and it is revealed
        /// only when the user opts into the legacy access-token affordance (<paramref name="useAccessToken"/>).
        /// </summary>
        public static bool ShowCloudTokenField(bool useAccessToken) => useAccessToken;

        /// <summary>
        /// The derived per-project connection-URL line ("Connection URL: &lt;url&gt;") for the pinned project URL
        /// the shared configurator writes (the <c>/p/&lt;pin&gt;</c> path segment + derived port from PRs 3–4). A
        /// null / whitespace <paramref name="pinnedUrl"/> yields the empty string so the editor collapses the row.
        /// </summary>
        public static string ConnectionUrlLabel(string? pinnedUrl) =>
            string.IsNullOrWhiteSpace(pinnedUrl) ? string.Empty : $"Connection URL: {pinnedUrl!.Trim()}";

        // --- Vertical timeline (Godot -> MCP server -> AI agent) presentation (pure-managed, unit-tested). ---

        /// <summary>
        /// The visual state of one timeline status circle, decoupled from <see cref="ConnectionStatus"/> so the
        /// editor's circle painter maps it 1:1 to a Godot <c>StyleBoxFlat</c> (filled vs ring): a <c>Online</c>
        /// circle is a filled green disc, <c>Connecting</c> is a green ring (transparent fill, 2px border), and
        /// <c>Disconnected</c> is a filled orange disc. Mirrors Unity-MCP's status-indicator classes.
        /// </summary>
        public enum TimelinePointState
        {
            /// <summary>Filled orange disc — not connected.</summary>
            Disconnected,

            /// <summary>Green ring (transparent fill, 2px border) — mid-handshake / retrying.</summary>
            Connecting,

            /// <summary>Filled green disc — connected.</summary>
            Online
        }

        /// <summary>
        /// Map the dock's <see cref="ConnectionStatus"/> to the timeline circle's
        /// <see cref="TimelinePointState"/>. The "Godot" and "MCP server" points both track this single hub
        /// state (one connection); the editor paints the returned state as a filled disc or a ring.
        /// </summary>
        public static TimelinePointState PointState(ConnectionStatus status) => status switch
        {
            ConnectionStatus.Connected => TimelinePointState.Online,
            ConnectionStatus.Connecting => TimelinePointState.Connecting,
            _ => TimelinePointState.Disconnected
        };

        /// <summary>
        /// The AI-agent timeline point's label. With no connected agent it reads
        /// "AI agent (connects on demand)"; when an agent is connected the label carries its
        /// <paramref name="clientName"/> and (when present) <paramref name="clientVersion"/> — e.g.
        /// "AI agent: Claude (1.2.0)" or "AI agent: Cursor". Whitespace-only names are treated as none.
        /// Mirrors Unity-MCP's "AI agent: &lt;client&gt; (&lt;ver&gt;)" line.
        /// </summary>
        public static string AgentLabel(string? clientName, string? clientVersion)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                return "AI agent (connects on demand)";

            return string.IsNullOrWhiteSpace(clientVersion)
                ? $"AI agent: {clientName!.Trim()}"
                : $"AI agent: {clientName!.Trim()} ({clientVersion!.Trim()})";
        }

        // --- Alert panels (amber WarningFrame). Pure-managed visibility rules, unit-tested. ---

        /// <summary>Title of the Cloud-mode "no token yet" alert.</summary>
        public const string AuthorizationRequiredTitle = "Authorization Required";

        /// <summary>Message body of the "Authorization Required" alert.</summary>
        public const string AuthorizationRequiredMessage =
            "Cloud mode needs an access token. Press Authorize to sign in.";

        /// <summary>Title of the "authorized but not connected" alert.</summary>
        public const string ConnectionRequiredTitle = "Connection Required";

        /// <summary>Message body of the "Connection Required" alert.</summary>
        public const string ConnectionRequiredMessage =
            "Not connected to the MCP server. Press Connect to establish the connection.";

        /// <summary>
        /// True when the "Authorization Required" alert should show: the live mode is Cloud and no cloud
        /// token is stored yet (the user must Authorize before a connection can succeed). Pure rule so the
        /// editor only decides VISIBILITY here — the alert chrome is built in <c>DockStyle.WarningFrame</c>.
        /// </summary>
        public static bool ShowAuthorizationRequired(bool isCloudMode, bool hasCloudToken) =>
            isCloudMode && !hasCloudToken;

        /// <summary>
        /// True when the "Connection Required" alert should show: the user is otherwise ready (in Custom
        /// mode, or in Cloud mode WITH a token) but the live connection is not <see cref="ConnectionStatus.Connected"/>.
        /// Suppressed in Cloud-mode-without-token (the "Authorization Required" alert owns that case) and while
        /// <see cref="ConnectionStatus.Connecting"/> (a connection attempt is already underway).
        /// </summary>
        public static bool ShowConnectionRequired(bool isCloudMode, bool hasCloudToken, ConnectionStatus status)
        {
            // Authorization alert owns the cloud-no-token case; don't double-alert.
            if (ShowAuthorizationRequired(isCloudMode, hasCloudToken))
                return false;

            // Only nag when fully disconnected — a Connecting attempt is already in flight.
            return status == ConnectionStatus.Disconnected;
        }
    }
}
