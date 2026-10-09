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
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// THIN ADAPTER over McpPlugin 8.1's shared <see cref="HttpTokenRefresher"/> (unified-machine-auth
    /// 04 §3, task f1 — the engine-adoption cascade of b3). The former local refresh implementation
    /// (its own <c>/oauth/token</c> form via <see cref="GodotDeviceAuthService"/>) is DELETED so exactly
    /// one refresh wire shape exists per language; the shared refresher enforces the 04 §3 rules this
    /// addon inherits by delegation:
    /// <list type="bullet">
    ///   <item><b>Stored <c>clientId</c> (04 §3.2):</b> a family's stored id is presented verbatim; the
    ///   component default (<see cref="GodotDeviceAuthFlow.DefaultClientId"/>) is presented ONLY for a
    ///   legacy family of unknown id (04 §3.7).</item>
    ///   <item><b>No <c>scope</c>, no <c>resource</c> (04 §3.3 / P0-3):</b> the wire request omits both
    ///   entirely — the server falls back to the stored grant.</item>
    ///   <item>Rate discipline (one attempt per family per skew window), the 15 s contract HTTP timeout,
    ///   and the <c>invalid_grant</c>-vs-transient failure split (04 §3.5/§3.6).</item>
    /// </list>
    ///
    /// <para>
    /// A stored credential without a serverTarget belongs to the original production origin.
    /// Changing the project cloud URL must never redirect an unbound legacy refresh token.
    /// Explicit stored targets and family client IDs are delegated to the shared implementation.
    /// </para>
    /// </summary>
    public sealed class GodotTokenRefresher : ITokenRefresher
    {
        readonly HttpTokenRefresher _inner;

        /// <summary>
        /// Construct the adapter. <paramref name="defaultAsBaseUrl"/> is retained for API compatibility;
        /// target-less legacy credentials remain bound to production. <paramref name="clientId"/> is this component's OWN id, presented only for legacy
        /// families of unknown id (04 §3.7) — it defaults to
        /// <see cref="GodotDeviceAuthFlow.DefaultClientId"/> and never overrides a family's stored id.
        /// <paramref name="httpClient"/> is injectable for tests (fake handler); the shared refresher's
        /// process-shared client is used when null.
        /// </summary>
        public GodotTokenRefresher(
            Func<string> defaultAsBaseUrl,
            string? clientId = null,
            HttpClient? httpClient = null)
        {
            _ = defaultAsBaseUrl ?? throw new ArgumentNullException(nameof(defaultAsBaseUrl));
            // Every request below has an explicit stored/original legacy target.
            _inner = new HttpTokenRefresher(
                defaultServerTarget: string.Empty,
                componentClientId: string.IsNullOrEmpty(clientId) ? GodotDeviceAuthFlow.DefaultClientId : clientId!,
                httpClient: httpClient);
        }

        /// <summary>
        /// Legacy two-string API (no family context): delegates to the family-aware overload with a null
        /// <c>clientId</c>, so the shared refresher presents the component default (04 §3.7) and — as
        /// always — omits <c>scope</c>/<c>resource</c> from the wire request.
        /// </summary>
        public Task<TokenRefreshResult> RefreshAsync(string refreshToken, string? serverTarget, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return Task.FromResult(TokenRefreshResult.Failure("no refresh token"));
            return RefreshAsync(new TokenRefreshRequest(refreshToken, serverTarget, clientId: null), cancellationToken);
        }

        /// <summary>
        /// The family-aware refresh: resolve a missing <see cref="TokenRefreshRequest.ServerTarget"/> from
        /// the original production AS base, then delegate to the shared <see cref="HttpTokenRefresher"/> with the
        /// request's family context (stored <c>clientId</c>) intact.
        /// </summary>
        public Task<TokenRefreshResult> RefreshAsync(TokenRefreshRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var resolved = string.IsNullOrEmpty(request.ServerTarget)
                ? new TokenRefreshRequest(request.RefreshToken, GodotMcpConfig.DefaultCloudBaseUrl, request.ClientId)
                : request;

            return _inner.RefreshAsync(resolved, cancellationToken);
        }
    }
}
