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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Stateless HTTP transport for the OAuth 2.0 device-authorization-grant ("device code") flow against
    /// the cloud server's <c>/api/auth/device/*</c> endpoints. The Godot analog of Unity-MCP's
    /// <c>DeviceAuthService</c> — pure-managed (no Godot native types, no <c>#if TOOLS</c>), so it is
    /// unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host with a mockable
    /// <see cref="HttpMessageHandler"/>.
    ///
    /// <para>
    /// The <see cref="HttpClient"/> is injected (default: a process-shared instance) so tests can
    /// substitute a fake handler. JSON uses <see cref="JsonNamingPolicy.SnakeCaseLower"/> +
    /// case-insensitive matching, mirroring the server contract.
    /// </para>
    /// </summary>
    public sealed class GodotDeviceAuthService
    {
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
        /// POST <c>&lt;cloudBaseUrl&gt;/api/auth/device/authorize</c> with <c>{ "client_label": ... }</c>
        /// to begin a device-authorization flow. Throws on a non-success HTTP status (the caller's flow
        /// turns the exception into a Failed state).
        /// </summary>
        public async Task<DeviceAuthorizeResponse> InitiateDeviceAuthAsync(
            string cloudBaseUrl, string? clientLabel, CancellationToken ct = default)
        {
            var body = clientLabel != null
                ? JsonSerializer.Serialize(new { client_label = clientLabel }, JsonOptions)
                : "{}";

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _httpClient
                .PostAsync($"{cloudBaseUrl.TrimEnd('/')}/api/auth/device/authorize", content, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<DeviceAuthorizeResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize device authorize response.");
        }

        /// <summary>
        /// POST <c>&lt;cloudBaseUrl&gt;/api/auth/device/token</c> with the device code + grant type to poll
        /// for the access token. Unlike <see cref="InitiateDeviceAuthAsync"/>, this does NOT throw on a
        /// non-2xx status: the device-flow spec carries pending/slow-down/denied as a structured
        /// <c>error</c> body (often with a 400), so the caller inspects <see cref="DeviceTokenResponse.Error"/>.
        /// </summary>
        public async Task<DeviceTokenResponse> PollDeviceTokenAsync(
            string cloudBaseUrl, string deviceCode, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new
            {
                device_code = deviceCode,
                grant_type = "urn:ietf:params:oauth:grant-type:device_code"
            }, JsonOptions);

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _httpClient
                .PostAsync($"{cloudBaseUrl.TrimEnd('/')}/api/auth/device/token", content, ct)
                .ConfigureAwait(false);

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<DeviceTokenResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize device token response.");
        }

        /// <summary>Response of <c>POST /api/auth/device/authorize</c>.</summary>
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

        /// <summary>Response of <c>POST /api/auth/device/token</c> (success carries the token; otherwise an error).</summary>
        public sealed class DeviceTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("error_description")]
            public string? ErrorDescription { get; set; }
        }
    }
}
