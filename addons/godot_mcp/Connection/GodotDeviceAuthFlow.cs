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
using System.Threading;
using System.Threading.Tasks;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>The state machine states of a single device-authorization run.</summary>
    public enum GodotDeviceAuthFlowState
    {
        /// <summary>No flow has started yet (fresh instance).</summary>
        Idle,

        /// <summary>The authorize request is in flight.</summary>
        Initiating,

        /// <summary>The server returned a user code; the user must visit the verification URL and approve.</summary>
        WaitingForUser,

        /// <summary>Polling the token endpoint, waiting for the user's approval to land.</summary>
        Polling,

        /// <summary>An access token was issued — the flow succeeded.</summary>
        Authorized,

        /// <summary>The authorization was denied, or an unexpected error occurred.</summary>
        Failed,

        /// <summary>The device code or the overall flow deadline expired before approval.</summary>
        Expired,

        /// <summary>The flow was cancelled (by <see cref="GodotDeviceAuthFlow.Cancel"/> or a cancellation token).</summary>
        Cancelled
    }

    /// <summary>
    /// Drives a single OAuth 2.0 device-authorization-grant run end-to-end: initiate → wait-for-user →
    /// poll → terminal state. The Godot analog of Unity-MCP's <c>DeviceAuthFlow</c>, but engine-free:
    /// it contains NO Godot types (no <c>Application.OpenURL</c>, no token persistence) so it is
    /// unit-testable in the plain-xUnit host with a mockable <see cref="GodotDeviceAuthService"/>. The UI
    /// layer (the <c>#if TOOLS</c> <c>ConnectionPanel</c>) opens the browser on
    /// <see cref="GodotDeviceAuthFlowState.WaitingForUser"/> and persists the token on
    /// <see cref="GodotDeviceAuthFlowState.Authorized"/>.
    ///
    /// <para>
    /// SECURITY: the issued access token is returned from <see cref="StartAsync"/> for the caller to
    /// persist — it is NEVER stored in <see cref="ErrorMessage"/>, logged, or otherwise surfaced as a
    /// human-readable string. <see cref="ErrorMessage"/> only ever holds non-secret diagnostic text.
    /// </para>
    /// </summary>
    public sealed class GodotDeviceAuthFlow
    {
        /// <summary>Minimum poll interval (seconds) the server's <c>interval</c> is clamped up to — the device-flow floor.</summary>
        public const int MinIntervalSeconds = 5;

        /// <summary>Increment (seconds) added to the interval on a <c>slow_down</c> server hint.</summary>
        public const int SlowDownIncrementSeconds = 5;

        /// <summary>Ceiling (seconds) the interval backs off to under repeated <c>slow_down</c> hints.</summary>
        public const int MaxIntervalSeconds = 30;

        readonly GodotDeviceAuthService _service;
        readonly Func<TimeSpan, CancellationToken, Task> _delay;
        readonly Func<DateTime> _utcNow;

        CancellationTokenSource? _cts;

        /// <summary>The current state. Starts at <see cref="GodotDeviceAuthFlowState.Idle"/>.</summary>
        public GodotDeviceAuthFlowState State { get; private set; } = GodotDeviceAuthFlowState.Idle;

        /// <summary>The short user code to display once <see cref="GodotDeviceAuthFlowState.WaitingForUser"/> is reached.</summary>
        public string? UserCode { get; private set; }

        /// <summary>The full verification URL (with the code embedded) for the UI to open in a browser. NOT a secret.</summary>
        public string? VerificationUriComplete { get; private set; }

        /// <summary>Non-secret diagnostic message for a Failed / Expired terminal state. NEVER contains a token.</summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>Raised on every <see cref="State"/> transition. Subscribers must marshal to the UI thread themselves.</summary>
        public event Action<GodotDeviceAuthFlowState>? OnStateChanged;

        /// <summary>
        /// Construct a flow. <paramref name="service"/> defaults to a fresh
        /// <see cref="GodotDeviceAuthService"/> over the shared HttpClient. <paramref name="delay"/> and
        /// <paramref name="utcNow"/> are injectable for fast, deterministic tests (the editor path uses the
        /// real <see cref="Task.Delay(TimeSpan, CancellationToken)"/> + <see cref="DateTime.UtcNow"/>).
        /// </summary>
        public GodotDeviceAuthFlow(
            GodotDeviceAuthService? service = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null,
            Func<DateTime>? utcNow = null)
        {
            _service = service ?? new GodotDeviceAuthService();
            _delay = delay ?? Task.Delay;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Run the device flow against <paramref name="cloudBaseUrl"/>. Returns the issued access token on
        /// success, or <c>null</c> on any non-Authorized terminal state. The token is the ONLY channel the
        /// secret leaves through — it is never logged or placed in <see cref="ErrorMessage"/>. A second
        /// call cancels any in-flight run first.
        /// </summary>
        public async Task<string?> StartAsync(string cloudBaseUrl, string? clientLabel = null)
        {
            Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                SetState(GodotDeviceAuthFlowState.Initiating);

                var authResponse = await _service
                    .InitiateDeviceAuthAsync(cloudBaseUrl, clientLabel, ct)
                    .ConfigureAwait(false);

                UserCode = authResponse.UserCode;
                VerificationUriComplete = authResponse.VerificationUriComplete;

                SetState(GodotDeviceAuthFlowState.WaitingForUser);
                SetState(GodotDeviceAuthFlowState.Polling);

                var intervalSeconds = Math.Max(authResponse.Interval, MinIntervalSeconds);
                var deadline = _utcNow().AddSeconds(authResponse.ExpiresIn);

                while (_utcNow() < deadline)
                {
                    ct.ThrowIfCancellationRequested();
                    await _delay(TimeSpan.FromSeconds(intervalSeconds), ct).ConfigureAwait(false);

                    var tokenResponse = await _service
                        .PollDeviceTokenAsync(cloudBaseUrl, authResponse.DeviceCode, ct)
                        .ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
                    {
                        // Reach the terminal state BEFORE returning the token, but never store the token
                        // on this instance — it flows out only as the return value.
                        SetState(GodotDeviceAuthFlowState.Authorized);
                        return tokenResponse.AccessToken;
                    }

                    switch (tokenResponse.Error)
                    {
                        case "access_denied":
                            ErrorMessage = "Authorization was denied.";
                            SetState(GodotDeviceAuthFlowState.Failed);
                            return null;

                        case "expired_token":
                            SetState(GodotDeviceAuthFlowState.Expired);
                            return null;

                        case "slow_down":
                            intervalSeconds = Math.Min(intervalSeconds + SlowDownIncrementSeconds, MaxIntervalSeconds);
                            break;

                        // "authorization_pending" (and any other transient/unknown error) — keep polling.
                        default:
                            break;
                    }
                }

                SetState(GodotDeviceAuthFlowState.Expired);
                return null;
            }
            catch (OperationCanceledException)
            {
                SetState(GodotDeviceAuthFlowState.Cancelled);
                return null;
            }
            catch (Exception ex)
            {
                // ex.Message comes from HTTP/JSON failures — never from a token, so it is safe to surface.
                ErrorMessage = ex.Message;
                SetState(GodotDeviceAuthFlowState.Failed);
                return null;
            }
        }

        /// <summary>Cancel any in-flight run; the active <see cref="StartAsync"/> settles on <see cref="GodotDeviceAuthFlowState.Cancelled"/>.</summary>
        public void Cancel()
        {
            var cts = _cts;
            _cts = null;
            if (cts != null)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                cts.Dispose();
            }
        }

        /// <summary>True while the flow is mid-run (the Authorize button shows "Cancel" in these states).</summary>
        public static bool IsRunning(GodotDeviceAuthFlowState state) =>
            state == GodotDeviceAuthFlowState.Initiating
            || state == GodotDeviceAuthFlowState.WaitingForUser
            || state == GodotDeviceAuthFlowState.Polling;

        void SetState(GodotDeviceAuthFlowState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
