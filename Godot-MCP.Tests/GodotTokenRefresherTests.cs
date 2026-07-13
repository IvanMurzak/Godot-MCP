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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Covers <see cref="GodotTokenRefresher"/> — the Godot <c>ITokenRefresher</c> that exchanges a refresh
    /// token for a fresh access token at <c>POST /oauth/token</c> (<c>grant_type=refresh_token</c>). Verifies
    /// the success mapping (access + rotated refresh + expiry), the fail-closed behaviour (error body / HTTP
    /// fault → <c>Failure</c>, never a fabricated token), the RFC-6749 form shape, and the server-target →
    /// AS-base resolution (a hub <c>/mcp</c> suffix is stripped). Never asserts a token into a log surface.
    /// </summary>
    public class GodotTokenRefresherTests
    {
        const string AsBaseUrl = "https://ai-game.dev";
        const string NewAccess = "fresh-access-token";
        const string NewRefresh = "rotated-refresh-token";
        static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        static GodotTokenRefresher MakeRefresher(RecordingHandler handler, Func<string>? defaultBase = null)
            => new(
                new GodotDeviceAuthService(new HttpClient(handler)),
                defaultBase ?? (() => AsBaseUrl),
                clientId: GodotDeviceAuthFlow.DefaultClientId,
                clock: () => T0);

        [Fact]
        public async Task RefreshAsync_Success_MapsAccessRefreshAndExpiry()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh, expiresIn: 3600));
            var refresher = MakeRefresher(handler);

            var result = await refresher.RefreshAsync("old-refresh", serverTarget: null);

            Assert.True(result.Succeeded);
            Assert.Equal(NewAccess, result.AccessToken);
            Assert.Equal(NewRefresh, result.RefreshToken);
            Assert.Equal(T0.AddSeconds(3600), result.ExpiresAt);
        }

        [Fact]
        public async Task RefreshAsync_SendsRefreshTokenGrantForm()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            var refresher = MakeRefresher(handler);

            await refresher.RefreshAsync("old-refresh", serverTarget: null);

            Assert.Equal($"{AsBaseUrl}/oauth/token", handler.LastRequestUri);
            Assert.Contains("grant_type=refresh_token", handler.LastBody);
            Assert.Contains("refresh_token=old-refresh", handler.LastBody);
            Assert.Contains($"client_id={GodotDeviceAuthFlow.DefaultClientId}", handler.LastBody);
        }

        [Fact]
        public async Task RefreshAsync_ServerTargetHubUrl_StripsMcpSuffixForAsBase()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            // The default base is intentionally different, to prove serverTarget wins.
            var refresher = MakeRefresher(handler, defaultBase: () => "https://should-not-be-used.example");

            await refresher.RefreshAsync("old-refresh", serverTarget: "https://ai-game.dev/mcp");

            Assert.Equal($"{AsBaseUrl}/oauth/token", handler.LastRequestUri);
        }

        [Fact]
        public async Task RefreshAsync_NullServerTarget_UsesDefaultBase()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            var refresher = MakeRefresher(handler, defaultBase: () => "https://local-as.example");

            await refresher.RefreshAsync("old-refresh", serverTarget: null);

            Assert.Equal("https://local-as.example/oauth/token", handler.LastRequestUri);
        }

        [Fact]
        public async Task RefreshAsync_ErrorBody_FailsClosedWithReason()
        {
            var handler = new RecordingHandler(() => JsonResponse(HttpStatusCode.BadRequest,
                "{ \"error\": \"invalid_grant\", \"error_description\": \"refresh token expired\" }"));
            var refresher = MakeRefresher(handler);

            var result = await refresher.RefreshAsync("expired-refresh", serverTarget: null);

            Assert.False(result.Succeeded);
            Assert.Null(result.AccessToken);
            Assert.Equal("invalid_grant", result.FailureReason);
        }

        [Fact]
        public async Task RefreshAsync_HttpFault_FailsClosed()
        {
            var handler = new RecordingHandler(() => throw new HttpRequestException("connection refused"));
            var refresher = MakeRefresher(handler);

            var result = await refresher.RefreshAsync("some-refresh", serverTarget: null);

            Assert.False(result.Succeeded);
            Assert.Null(result.AccessToken);
        }

        [Fact]
        public async Task RefreshAsync_Cancellation_Propagates()
        {
            var handler = new RecordingHandler(() => throw new OperationCanceledException());
            var refresher = MakeRefresher(handler);

            // A pre-canceled token makes HttpClient throw TaskCanceledException (a subclass); assert ANY
            // OperationCanceledException so cancellation propagates rather than collapsing into a Failure.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => refresher.RefreshAsync("some-refresh", serverTarget: null, new CancellationToken(canceled: true)));
        }

        // --- helpers ---

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

        static HttpResponseMessage JsonResponse(HttpStatusCode code, string json)
            => new(code) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

        /// <summary>A handler that records the last request URI + body and returns (or throws) one scripted response.</summary>
        sealed class RecordingHandler : HttpMessageHandler
        {
            readonly Func<HttpResponseMessage> _respond;

            public RecordingHandler(Func<HttpResponseMessage> respond) => _respond = respond;

            public string? LastRequestUri { get; private set; }
            public string LastBody { get; private set; } = "";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastRequestUri = request.RequestUri!.GetLeftPart(UriPartial.Path);
                LastBody = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
                return _respond();
            }
        }
    }
}
