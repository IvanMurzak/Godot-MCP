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
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Covers the pure-managed RFC 8628 device-code auth core (<see cref="GodotDeviceAuthFlow"/> +
    /// <see cref="GodotDeviceAuthService"/>) with a scripted <see cref="HttpMessageHandler"/> and an
    /// injected no-op delay + clock so the polling loop runs instantly (never the real 5s interval).
    /// The transport is the ai-game.dev alias: <c>POST /oauth/device_authorization</c> (form
    /// <c>client_id</c>+<c>scope</c>) then poll <c>POST /oauth/token</c> (device-code grant) → access +
    /// rotating refresh token + expiry.
    ///
    /// <para>
    /// SECURITY assertion threaded through the suite: the issued access/refresh token must NEVER appear in any
    /// flow <see cref="GodotDeviceAuthFlow.ErrorMessage"/>, <see cref="GodotDeviceAuthFlow.UserCode"/>, or
    /// the verification URL. The secrets are observable ONLY as the <c>AuthorizeAsync</c>/<c>StartAsync</c>
    /// return value.
    /// </para>
    /// </summary>
    public class GodotDeviceAuthFlowTests
    {
        const string AsBaseUrl = "https://ai-game.dev";
        const string AccessToken = "super-secret-access-token-value";
        const string RefreshToken = "super-secret-refresh-token-value";

        // --- Happy path: issuance + immediate token, full credential surfaced ---

        [Fact]
        public async Task AuthorizeAsync_ImmediateToken_ReachesAuthorized_AndReturnsFullCredential()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(userCode: "WXYZ-1234"),
                TokenOk(AccessToken, RefreshToken, expiresIn: 3600));

            var (flow, states) = MakeFlow(handler);

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.NotNull(result);
            Assert.Equal(AccessToken, result!.AccessToken);
            Assert.Equal(RefreshToken, result.RefreshToken);
            // FixedClock is t0 = 2026-01-01T00:00:00Z; expires_in 3600 → t0 + 1h.
            Assert.Equal(new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero), result.ExpiresAt);

            Assert.Equal(GodotDeviceAuthFlowState.Authorized, flow.State);
            Assert.Equal("WXYZ-1234", flow.UserCode);
            Assert.Equal($"{AsBaseUrl}/verify?code=WXYZ-1234", flow.VerificationUriComplete);

            // State events fire in order: Initiating -> WaitingForUser -> Polling -> Authorized.
            Assert.Equal(new[]
            {
                GodotDeviceAuthFlowState.Initiating,
                GodotDeviceAuthFlowState.WaitingForUser,
                GodotDeviceAuthFlowState.Polling,
                GodotDeviceAuthFlowState.Authorized
            }, states);

            AssertSecretsNotLeaked(flow);
        }

        [Fact]
        public async Task StartAsync_ImmediateToken_ReturnsAccessTokenOnly()
        {
            var handler = new ScriptedHandler(AuthorizeOk(), TokenOk(AccessToken, RefreshToken));
            var (flow, _) = MakeFlow(handler);

            var token = await flow.StartAsync(AsBaseUrl, "Godot Editor");

            Assert.Equal(AccessToken, token);
            Assert.Equal(GodotDeviceAuthFlowState.Authorized, flow.State);
        }

        // --- The RFC 8628 request shape: form-encoded client_id + scope, device-code grant ---

        [Fact]
        public async Task AuthorizeAsync_SendsRfc8628FormFields()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(deviceCode: "dev-code-123"),
                TokenOk(AccessToken, RefreshToken));
            var (flow, _) = MakeFlow(handler);

            await flow.AuthorizeAsync(AsBaseUrl);

            // Authorize request: POST /oauth/device_authorization with client_id + scope=mcp:plugin.
            var authorize = handler.Requests[0];
            Assert.EndsWith("/oauth/device_authorization", authorize.Path);
            Assert.Contains($"client_id={GodotDeviceAuthFlow.DefaultClientId}", authorize.Body);
            Assert.Contains("scope=mcp%3Aplugin", authorize.Body); // "mcp:plugin" url-encoded

            // Token request: POST /oauth/token with the device-code grant + same client_id + device_code.
            var token = handler.Requests[1];
            Assert.EndsWith("/oauth/token", token.Path);
            Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", token.Body);
            Assert.Contains("device_code=dev-code-123", token.Body);
            Assert.Contains($"client_id={GodotDeviceAuthFlow.DefaultClientId}", token.Body);
        }

        [Fact]
        public async Task RequestDeviceAuthorization_ParsesAllFields()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(userCode: "AAAA-BBBB", deviceCode: "dev-code-123", interval: 5, expiresIn: 600),
                TokenOk(AccessToken, RefreshToken));

            var service = new GodotDeviceAuthService(new HttpClient(handler));
            var response = await service.RequestDeviceAuthorizationAsync(
                AsBaseUrl, GodotDeviceAuthFlow.DefaultClientId, GodotDeviceAuthFlow.PluginScope);

            Assert.Equal("dev-code-123", response.DeviceCode);
            Assert.Equal("AAAA-BBBB", response.UserCode);
            Assert.Equal(5, response.Interval);
            Assert.Equal(600, response.ExpiresIn);
            Assert.Equal($"{AsBaseUrl}/verify?code=AAAA-BBBB", response.VerificationUriComplete);
        }

        // --- authorization_pending keeps polling, then succeeds ---

        [Fact]
        public async Task AuthorizeAsync_PendingThenToken_KeepsPolling_ThenAuthorized()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(),
                TokenError("authorization_pending"),
                TokenError("authorization_pending"),
                TokenOk(AccessToken, RefreshToken));

            var (flow, _) = MakeFlow(handler);

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Equal(AccessToken, result!.AccessToken);
            Assert.Equal(GodotDeviceAuthFlowState.Authorized, flow.State);
            // 1 authorize + 3 token polls.
            Assert.Equal(4, handler.RequestCount);
            AssertSecretsNotLeaked(flow);
        }

        // --- slow_down backs the interval off ---

        [Fact]
        public async Task AuthorizeAsync_SlowDown_BacksOffInterval()
        {
            var delays = new List<TimeSpan>();
            var handler = new ScriptedHandler(
                AuthorizeOk(interval: 5),
                TokenError("slow_down"),
                TokenError("slow_down"),
                TokenOk(AccessToken, RefreshToken));

            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (ts, _) => { delays.Add(ts); return Task.CompletedTask; },
                utcNow: FixedClock());

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Equal(AccessToken, result!.AccessToken);
            // Interval starts at 5s, +5 per slow_down (capped at 30): poll1=5, poll2=10, poll3=15.
            Assert.Equal(3, delays.Count);
            Assert.Equal(TimeSpan.FromSeconds(5), delays[0]);
            Assert.Equal(TimeSpan.FromSeconds(10), delays[1]);
            Assert.Equal(TimeSpan.FromSeconds(15), delays[2]);
        }

        [Fact]
        public async Task AuthorizeAsync_SlowDown_IntervalCapsAtMax()
        {
            var delays = new List<TimeSpan>();
            // 6 slow_downs would push 5 -> 35 uncapped; the cap holds it at 30.
            var responses = new List<Func<HttpResponseMessage>> { AuthorizeOk(interval: 5) };
            for (var i = 0; i < 6; i++)
                responses.Add(TokenError("slow_down"));
            responses.Add(TokenOk(AccessToken, RefreshToken));

            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(new ScriptedHandler(responses.ToArray()))),
                delay: (ts, _) => { delays.Add(ts); return Task.CompletedTask; },
                utcNow: FixedClock());

            await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Equal(TimeSpan.FromSeconds(GodotDeviceAuthFlow.MaxIntervalSeconds), delays[^1]);
            Assert.All(delays, d => Assert.True(d <= TimeSpan.FromSeconds(GodotDeviceAuthFlow.MaxIntervalSeconds)));
        }

        // --- access_denied -> Failed ---

        [Fact]
        public async Task AuthorizeAsync_AccessDenied_ReachesFailed()
        {
            var handler = new ScriptedHandler(AuthorizeOk(), TokenError("access_denied"));
            var (flow, _) = MakeFlow(handler);

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Null(result);
            Assert.Equal(GodotDeviceAuthFlowState.Failed, flow.State);
            Assert.False(string.IsNullOrEmpty(flow.ErrorMessage));
            AssertSecretsNotLeaked(flow);
        }

        // --- expired_token -> Expired ---

        [Fact]
        public async Task AuthorizeAsync_ExpiredToken_ReachesExpired()
        {
            var handler = new ScriptedHandler(AuthorizeOk(), TokenError("expired_token"));
            var (flow, _) = MakeFlow(handler);

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Null(result);
            Assert.Equal(GodotDeviceAuthFlowState.Expired, flow.State);
        }

        // --- deadline reached (expires_in elapses) -> Expired ---

        [Fact]
        public async Task AuthorizeAsync_DeadlineElapses_ReachesExpired()
        {
            // expires_in = 10s; the fake clock jumps 20s on the first now() AFTER the deadline is computed,
            // so the while-loop condition is false before any poll. Pending response is present but unused.
            var handler = new ScriptedHandler(AuthorizeOk(expiresIn: 10), TokenError("authorization_pending"));

            // Clock: first call (deadline base) = t0; subsequent calls = t0 + 20s (past the 10s deadline).
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var calls = 0;
            Func<DateTime> clock = () => calls++ == 0 ? t0 : t0.AddSeconds(20);

            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (_, _) => Task.CompletedTask,
                utcNow: clock);

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Null(result);
            Assert.Equal(GodotDeviceAuthFlowState.Expired, flow.State);
        }

        // --- Cancel() -> Cancelled ---

        [Fact]
        public async Task Cancel_DuringPoll_ReachesCancelled()
        {
            GodotDeviceAuthFlow? flowRef = null;

            // Cancel the flow the moment the token endpoint is first hit (mid-poll), via the handler hook.
            var handler = new ScriptedHandler(AuthorizeOk(), TokenError("authorization_pending"))
            {
                OnTokenRequest = () => flowRef!.Cancel()
            };

            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: Task.Delay,
                utcNow: FixedClock());
            flowRef = flow;

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Null(result);
            Assert.Equal(GodotDeviceAuthFlowState.Cancelled, flow.State);
            AssertSecretsNotLeaked(flow);
        }

        // --- HTTP failure on authorize -> Failed, no token in message ---

        [Fact]
        public async Task AuthorizeAsync_AuthorizeHttp500_ReachesFailed_NoTokenLeak()
        {
            var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom")
            });
            var (flow, _) = MakeFlow(handler);

            var result = await flow.AuthorizeAsync(AsBaseUrl);

            Assert.Null(result);
            Assert.Equal(GodotDeviceAuthFlowState.Failed, flow.State);
            AssertSecretsNotLeaked(flow);
        }

        // --- Secrets are never exposed anywhere except the return value ---

        [Fact]
        public async Task AuthorizeAsync_Authorized_SecretsNotInAnyStringSurface()
        {
            var handler = new ScriptedHandler(AuthorizeOk(userCode: "CODE-9999"), TokenOk(AccessToken, RefreshToken));
            var (flow, states) = MakeFlow(handler);

            var result = await flow.AuthorizeAsync(AsBaseUrl);
            Assert.Equal(AccessToken, result!.AccessToken);

            // Neither secret must appear in UserCode / VerificationUriComplete / ErrorMessage, nor in any
            // pure-managed status message the UI would render for the observed states.
            AssertSecretsNotLeaked(flow);
            foreach (var s in states)
            {
                var msg = UI.ConnectionPanelView.CloudAuthStatusMessage(s, flow.UserCode, flow.ErrorMessage);
                Assert.DoesNotContain(AccessToken, msg, StringComparison.Ordinal);
                Assert.DoesNotContain(RefreshToken, msg, StringComparison.Ordinal);
            }
        }

        // --- helpers ---

        static (GodotDeviceAuthFlow flow, List<GodotDeviceAuthFlowState> states) MakeFlow(ScriptedHandler handler)
        {
            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (_, _) => Task.CompletedTask,
                utcNow: FixedClock());

            var states = new List<GodotDeviceAuthFlowState>();
            flow.OnStateChanged += s => states.Add(s);
            return (flow, states);
        }

        /// <summary>A clock that always returns the same early instant, so the expires_in deadline never elapses.</summary>
        static Func<DateTime> FixedClock()
        {
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return () => t0;
        }

        static void AssertSecretsNotLeaked(GodotDeviceAuthFlow flow)
        {
            foreach (var secret in new[] { AccessToken, RefreshToken })
            {
                Assert.True(string.IsNullOrEmpty(flow.UserCode) || !flow.UserCode!.Contains(secret, StringComparison.Ordinal));
                Assert.True(string.IsNullOrEmpty(flow.ErrorMessage) || !flow.ErrorMessage!.Contains(secret, StringComparison.Ordinal));
                Assert.True(string.IsNullOrEmpty(flow.VerificationUriComplete) || !flow.VerificationUriComplete!.Contains(secret, StringComparison.Ordinal));
            }
        }

        static Func<HttpResponseMessage> AuthorizeOk(
            string userCode = "USER-CODE", string deviceCode = "device-code", int interval = 5, int expiresIn = 600)
            => () => JsonResponse(HttpStatusCode.OK, $$"""
                {
                  "device_code": "{{deviceCode}}",
                  "user_code": "{{userCode}}",
                  "verification_uri": "https://ai-game.dev/verify",
                  "verification_uri_complete": "https://ai-game.dev/verify?code={{userCode}}",
                  "expires_in": {{expiresIn}},
                  "interval": {{interval}}
                }
                """);

        static Func<HttpResponseMessage> TokenOk(string accessToken, string refreshToken, int expiresIn = 3600)
            => () => JsonResponse(HttpStatusCode.OK, $$"""
                {
                  "access_token": "{{accessToken}}",
                  "refresh_token": "{{refreshToken}}",
                  "token_type": "Bearer",
                  "expires_in": {{expiresIn}},
                  "scope": "mcp:plugin"
                }
                """);

        static Func<HttpResponseMessage> TokenError(string error)
            => () => JsonResponse(HttpStatusCode.BadRequest, $$"""
                { "error": "{{error}}", "error_description": "{{error}} description" }
                """);

        static HttpResponseMessage JsonResponse(HttpStatusCode code, string json)
            => new(code) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

        /// <summary>
        /// A test <see cref="HttpMessageHandler"/> that replays a scripted sequence of responses and records
        /// each request's path + form body. The first scripted response answers the device-authorization POST;
        /// each subsequent one answers a token POST. An optional <see cref="OnTokenRequest"/> hook fires before
        /// each token response (used to cancel mid-poll).
        /// </summary>
        sealed class ScriptedHandler : HttpMessageHandler
        {
            readonly Queue<Func<HttpResponseMessage>> _responses;

            public ScriptedHandler(params Func<HttpResponseMessage>[] responses)
            {
                _responses = new Queue<Func<HttpResponseMessage>>(responses);
            }

            public int RequestCount { get; private set; }

            /// <summary>The recorded (absolute path, form body) of every request, in order.</summary>
            public List<(string Path, string Body)> Requests { get; } = new();

            /// <summary>Invoked just before each token-endpoint response is produced (authorize is skipped).</summary>
            public Action? OnTokenRequest { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequestCount++;

                var path = request.RequestUri!.AbsolutePath;
                var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
                Requests.Add((path, body));

                var isToken = path.EndsWith("/oauth/token", StringComparison.Ordinal);
                if (isToken)
                    OnTokenRequest?.Invoke();

                cancellationToken.ThrowIfCancellationRequested();

                var next = _responses.Count > 0 ? _responses.Dequeue() : (() => JsonResponse(HttpStatusCode.BadRequest, "{ \"error\": \"authorization_pending\" }"));
                return next();
            }
        }
    }
}
