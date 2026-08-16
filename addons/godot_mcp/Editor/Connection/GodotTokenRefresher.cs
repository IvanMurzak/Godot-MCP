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
    /// The one Godot-local behavior this adapter ADDS is the live default-AS-base seam: a stored
    /// credential without a <c>serverTarget</c> refreshes against <paramref name="defaultAsBaseUrl"/>
    /// READ LIVE per call (so a <c>.env</c>/env cloud-URL override applies without a rebuild — the
    /// documented behavior of the pre-f1 refresher), where the shared refresher's own default is
    /// captured once at construction. Trailing-<c>/mcp</c> stripping and target normalization stay with
    /// the shared implementation. Pure-managed (no Godot native types, no <c>#if TOOLS</c>) and
    /// unit-testable with a fake <see cref="HttpMessageHandler"/>; fails closed and never logs token
    /// material (both inherited from the shared refresher).
    /// </para>
    /// </summary>
    public sealed class GodotTokenRefresher : ITokenRefresher
    {
        readonly HttpTokenRefresher _inner;
        readonly Func<string> _defaultAsBaseUrl;

        /// <summary>
        /// Construct the adapter. <paramref name="defaultAsBaseUrl"/> supplies the AS base URL when the
        /// stored credential carries no <c>serverTarget</c> (read live so a <c>.env</c> cloud-URL override
        /// applies). <paramref name="clientId"/> is this component's OWN id, presented only for legacy
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
            _defaultAsBaseUrl = defaultAsBaseUrl ?? throw new ArgumentNullException(nameof(defaultAsBaseUrl));
            // The inner default target is deliberately unused (empty): every request is resolved against
            // the LIVE default below before delegation, so the shared refresher always receives an
            // explicit ServerTarget.
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
        /// the LIVE default AS base, then delegate to the shared <see cref="HttpTokenRefresher"/> with the
        /// request's family context (stored <c>clientId</c>) intact.
        /// </summary>
        public Task<TokenRefreshResult> RefreshAsync(TokenRefreshRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var resolved = string.IsNullOrEmpty(request.ServerTarget)
                ? new TokenRefreshRequest(request.RefreshToken, _defaultAsBaseUrl(), request.ClientId)
                : request;

            return _inner.RefreshAsync(resolved, cancellationToken);
        }
    }
}
