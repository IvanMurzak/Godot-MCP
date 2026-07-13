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
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Godot's implementation of McpPlugin 7.0's <see cref="ITokenRefresher"/> seam (mcp-authorize design 03
    /// Flow B / 06): the ONE piece of the connection layer that talks to the ai-game.dev authorization server
    /// over HTTP to exchange a refresh token for a fresh access token (<c>grant_type=refresh_token</c> at
    /// <c>/oauth/token</c>). <see cref="PluginCredentialProvider"/> owns the machine-store credential and
    /// invokes this both proactively (before <c>exp</c>) and reactively (on the connection's 3-strike
    /// <c>_authorizationRejected</c> signal).
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so it is unit-testable with a fake
    /// <see cref="System.Net.Http.HttpMessageHandler"/>. It <b>fails closed</b> — any error (a rejected token,
    /// an HTTP/JSON fault) returns <see cref="TokenRefreshResult.Failure"/>, never a partial or fabricated
    /// token — and never logs token material (a surfaced <see cref="Exception.Message"/> is HTTP/JSON only).
    /// </para>
    /// </summary>
    public sealed class GodotTokenRefresher : ITokenRefresher
    {
        readonly GodotDeviceAuthService _service;
        readonly Func<string> _defaultAsBaseUrl;
        readonly string _clientId;
        readonly Func<DateTimeOffset> _clock;

        /// <summary>
        /// Construct a refresher. <paramref name="defaultAsBaseUrl"/> supplies the AS base URL when the stored
        /// credential carries no <c>serverTarget</c> (read live so a <c>.env</c> cloud-URL override applies).
        /// <paramref name="clientId"/> defaults to <see cref="GodotDeviceAuthFlow.DefaultClientId"/> — it MUST
        /// match the id the refresh token was issued under. <paramref name="clock"/> is injectable for
        /// deterministic expiry tests.
        /// </summary>
        public GodotTokenRefresher(
            GodotDeviceAuthService service,
            Func<string> defaultAsBaseUrl,
            string? clientId = null,
            Func<DateTimeOffset>? clock = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _defaultAsBaseUrl = defaultAsBaseUrl ?? throw new ArgumentNullException(nameof(defaultAsBaseUrl));
            _clientId = string.IsNullOrEmpty(clientId) ? GodotDeviceAuthFlow.DefaultClientId : clientId!;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public async Task<TokenRefreshResult> RefreshAsync(string refreshToken, string? serverTarget, CancellationToken cancellationToken = default)
        {
            try
            {
                var asBase = ResolveAsBaseUrl(serverTarget);
                var response = await _service
                    .RefreshTokenAsync(asBase, refreshToken, _clientId, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(response.AccessToken))
                {
                    var expiresAt = response.ExpiresIn > 0
                        ? _clock().AddSeconds(response.ExpiresIn)
                        : (DateTimeOffset?)null;
                    // A null/empty RefreshToken tells the caller the AS did not rotate it (keep the old one).
                    return TokenRefreshResult.Success(response.AccessToken!, response.RefreshToken, expiresAt);
                }

                return TokenRefreshResult.Failure(response.Error ?? "refresh failed");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // ex.Message is an HTTP/JSON transport fault, never token material — safe to surface as the
                // (non-secret) failure reason the provider logs.
                return TokenRefreshResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Resolve the AS base URL for the refresh POST. A stored <c>serverTarget</c> wins (an enrolled
        /// hosted-vs-local target); a trailing <c>/mcp</c> hub path is stripped so we never POST to
        /// <c>/mcp/oauth/token</c>. An empty target falls back to the live default (the plugin's cloud base).
        /// </summary>
        string ResolveAsBaseUrl(string? serverTarget)
        {
            if (string.IsNullOrEmpty(serverTarget))
                return _defaultAsBaseUrl();

            var trimmed = serverTarget!.TrimEnd('/');
            if (trimmed.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^"/mcp".Length];
            return trimmed;
        }
    }
}
