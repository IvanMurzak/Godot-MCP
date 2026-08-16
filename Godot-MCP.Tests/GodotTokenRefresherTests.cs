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
using com.IvanMurzak.McpPlugin;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Covers <see cref="GodotTokenRefresher"/> — since unified-machine-auth f1 a THIN ADAPTER over the
    /// shared <see cref="HttpTokenRefresher"/> (McpPlugin 8.1, task b3). Verifies the 04 §3 wire contract
    /// the adapter must preserve end-to-end: <c>grant_type=refresh_token</c> at
    /// <c>&lt;target&gt;/oauth/token</c>, the family's STORED <c>clientId</c> presented verbatim (component
    /// default ONLY for the legacy no-context API), <c>scope</c>/<c>resource</c> omitted entirely, the
    /// success mapping (access + rotated refresh + expiry), fail-closed error handling, and the adapter's
    /// one local behavior — the LIVE default-AS-base resolution for target-less requests (with the shared
    /// <c>/mcp</c>-suffix strip for stored hub URLs). Never asserts a token into a log surface.
    /// </summary>
    public class GodotTokenRefresherTests
    {
        const string AsBaseUrl = "https://ai-game.dev";
        const string NewAccess = "fresh-access-token";
        const string NewRefresh = "rotated-refresh-token";

        static GodotTokenRefresher MakeRefresher(RecordingHandler handler, Func<string>? defaultBase = null)
            => new(
                defaultBase ?? (() => AsBaseUrl),
                clientId: GodotDeviceAuthFlow.DefaultClientId,
                httpClient: new HttpClient(handler));

        [Fact]
        public async Task RefreshAsync_Success_MapsAccessRefreshAndExpiry()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh, expiresIn: 3600));
            var refresher = MakeRefresher(handler);

            var before = DateTimeOffset.UtcNow;
            var result = await refresher.RefreshAsync("old-refresh", serverTarget: null);
            var after = DateTimeOffset.UtcNow;

            Assert.True(result.Succeeded);
            Assert.Equal(NewAccess, result.AccessToken);
            Assert.Equal(NewRefresh, result.RefreshToken);
            // The shared refresher stamps expiry off the real clock — assert the window, not an instant.
            Assert.NotNull(result.ExpiresAt);
            Assert.InRange(result.ExpiresAt!.Value, before.AddSeconds(3600), after.AddSeconds(3600));
        }

        [Fact]
        public async Task RefreshAsync_LegacyApi_SendsRefreshTokenGrantForm_WithComponentDefaultClientId()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            var refresher = MakeRefresher(handler);

            await refresher.RefreshAsync("old-refresh", serverTarget: null);

            Assert.Equal($"{AsBaseUrl}/oauth/token", handler.LastRequestUri);
            Assert.Contains("grant_type=refresh_token", handler.LastBody);
            Assert.Contains("refresh_token=old-refresh", handler.LastBody);
            // No family context on the legacy two-string API ⇒ the component default (04 §3.7).
            Assert.Contains($"client_id={GodotDeviceAuthFlow.DefaultClientId}", handler.LastBody);
        }

        /// <summary>
        /// 04 §3.2 (G-SEC-1 plant-3 discriminator): a family-aware request's STORED <c>clientId</c> reaches
        /// the wire verbatim — the adapter must never substitute the component default.
        /// </summary>
        [Fact]
        public async Task RefreshAsync_FamilyAware_PresentsTheStoredClientId()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            var refresher = MakeRefresher(handler);

            await refresher.RefreshAsync(
                new TokenRefreshRequest("old-refresh", serverTarget: null, clientId: "stored-custom-id"));

            Assert.Contains("client_id=stored-custom-id", handler.LastBody);
            Assert.DoesNotContain(GodotDeviceAuthFlow.DefaultClientId, handler.LastBody);
        }

        /// <summary>04 §3.3 / P0-3 (G-SEC-1 plant-3 discriminator): no <c>scope</c>, no <c>resource</c> — ever.</summary>
        [Fact]
        public async Task RefreshAsync_OmitsScopeAndResourceEntirely()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            var refresher = MakeRefresher(handler);

            await refresher.RefreshAsync(
                new TokenRefreshRequest("old-refresh", serverTarget: null, clientId: "stored-custom-id"));

            Assert.DoesNotContain("scope=", handler.LastBody);
            Assert.DoesNotContain("resource=", handler.LastBody);
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
        public async Task RefreshAsync_NullServerTarget_UsesTheLiveDefaultBase()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            // Read LIVE per call (a .env cloud-URL override applies without a rebuild): flip the value
            // between construction and the call to prove the resolution is not captured at construction.
            var liveBase = "https://constructed.example";
            var refresher = MakeRefresher(handler, defaultBase: () => liveBase);
            liveBase = "https://local-as.example";

            await refresher.RefreshAsync("old-refresh", serverTarget: null);

            Assert.Equal("https://local-as.example/oauth/token", handler.LastRequestUri);
        }

        [Fact]
        public async Task RefreshAsync_ErrorBody_FailsClosed_ClassifiedInvalidGrant()
        {
            var handler = new RecordingHandler(() => JsonResponse(HttpStatusCode.BadRequest,
                "{ \"error\": \"invalid_grant\", \"error_description\": \"refresh token expired\" }"));
            var refresher = MakeRefresher(handler);

            var result = await refresher.RefreshAsync("expired-refresh", serverTarget: null);

            Assert.False(result.Succeeded);
            Assert.Null(result.AccessToken);
            Assert.Contains("invalid_grant", result.FailureReason);
            Assert.Equal(TokenRefreshFailureKind.InvalidGrant, result.FailureKind);
        }

        [Fact]
        public async Task RefreshAsync_HttpFault_FailsClosed_AsTransient()
        {
            var handler = new RecordingHandler(() => throw new HttpRequestException("connection refused"));
            var refresher = MakeRefresher(handler);

            var result = await refresher.RefreshAsync("some-refresh", serverTarget: null);

            Assert.False(result.Succeeded);
            Assert.Null(result.AccessToken);
            Assert.Equal(TokenRefreshFailureKind.Transient, result.FailureKind);
        }

        [Fact]
        public async Task RefreshAsync_PreCanceledToken_Propagates()
        {
            var handler = new RecordingHandler(TokenOk(NewAccess, NewRefresh));
            var refresher = MakeRefresher(handler);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => refresher.RefreshAsync("some-refresh", serverTarget: null, new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task RefreshAsync_EmptyRefreshToken_FailsWithoutNetworkIo()
        {
            var handler = new RecordingHandler(() => throw new InvalidOperationException("unexpected network call"));
            var refresher = MakeRefresher(handler);

            var result = await refresher.RefreshAsync("", serverTarget: null);

            Assert.False(result.Succeeded);
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
