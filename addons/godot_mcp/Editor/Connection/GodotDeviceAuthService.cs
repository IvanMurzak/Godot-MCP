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
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Stateless HTTP transport for the OAuth 2.0 device-authorization-grant ("device code") flow against
    /// the ai-game.dev authorization server's <b>RFC 8628-conformant alias</b> — <c>/oauth/device_authorization</c>
    /// + <c>/oauth/token</c> (mcp-authorize design 03 Flow B / 06). The Godot analog of Unity-MCP's
    /// <c>DeviceAuthService</c> — pure-managed (no Godot native types, no <c>#if TOOLS</c>), so it is
    /// unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host with a mockable
    /// <see cref="HttpMessageHandler"/>.
    ///
    /// <para>
    /// The AS surface (verified against <c>cloud/AI-Game-Dev-Server</c> <c>routers/oauth.py</c>):
    /// <list type="bullet">
    ///   <item><c>POST /oauth/device_authorization</c> — <c>application/x-www-form-urlencoded</c>
    ///   <c>client_id</c> (REQUIRED) + <c>scope</c> (<c>mcp:plugin</c>) → the standard device-authorization
    ///   document.</item>
    ///   <item><c>POST /oauth/token</c> — the device-code URN grant (<c>grant_type</c> + <c>device_code</c> +
    ///   <c>client_id</c>) polled until authorized, and the <c>refresh_token</c> grant. <c>scope=mcp:plugin</c>
    ///   yields the ES256 hub JWT (<c>aud=urn:agd:hub</c>) + a rotating refresh token.</item>
    /// </list>
    /// The SAME <c>client_id</c> MUST be presented to the device-authorization request, the device-code token
    /// exchange, AND every subsequent refresh — the AS binds the grant to it and rejects a mismatch with
    /// <c>invalid_grant</c> (<c>oauth_token_service.grant_device_code</c> / <c>grant_refresh_token</c>).
    /// </para>
    ///
    /// <para>
    /// The <see cref="HttpClient"/> is injected (default: a process-shared instance) so tests can
    /// substitute a fake handler. JSON uses <see cref="JsonNamingPolicy.SnakeCaseLower"/> +
    /// case-insensitive matching, mirroring the server contract.
    /// </para>
    /// </summary>
    public sealed class GodotDeviceAuthService
    {
        /// <summary>The RFC 8628 device-code grant type presented at <c>/oauth/token</c>.</summary>
        public const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

        /// <summary>The refresh-token grant type presented at <c>/oauth/token</c>.</summary>
        public const string RefreshTokenGrantType = "refresh_token";

        static readonly HttpClient SharedHttpClient = new();

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        readonly HttpClient _httpClient;

        /// <summary>
        /// Create a service over a specific <see cref="HttpClient"/> (tests inject one wrapping a fake
        /// <see cref="HttpMessageHandler"/>). When <paramref name="httpClient"/> is <c>null</c>, the
        /// process-shared client is used (the editor path).
        /// </summary>
        public GodotDeviceAuthService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? SharedHttpClient;
        }

        /// <summary>
        /// POST <c>&lt;asBaseUrl&gt;/oauth/device_authorization</c> with the form
        /// <c>client_id=&lt;clientId&gt;&amp;scope=&lt;scope&gt;</c> to begin a device-authorization flow.
        /// Throws on a non-success HTTP status (the caller's flow turns the exception into a Failed state).
        /// </summary>
        public async Task<DeviceAuthorizeResponse> RequestDeviceAuthorizationAsync(
            string asBaseUrl, string clientId, string scope, CancellationToken ct = default)
        {
            using var content = FormContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scope,
            });
            using var response = await _httpClient
                .PostAsync($"{asBaseUrl.TrimEnd('/')}/oauth/device_authorization", content, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<DeviceAuthorizeResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize device authorization response.");
        }

        /// <summary>
        /// POST <c>&lt;asBaseUrl&gt;/oauth/token</c> with the device-code grant to poll for the access token.
        /// Unlike <see cref="RequestDeviceAuthorizationAsync"/>, this does NOT throw on a non-2xx status: the
        /// device-flow spec carries pending/slow-down/denied as a structured RFC 6749 §5.2 <c>error</c> body
        /// (a 400), so the caller inspects <see cref="DeviceTokenResponse.Error"/>. The <paramref name="clientId"/>
        /// MUST equal the one presented at <see cref="RequestDeviceAuthorizationAsync"/>.
        /// </summary>
        public async Task<DeviceTokenResponse> PollTokenAsync(
            string asBaseUrl, string deviceCode, string clientId, CancellationToken ct = default)
        {
            using var content = FormContent(new Dictionary<string, string>
            {
                ["grant_type"] = DeviceCodeGrantType,
                ["device_code"] = deviceCode,
                ["client_id"] = clientId,
            });
            return await PostTokenAsync(asBaseUrl, content, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// POST <c>&lt;asBaseUrl&gt;/oauth/token</c> with the <c>refresh_token</c> grant to mint a fresh
        /// access token (and possibly a rotated refresh token). Does NOT throw on a non-2xx status — a
        /// rejected/expired refresh token surfaces as a structured RFC 6749 §5.2 error body the caller reads
        /// via <see cref="DeviceTokenResponse.Error"/>. The <paramref name="clientId"/> MUST equal the one the
        /// refresh token was issued under (the AS rejects a mismatch with <c>invalid_grant</c>).
        /// </summary>
        public async Task<DeviceTokenResponse> RefreshTokenAsync(
            string asBaseUrl, string refreshToken, string clientId, CancellationToken ct = default)
        {
            using var content = FormContent(new Dictionary<string, string>
            {
                ["grant_type"] = RefreshTokenGrantType,
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
            });
            return await PostTokenAsync(asBaseUrl, content, ct).ConfigureAwait(false);
        }

        async Task<DeviceTokenResponse> PostTokenAsync(string asBaseUrl, HttpContent content, CancellationToken ct)
        {
            using var response = await _httpClient
                .PostAsync($"{asBaseUrl.TrimEnd('/')}/oauth/token", content, ct)
                .ConfigureAwait(false);

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<DeviceTokenResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize token response.");
        }

        static FormUrlEncodedContent FormContent(Dictionary<string, string> fields)
            => new(fields);

        /// <summary>Response of <c>POST /oauth/device_authorization</c> (the standard RFC 8628 document).</summary>
        public sealed class DeviceAuthorizeResponse
        {
            [JsonPropertyName("device_code")]
            public string DeviceCode { get; set; } = "";

            [JsonPropertyName("user_code")]
            public string UserCode { get; set; } = "";

            [JsonPropertyName("verification_uri")]
            public string VerificationUri { get; set; } = "";

            [JsonPropertyName("verification_uri_complete")]
            public string VerificationUriComplete { get; set; } = "";

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("interval")]
            public int Interval { get; set; }
        }

        /// <summary>
        /// Response of <c>POST /oauth/token</c> (success carries the access token + rotating refresh token +
        /// expiry; otherwise an RFC 6749 §5.2 error).
        /// </summary>
        public sealed class DeviceTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("error_description")]
            public string? ErrorDescription { get; set; }
        }
    }
}
