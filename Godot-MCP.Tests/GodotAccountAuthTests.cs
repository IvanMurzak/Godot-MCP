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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.McpPlugin.AgentConfig;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Covers <see cref="GodotAccountAuth"/> — the machine-store account coordinator (mcp-authorize D12).
    /// Verifies the three PR-2 behaviours against a temp-directory <see cref="MachineCredentialStore"/> + a
    /// fake <see cref="HttpMessageHandler"/>: boot auto-adopt (a pre-populated store leaves the coordinator
    /// signed in with no network I/O), sign-in persistence (the device flow's credential is written to the
    /// store), sign-out (the store is wiped), and reactive refresh (the refresh grant rotates the stored
    /// credential). No token is asserted into a log surface.
    /// </summary>
    public class GodotAccountAuthTests
    {
        const string AsBaseUrl = "https://ai-game.dev";
        static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        static readonly DateTime T0Dt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // --- Boot auto-adopt (the zero-button rule) ---

        [Fact]
        public void EmptyStore_NotSignedIn()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, new ThrowingHandler());

            Assert.False(account.IsSignedIn);
        }

        [Fact]
        public async Task EmptyStore_AccessTokenProvider_ReturnsNull()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, new ThrowingHandler());

            var token = await account.AccessTokenProvider();

            Assert.Null(token);
        }

        [Fact]
        public async Task PrePopulatedStore_AutoAdopts_WithoutNetworkIo()
        {
            using var tmp = new TempDir();
            // ExpiresAt = null → no proactive refresh is due, so the provider must return the stored token
            // WITHOUT touching the network (the ThrowingHandler proves it).
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "stored-access",
                RefreshToken = "stored-refresh",
                ExpiresAt = null,
                ServerTarget = AsBaseUrl,
            });

            using var account = MakeAccount(tmp, new ThrowingHandler());

            Assert.True(account.IsSignedIn);
            Assert.Equal("stored-access", await account.AccessTokenProvider());
        }

        // --- Sign-in persistence (device flow → machine store) ---

        [Fact]
        public async Task SignInAsync_Success_PersistsCredential_AndAnotherCoordinatorAutoAdopts()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, new ThrowingHandler());

            var flow = MakeFlow(new DeviceFlowHandler(
                Authorize("USER-1", "dev-1"),
                Token(access: "acc-1", refresh: "ref-1", expiresIn: 3600)));

            var ok = await account.SignInAsync(flow, AsBaseUrl);

            Assert.True(ok);
            Assert.True(account.IsSignedIn);

            var persisted = Store(tmp).Read();
            Assert.NotNull(persisted);
            Assert.Equal("acc-1", persisted!.AccessToken);
            Assert.Equal("ref-1", persisted.RefreshToken);
            Assert.Equal(AsBaseUrl, persisted.ServerTarget);
            Assert.Equal(T0.AddSeconds(3600), persisted.ExpiresAt);

            // A fresh coordinator over the SAME store auto-adopts (the once-per-machine sign-in seen by a
            // second editor session / another engine).
            using var account2 = MakeAccount(tmp, new ThrowingHandler());
            Assert.True(account2.IsSignedIn);
        }

        [Fact]
        public async Task SignInAsync_Denied_DoesNotPersist()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, new ThrowingHandler());

            var flow = MakeFlow(new DeviceFlowHandler(
                Authorize("USER-1", "dev-1"),
                Error("access_denied")));

            var ok = await account.SignInAsync(flow, AsBaseUrl);

            Assert.False(ok);
            Assert.False(account.IsSignedIn);
            Assert.False(Store(tmp).Exists);
        }

        // --- Sign-out ---

        [Fact]
        public void SignOut_WipesStore()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials { AccessToken = "a", RefreshToken = "r", ServerTarget = AsBaseUrl });

            using var account = MakeAccount(tmp, new ThrowingHandler());
            Assert.True(account.IsSignedIn);

            account.SignOut();

            Assert.False(account.IsSignedIn);
            Assert.False(Store(tmp).Exists);
            using var account2 = MakeAccount(tmp, new ThrowingHandler());
            Assert.False(account2.IsSignedIn);
        }

        // --- Reactive refresh rotates the stored credential ---

        [Fact]
        public async Task RefreshAsync_RotatesStoredCredential()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "acc-old",
                RefreshToken = "ref-old",
                ExpiresAt = T0.AddSeconds(-10), // already expired
                ServerTarget = AsBaseUrl,
            });

            // The refresh grant returns a fresh token pair.
            using var account = MakeAccount(tmp, new SingleResponseHandler(
                TokenJson(access: "acc-new", refresh: "ref-new", expiresIn: 3600)));

            var refreshed = await account.RefreshAsync();

            Assert.True(refreshed);
            Assert.True(account.IsSignedIn);

            var persisted = Store(tmp).Read();
            Assert.Equal("acc-new", persisted!.AccessToken);
            Assert.Equal("ref-new", persisted.RefreshToken);
            // ServerTarget is preserved across a rotation (MachineCredentialStore.Rotate keeps identity fields).
            Assert.Equal(AsBaseUrl, persisted.ServerTarget);
        }

        [Fact]
        public async Task RefreshAsync_ServerRejects_FailsClosed_KeepsOldCredential()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "acc-old",
                RefreshToken = "ref-old",
                ExpiresAt = T0.AddSeconds(-10),
                ServerTarget = AsBaseUrl,
            });

            using var account = MakeAccount(tmp, new SingleResponseHandler(
                () => JsonResponse(HttpStatusCode.BadRequest, "{ \"error\": \"invalid_grant\" }")));

            var refreshed = await account.RefreshAsync();

            Assert.False(refreshed);
        }

        // --- helpers ---

        static MachineCredentialStore Store(TempDir tmp) => new(tmp.Path);

        static GodotAccountAuth MakeAccount(TempDir tmp, HttpMessageHandler handler)
            => new(
                asBaseUrlProvider: () => AsBaseUrl,
                store: new MachineCredentialStore(tmp.Path),
                service: new GodotDeviceAuthService(new HttpClient(handler)),
                clock: () => T0);

        static GodotDeviceAuthFlow MakeFlow(HttpMessageHandler handler)
            => new(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (_, _) => Task.CompletedTask,
                utcNow: () => T0Dt);

        static Func<HttpResponseMessage> Authorize(string userCode, string deviceCode)
            => () => JsonResponse(HttpStatusCode.OK, $$"""
                {
                  "device_code": "{{deviceCode}}",
                  "user_code": "{{userCode}}",
                  "verification_uri": "https://ai-game.dev/verify",
                  "verification_uri_complete": "https://ai-game.dev/verify?code={{userCode}}",
                  "expires_in": 600,
                  "interval": 5
                }
                """);

        static Func<HttpResponseMessage> Token(string access, string refresh, int expiresIn)
            => () => JsonResponse(HttpStatusCode.OK, TokenJsonText(access, refresh, expiresIn));

        static Func<HttpResponseMessage> Error(string error)
            => () => JsonResponse(HttpStatusCode.BadRequest, $"{{ \"error\": \"{error}\" }}");

        static Func<HttpResponseMessage> TokenJson(string access, string refresh, int expiresIn)
            => () => JsonResponse(HttpStatusCode.OK, TokenJsonText(access, refresh, expiresIn));

        static string TokenJsonText(string access, string refresh, int expiresIn) => $$"""
            {
              "access_token": "{{access}}",
              "refresh_token": "{{refresh}}",
              "token_type": "Bearer",
              "expires_in": {{expiresIn}},
              "scope": "mcp:plugin"
            }
            """;

        static HttpResponseMessage JsonResponse(HttpStatusCode code, string json)
            => new(code) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

        /// <summary>A unique temp directory, deleted on dispose — the isolated machine-store root per test.</summary>
        sealed class TempDir : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "godot-mcp-acct-" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
                catch { /* best-effort test cleanup */ }
            }
        }

        /// <summary>Device flow handler: first request (device_authorization) → authorize; each token POST → token.</summary>
        sealed class DeviceFlowHandler : HttpMessageHandler
        {
            readonly Func<HttpResponseMessage> _authorize;
            readonly Queue<Func<HttpResponseMessage>> _token;

            public DeviceFlowHandler(Func<HttpResponseMessage> authorize, params Func<HttpResponseMessage>[] token)
            {
                _authorize = authorize;
                _token = new Queue<Func<HttpResponseMessage>>(token);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri!.AbsolutePath;
                var responder = path.EndsWith("/oauth/token", StringComparison.Ordinal)
                    ? (_token.Count > 0 ? _token.Dequeue() : _token.Peek())
                    : _authorize;
                return Task.FromResult(responder());
            }
        }

        /// <summary>Responds to every request (the refresh POST) with one scripted response.</summary>
        sealed class SingleResponseHandler : HttpMessageHandler
        {
            readonly Func<HttpResponseMessage> _respond;
            public SingleResponseHandler(Func<HttpResponseMessage> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_respond());
        }

        /// <summary>Throws on any request — proves a code path performs no network I/O.</summary>
        sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new InvalidOperationException("unexpected network call: " + request.RequestUri);
        }
    }
}
