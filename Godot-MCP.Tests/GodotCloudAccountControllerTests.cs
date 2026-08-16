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
    /// Covers <see cref="GodotCloudAccountController"/> — the pure orchestration behind the dock's Cloud
    /// "Authorize" button (unified-machine-auth task f1), and the home of the O8 sink-write invariant:
    ///
    /// <para><b>A successful authorize persists the credential ONLY into the machine store.</b> The
    /// persisted-config layer (<see cref="GodotMcpConfig.CloudToken"/> — serialized to the plaintext
    /// <c>user://godot-mcp-config.json</c> sink) gains NO new cloudToken. This is the G-SEC-1 plant-1
    /// test: re-enabling the historical <c>config.CloudToken = token</c> write anywhere on the authorize
    /// path turns <see cref="SignIn_Success_WritesTheMachineStore_AndNeverTheCloudTokenSink"/> RED.
    /// The positive half is proven in the same test by a POSITIVE artifact — the machine store actually
    /// holding the committed families — so the no-write assert can never pass vacuously (the path
    /// demonstrably CAN persist a credential; it persists it to the store).</para>
    /// </summary>
    public class GodotCloudAccountControllerTests
    {
        const string AsBaseUrl = "https://ai-game.dev";

        [Fact]
        public async Task SignIn_Success_WritesTheMachineStore_AndNeverTheCloudTokenSink()
        {
            using var tmp = new TempDir();
            var handler = new ScriptedHandler(
                deviceAuthorize: DeviceAuthorizeJson("USER-1", "dev-1"),
                deviceToken: TokenJson("acc-agent", "ref-agent", scope: "mcp:agent"),
                exchange: ExchangeJson("acc-plugin", "ref-plugin", scope: "mcp:plugin", sub: "usr_1"));
            using var account = MakeAccount(tmp, handler);
            var config = new GodotMcpConfig();
            Assert.Null(config.CloudToken); // precondition: a fresh config carries no cloud token

            var outcome = await GodotCloudAccountController.SignInAsync(
                account, MakeFlow(handler), AsBaseUrl, config);

            // POSITIVE artifact: the sign-in DID persist a credential — into the machine store.
            Assert.Equal(GodotAccountSignInStatus.SignedIn, outcome.Status);
            var persisted = new MachineCredentialStore(tmp.Path).Read();
            Assert.Equal("acc-plugin", persisted?.Families?.Plugin?.AccessToken);
            Assert.Equal("acc-agent", persisted?.Families?.Agent?.AccessToken);
            Assert.True(account.IsSignedIn);

            // THE PIN (O8 / G-SEC-1 plant 1): the legacy user:// sink layer gained no new cloudToken.
            Assert.Null(config.CloudToken);
            // And the custom-token field was not abused as a side channel either.
            Assert.Null(config.CustomToken);
        }

        [Fact]
        public async Task SignIn_NotAuthorized_TouchesNeitherStoreNorConfig()
        {
            using var tmp = new TempDir();
            var handler = new ScriptedHandler(
                deviceAuthorize: DeviceAuthorizeJson("USER-1", "dev-1"),
                deviceToken: "{ \"error\": \"access_denied\" }",
                deviceTokenStatus: HttpStatusCode.BadRequest);
            using var account = MakeAccount(tmp, handler);
            var config = new GodotMcpConfig();

            var outcome = await GodotCloudAccountController.SignInAsync(
                account, MakeFlow(handler), AsBaseUrl, config);

            Assert.Equal(GodotAccountSignInStatus.NotAuthorized, outcome.Status);
            Assert.False(new MachineCredentialStore(tmp.Path).Exists);
            Assert.Null(config.CloudToken);
        }

        /// <summary>
        /// A PRE-EXISTING legacy sink token (the O8 read-fallback window) is left exactly as it was — the
        /// controller neither clears nor overwrites it on a successful machine-store sign-in (delete-source
        /// semantics belong to the f4 follow-up).
        /// </summary>
        [Fact]
        public async Task SignIn_Success_LeavesAPreExistingLegacySinkTokenUntouched()
        {
            using var tmp = new TempDir();
            var handler = new ScriptedHandler(
                deviceAuthorize: DeviceAuthorizeJson("USER-1", "dev-1"),
                deviceToken: TokenJson("acc-agent", "ref-agent", scope: "mcp:agent"),
                exchange: ExchangeJson("acc-plugin", "ref-plugin", scope: "mcp:plugin", sub: "usr_1"));
            using var account = MakeAccount(tmp, handler);
            var config = new GodotMcpConfig { CloudToken = "pre-existing-sink-token" };

            var outcome = await GodotCloudAccountController.SignInAsync(
                account, MakeFlow(handler), AsBaseUrl, config);

            Assert.True(outcome.Succeeded);
            Assert.Equal("pre-existing-sink-token", config.CloudToken);
        }

        // --- helpers (mirrors GodotAccountAuthTests' fixtures) ---

        static GodotAccountAuth MakeAccount(TempDir tmp, HttpMessageHandler handler)
            => new(
                asBaseUrlProvider: () => AsBaseUrl,
                store: new MachineCredentialStore(tmp.Path),
                httpClient: new HttpClient(handler));

        static GodotDeviceAuthFlow MakeFlow(HttpMessageHandler handler)
            => new(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (_, _) => Task.CompletedTask,
                utcNow: () => DateTime.UtcNow);

        static string DeviceAuthorizeJson(string userCode, string deviceCode) => $$"""
            {
              "device_code": "{{deviceCode}}",
              "user_code": "{{userCode}}",
              "verification_uri": "https://ai-game.dev/verify",
              "verification_uri_complete": "https://ai-game.dev/verify?code={{userCode}}",
              "expires_in": 600,
              "interval": 5
            }
            """;

        static string TokenJson(string access, string refresh, string scope) => $$"""
            {
              "access_token": "{{access}}",
              "refresh_token": "{{refresh}}",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "{{scope}}"
            }
            """;

        static string ExchangeJson(string access, string refresh, string scope, string sub) => $$"""
            {
              "access_token": "{{access}}",
              "refresh_token": "{{refresh}}",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "{{scope}}",
              "issued_token_type": "urn:ietf:params:oauth:token-type:access_token",
              "sub": "{{sub}}"
            }
            """;

        sealed class TempDir : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "godot-mcp-ctrl-" + Guid.NewGuid().ToString("N"));

            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
                catch { /* best-effort test cleanup */ }
            }
        }

        /// <summary>Routes device_authorization / device-code token / token-exchange to scripted JSON.</summary>
        sealed class ScriptedHandler : HttpMessageHandler
        {
            readonly string _deviceAuthorize;
            readonly string _deviceToken;
            readonly HttpStatusCode _deviceTokenStatus;
            readonly string? _exchange;

            public ScriptedHandler(string deviceAuthorize, string deviceToken,
                string? exchange = null, HttpStatusCode deviceTokenStatus = HttpStatusCode.OK)
            {
                _deviceAuthorize = deviceAuthorize;
                _deviceToken = deviceToken;
                _exchange = exchange;
                _deviceTokenStatus = deviceTokenStatus;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri!.AbsolutePath;
                var body = request.Content != null
                    ? await request.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;
                var decoded = WebUtility.UrlDecode(body);

                if (path.EndsWith("/oauth/device_authorization", StringComparison.Ordinal))
                    return Json(HttpStatusCode.OK, _deviceAuthorize);
                if (path.EndsWith("/oauth/revoke", StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.OK);
                if (path.EndsWith("/oauth/token", StringComparison.Ordinal))
                {
                    if (decoded.Contains("grant-type:token-exchange"))
                        return Json(HttpStatusCode.OK, _exchange ?? throw new InvalidOperationException("unscripted token exchange"));
                    return Json(_deviceTokenStatus, _deviceToken);
                }
                throw new InvalidOperationException($"unexpected request: {path}"); // path only — never form fields

                static HttpResponseMessage Json(HttpStatusCode code, string json)
                    => new(code) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
            }
        }
    }
}
