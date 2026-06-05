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
    /// Covers the pure-managed cloud device-code auth core (<see cref="GodotDeviceAuthFlow"/> +
    /// <see cref="GodotDeviceAuthService"/>) with a scripted <see cref="HttpMessageHandler"/> and an
    /// injected no-op delay + clock so the polling loop runs instantly (never the real 5s interval).
    ///
    /// <para>
    /// SECURITY assertion threaded through the suite: the issued access token must NEVER appear in any
    /// flow <see cref="GodotDeviceAuthFlow.ErrorMessage"/>, <see cref="GodotDeviceAuthFlow.UserCode"/>, or
    /// the verification URL. The token is observable ONLY as the <c>StartAsync</c> return value.
    /// </para>
    /// </summary>
    public class GodotDeviceAuthFlowTests
    {
        const string CloudBaseUrl = "https://ai-game.dev";
        const string AccessToken = "super-secret-access-token-value";

        // --- Happy path: issuance + immediate token ---

        [Fact]
        public async Task StartAsync_ImmediateToken_ReachesAuthorized_AndReturnsToken()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(userCode: "WXYZ-1234"),
                TokenOk(AccessToken));

            var (flow, states) = MakeFlow(handler);

            var token = await flow.StartAsync(CloudBaseUrl, "Godot Editor");

            Assert.Equal(AccessToken, token);
            Assert.Equal(GodotDeviceAuthFlowState.Authorized, flow.State);
            Assert.Equal("WXYZ-1234", flow.UserCode);
            Assert.Equal($"{CloudBaseUrl}/verify?code=WXYZ-1234", flow.VerificationUriComplete);

            // State events fire in order: Initiating -> WaitingForUser -> Polling -> Authorized.
            Assert.Equal(new[]
            {
                GodotDeviceAuthFlowState.Initiating,
                GodotDeviceAuthFlowState.WaitingForUser,
                GodotDeviceAuthFlowState.Polling,
                GodotDeviceAuthFlowState.Authorized
            }, states);

            AssertTokenNotLeaked(flow);
        }

        [Fact]
        public async Task InitiateDeviceAuth_ParsesAllFields()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(userCode: "AAAA-BBBB", deviceCode: "dev-code-123", interval: 5, expiresIn: 600),
                TokenOk(AccessToken));

            var service = new GodotDeviceAuthService(new HttpClient(handler));
            var response = await service.InitiateDeviceAuthAsync(CloudBaseUrl, "Godot Editor");

            Assert.Equal("dev-code-123", response.DeviceCode);
            Assert.Equal("AAAA-BBBB", response.UserCode);
            Assert.Equal(5, response.Interval);
            Assert.Equal(600, response.ExpiresIn);
            Assert.Equal($"{CloudBaseUrl}/verify?code=AAAA-BBBB", response.VerificationUriComplete);
        }

        // --- authorization_pending keeps polling, then succeeds ---

        [Fact]
        public async Task StartAsync_PendingThenToken_KeepsPolling_ThenAuthorized()
        {
            var handler = new ScriptedHandler(
                AuthorizeOk(),
                TokenError("authorization_pending"),
                TokenError("authorization_pending"),
                TokenOk(AccessToken));

            var (flow, _) = MakeFlow(handler);

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Equal(AccessToken, token);
            Assert.Equal(GodotDeviceAuthFlowState.Authorized, flow.State);
            // 1 authorize + 3 token polls.
            Assert.Equal(4, handler.RequestCount);
            AssertTokenNotLeaked(flow);
        }

        // --- slow_down backs the interval off ---

        [Fact]
        public async Task StartAsync_SlowDown_BacksOffInterval()
        {
            var delays = new List<TimeSpan>();
            var handler = new ScriptedHandler(
                AuthorizeOk(interval: 5),
                TokenError("slow_down"),
                TokenError("slow_down"),
                TokenOk(AccessToken));

            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (ts, _) => { delays.Add(ts); return Task.CompletedTask; },
                utcNow: FixedClock());

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Equal(AccessToken, token);
            // Interval starts at 5s, +5 per slow_down (capped at 30): poll1=5, poll2=10, poll3=15.
            Assert.Equal(3, delays.Count);
            Assert.Equal(TimeSpan.FromSeconds(5), delays[0]);
            Assert.Equal(TimeSpan.FromSeconds(10), delays[1]);
            Assert.Equal(TimeSpan.FromSeconds(15), delays[2]);
        }

        [Fact]
        public async Task StartAsync_SlowDown_IntervalCapsAtMax()
        {
            var delays = new List<TimeSpan>();
            // 6 slow_downs would push 5 -> 35 uncapped; the cap holds it at 30.
            var responses = new List<Func<HttpResponseMessage>> { AuthorizeOk(interval: 5) };
            for (var i = 0; i < 6; i++)
                responses.Add(TokenError("slow_down"));
            responses.Add(TokenOk(AccessToken));

            var flow = new GodotDeviceAuthFlow(
                new GodotDeviceAuthService(new HttpClient(new ScriptedHandler(responses.ToArray()))),
                delay: (ts, _) => { delays.Add(ts); return Task.CompletedTask; },
                utcNow: FixedClock());

            await flow.StartAsync(CloudBaseUrl);

            Assert.Equal(TimeSpan.FromSeconds(GodotDeviceAuthFlow.MaxIntervalSeconds), delays[^1]);
            Assert.All(delays, d => Assert.True(d <= TimeSpan.FromSeconds(GodotDeviceAuthFlow.MaxIntervalSeconds)));
        }

        // --- access_denied -> Failed ---

        [Fact]
        public async Task StartAsync_AccessDenied_ReachesFailed()
        {
            var handler = new ScriptedHandler(AuthorizeOk(), TokenError("access_denied"));
            var (flow, _) = MakeFlow(handler);

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Null(token);
            Assert.Equal(GodotDeviceAuthFlowState.Failed, flow.State);
            Assert.False(string.IsNullOrEmpty(flow.ErrorMessage));
            AssertTokenNotLeaked(flow);
        }

        // --- expired_token -> Expired ---

        [Fact]
        public async Task StartAsync_ExpiredToken_ReachesExpired()
        {
            var handler = new ScriptedHandler(AuthorizeOk(), TokenError("expired_token"));
            var (flow, _) = MakeFlow(handler);

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Null(token);
            Assert.Equal(GodotDeviceAuthFlowState.Expired, flow.State);
        }

        // --- deadline reached (expires_in elapses) -> Expired ---

        [Fact]
        public async Task StartAsync_DeadlineElapses_ReachesExpired()
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

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Null(token);
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

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Null(token);
            Assert.Equal(GodotDeviceAuthFlowState.Cancelled, flow.State);
            AssertTokenNotLeaked(flow);
        }

        // --- HTTP failure on authorize -> Failed, no token in message ---

        [Fact]
        public async Task StartAsync_AuthorizeHttp500_ReachesFailed_NoTokenLeak()
        {
            var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom")
            });
            var (flow, _) = MakeFlow(handler);

            var token = await flow.StartAsync(CloudBaseUrl);

            Assert.Null(token);
            Assert.Equal(GodotDeviceAuthFlowState.Failed, flow.State);
            AssertTokenNotLeaked(flow);
        }

        // --- Token is never exposed anywhere except the return value ---

        [Fact]
        public async Task StartAsync_Authorized_TokenNotInAnyStringSurface()
        {
            var handler = new ScriptedHandler(AuthorizeOk(userCode: "CODE-9999"), TokenOk(AccessToken));
            var (flow, states) = MakeFlow(handler);

            var token = await flow.StartAsync(CloudBaseUrl);
            Assert.Equal(AccessToken, token);

            // Token must not appear in UserCode / VerificationUriComplete / ErrorMessage, nor in any
            // pure-managed status message the UI would render for the observed states.
            AssertTokenNotLeaked(flow);
            foreach (var s in states)
            {
                var msg = UI.ConnectionPanelView.CloudAuthStatusMessage(s, flow.UserCode, flow.ErrorMessage);
                Assert.DoesNotContain(AccessToken, msg, StringComparison.Ordinal);
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

        static void AssertTokenNotLeaked(GodotDeviceAuthFlow flow)
        {
            Assert.True(string.IsNullOrEmpty(flow.UserCode) || !flow.UserCode!.Contains(AccessToken, StringComparison.Ordinal));
            Assert.True(string.IsNullOrEmpty(flow.ErrorMessage) || !flow.ErrorMessage!.Contains(AccessToken, StringComparison.Ordinal));
            Assert.True(string.IsNullOrEmpty(flow.VerificationUriComplete) || !flow.VerificationUriComplete!.Contains(AccessToken, StringComparison.Ordinal));
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

        static Func<HttpResponseMessage> TokenOk(string accessToken)
            => () => JsonResponse(HttpStatusCode.OK, $$"""
                { "access_token": "{{accessToken}}", "token_type": "Bearer" }
                """);

        static Func<HttpResponseMessage> TokenError(string error)
            => () => JsonResponse(HttpStatusCode.BadRequest, $$"""
                { "error": "{{error}}", "error_description": "{{error}} description" }
                """);

        static HttpResponseMessage JsonResponse(HttpStatusCode code, string json)
            => new(code) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

        /// <summary>
        /// A test <see cref="HttpMessageHandler"/> that replays a scripted sequence of responses. The first
        /// scripted response answers the authorize POST; each subsequent one answers a token POST. An
        /// optional <see cref="OnTokenRequest"/> hook fires before each token response (used to cancel mid-poll).
        /// </summary>
        sealed class ScriptedHandler : HttpMessageHandler
        {
            readonly Queue<Func<HttpResponseMessage>> _responses;

            public ScriptedHandler(params Func<HttpResponseMessage>[] responses)
            {
                _responses = new Queue<Func<HttpResponseMessage>>(responses);
            }

            public int RequestCount { get; private set; }

            /// <summary>Invoked just before each token-endpoint response is produced (authorize is skipped).</summary>
            public Action? OnTokenRequest { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequestCount++;

                var isToken = request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal);
                if (isToken)
                    OnTokenRequest?.Invoke();

                cancellationToken.ThrowIfCancellationRequested();

                var next = _responses.Count > 0 ? _responses.Dequeue() : (() => JsonResponse(HttpStatusCode.BadRequest, "{ \"error\": \"authorization_pending\" }"));
                return Task.FromResult(next());
            }
        }
    }
}
