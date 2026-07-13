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
using com.IvanMurzak.McpPlugin.AgentConfig;
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The Godot addon's ai-game.dev account-credential coordinator (mcp-authorize design 06, D12 — the
    /// zero-button rule). It owns the shared machine credential store (<c>~/.ai-game-dev/credentials.json</c>)
    /// through McpPlugin 7.0's <see cref="PluginCredentialProvider"/> and the Godot
    /// <see cref="GodotTokenRefresher"/>, wiring the two device-flow endpoints (sign-in + refresh) into a
    /// single seam the connection consumes.
    ///
    /// <para>Three behaviours:</para>
    /// <list type="bullet">
    ///   <item><b>Boot auto-adopt:</b> the machine store is read at construction. If a valid (or refreshable)
    ///   credential is present the plugin is signed in with <b>zero UI interaction</b> — the connection
    ///   presents its JWT and pairs on boot (the store is populated once per machine by a <c>godot-cli
    ///   login</c>, an enrollment, or another engine).</item>
    ///   <item><b>Sign-in (persist):</b> <see cref="SignInAsync"/> runs the device flow and persists the
    ///   resulting credential into the machine store. (The in-editor "Sign in" button is wired to this by the
    ///   ConnectionPanel UI flip in the follow-up PR; PR 2 delivers + unit-tests the capability.)</item>
    ///   <item><b>Refresh:</b> <see cref="AccessTokenProvider"/> refreshes the token proactively before
    ///   <c>exp</c> on every (re)connect; <see cref="RefreshAsync"/> refreshes reactively when the connection's
    ///   3-strike authorization-rejected signal fires.</item>
    /// </list>
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the whole store/adopt/refresh lifecycle is
    /// unit-testable with a temp-directory <see cref="MachineCredentialStore"/> + a fake
    /// <see cref="System.Net.Http.HttpMessageHandler"/>. Never logs token material.
    /// </para>
    /// </summary>
    public sealed class GodotAccountAuth : IDisposable
    {
        readonly MachineCredentialStore _store;
        readonly PluginCredentialProvider _provider;
        readonly string _clientId;

        /// <summary>
        /// Construct the coordinator. <paramref name="asBaseUrlProvider"/> supplies the authorization-server
        /// base URL for refreshes (read live so a <c>.env</c> cloud-URL override applies). The remaining
        /// arguments are injectable seams for tests: a temp-directory <paramref name="store"/>, a fake-handler
        /// <paramref name="service"/>, a deterministic <paramref name="clock"/>. Constructing this READS the
        /// machine store (the auto-adopt), so a pre-existing credential leaves the coordinator
        /// <see cref="IsSignedIn"/> immediately.
        /// </summary>
        public GodotAccountAuth(
            Func<string> asBaseUrlProvider,
            MachineCredentialStore? store = null,
            GodotDeviceAuthService? service = null,
            ILogger? logger = null,
            string? clientId = null,
            Func<DateTimeOffset>? clock = null)
        {
            if (asBaseUrlProvider == null)
                throw new ArgumentNullException(nameof(asBaseUrlProvider));

            _store = store ?? new MachineCredentialStore();
            _clientId = string.IsNullOrEmpty(clientId) ? GodotDeviceAuthFlow.DefaultClientId : clientId!;
            var refresher = new GodotTokenRefresher(
                service ?? new GodotDeviceAuthService(), asBaseUrlProvider, _clientId, clock);

            // Auto-adopt: PluginCredentialProvider reads the store at construction. No UI, no device flow.
            _provider = new PluginCredentialProvider(_store, refresher, logger);
        }

        /// <summary>True when a usable machine-store credential is present (the zero-button signed-in state).</summary>
        public bool IsSignedIn => _provider.IsSignedIn;

        /// <summary>The account id (<c>sub</c>) the current credential resolves to, if known (diagnostic only).</summary>
        public string? Subject => _provider.Subject;

        /// <summary>
        /// The <c>Func&lt;Task&lt;string?&gt;&gt;</c> to compose into the connection's credential provider. It
        /// returns the current (proactively-refreshed) access token, or null when signed out — so a signed-out
        /// machine transparently falls back to the connection's existing token resolution.
        /// </summary>
        public Func<Task<string?>> AccessTokenProvider => _provider.AsAccessTokenProvider();

        /// <summary>
        /// Refresh the access token now (driven by the connection's 3-strike authorization-rejected signal).
        /// Returns true when a fresh token was persisted; false when refresh failed or is impossible (in which
        /// case the provider has surfaced sign-in-required and the caller must NOT loop).
        /// </summary>
        public Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
            => _provider.RefreshAsync(cancellationToken);

        /// <summary>
        /// Run the device flow against <paramref name="asBaseUrl"/> via <paramref name="flow"/> and, on
        /// success, persist the resulting credential into the machine store (D12). The caller owns the flow so
        /// it can subscribe to <see cref="GodotDeviceAuthFlow.OnStateChanged"/> for the browser-open. Returns
        /// true when a credential was adopted (signed in), false on any non-Authorized terminal state.
        /// </summary>
        public async Task<bool> SignInAsync(GodotDeviceAuthFlow flow, string asBaseUrl)
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));

            var result = await flow.AuthorizeAsync(asBaseUrl).ConfigureAwait(false);
            if (result == null)
                return false;

            _provider.Adopt(new MachineCredentials
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresAt = result.ExpiresAt,
                ServerTarget = asBaseUrl,
            });
            return true;
        }

        /// <summary>Sign out: delete the stored credential and reset to signed-out.</summary>
        public void SignOut() => _provider.SignOut();

        public void Dispose() => _provider.Dispose();
    }
}
