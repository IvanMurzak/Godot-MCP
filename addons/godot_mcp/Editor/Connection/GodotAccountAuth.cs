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
using com.IvanMurzak.McpPlugin.AgentConfig;
using Microsoft.Extensions.Logging;
using R3;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>How a <see cref="GodotAccountAuth.SignInAsync"/> attempt ended (non-secret).</summary>
    public enum GodotAccountSignInStatus
    {
        /// <summary>Agent + plugin families committed to the machine store — fully signed in (F1 happy path).</summary>
        SignedIn,

        /// <summary>
        /// The agent family was committed but the plugin family was not (failed/busy token exchange or a
        /// busy/unreadable second lock hold) — the 03 F1 failure path. The machine holds a valid agent
        /// credential; a retry of Authorize completes the derivation.
        /// </summary>
        PartiallyAuthorized,

        /// <summary>The device flow ended without authorization (denied / expired / cancelled / failed). Nothing was written.</summary>
        NotAuthorized,

        /// <summary>The machine credential lock was busy before anything was written — retry.</summary>
        Busy,

        /// <summary>
        /// The store belongs to a different account (F7/D6 subject guard) — nothing was written; an
        /// account-switch confirmation flow is required before replacing it.
        /// </summary>
        SubjectConflict,

        /// <summary>Any other terminal failure (e.g. a machine-wide sign-out raced the commit). Nothing usable was written.</summary>
        Failed,
    }

    /// <summary>The outcome of a sign-in attempt. Carries NO token material.</summary>
    public sealed class GodotAccountSignInResult
    {
        internal GodotAccountSignInResult(GodotAccountSignInStatus status, string? detail)
        {
            Status = status;
            Detail = detail;
        }

        /// <summary>How the attempt ended.</summary>
        public GodotAccountSignInStatus Status { get; }

        /// <summary>Human-facing detail (busy path, exchange failure reason, …). Never token material.</summary>
        public string? Detail { get; }

        /// <summary>True when the machine is fully signed in (agent + plugin families committed).</summary>
        public bool Succeeded => Status == GodotAccountSignInStatus.SignedIn;
    }

    /// <summary>Outcome of the O8/F11.2 legacy <c>user://</c> cloudToken migration (non-secret).</summary>
    public enum GodotLegacyTokenMigrationResult
    {
        /// <summary>The legacy sink holds no cloudToken — nothing to migrate.</summary>
        NoSinkToken,

        /// <summary>The sink credential was written into the machine store (as a legacy family) under the lock.</summary>
        Migrated,

        /// <summary>The machine store already holds credential material — the store wins; sink untouched.</summary>
        SkippedStoreHasCredential,

        /// <summary>The store exists but cannot be read (04 §1) — never overwritten; migration skipped.</summary>
        SkippedStoreUnreadable,

        /// <summary>The machine credential lock was busy — migration retries on a later boot (idempotent).</summary>
        Busy,
    }

    /// <summary>
    /// The Godot addon's ai-game.dev account-credential coordinator (unified-machine-auth design, task f1 —
    /// the Godot adoption of the shared b3 credential stack). It owns the shared machine credential store
    /// (<c>~/.ai-game-dev/credentials.json</c>) through McpPlugin 8.1's <see cref="PluginCredentialProvider"/>
    /// + the cross-process <see cref="MachineCredentialLock"/>, and exposes the four account operations the
    /// editor consumes:
    /// <list type="bullet">
    ///   <item><b>Boot auto-adopt (zero-button rule):</b> the machine store is read at construction; a valid
    ///   (or refreshable) credential signs the plugin in with zero UI interaction.</item>
    ///   <item><b>Sign-in (F1):</b> <see cref="SignInAsync"/> runs the RFC 8628 device flow (scope
    ///   <c>mcp:agent</c>), then the shared TWO-LOCK-HOLD commit
    ///   (<see cref="MachineCredentialLoginCommit.CommitAsync"/>): agent family under the first hold, RFC 8693
    ///   token exchange between the holds, plugin family + v1 mirror under the second. Credential writes go
    ///   ONLY through the guarded helper — never through bare <see cref="PluginCredentialProvider.Adopt"/>
    ///   (staged remediation G-SEC-2).</item>
    ///   <item><b>Refresh:</b> proactive (before <c>exp</c>) and reactive (3-strike authorization-rejected)
    ///   via the shared <see cref="HttpTokenRefresher"/> (through the <see cref="GodotTokenRefresher"/>
    ///   adapter): stored <c>clientId</c>, no <c>scope</c>/<c>resource</c>, under the machine lock (04 §3).</item>
    ///   <item><b>Sign-out (F6):</b> <see cref="SignOutMachineWideAsync"/> — best-effort RFC 7009 revocation
    ///   of every stored family, then the lock-protocol store delete, machine-wide.</item>
    /// </list>
    ///
    /// <para>
    /// It also owns the O8/F11.2 legacy migration (<see cref="TryMigrateLegacyCloudToken"/>): a
    /// <c>user://godot-mcp-config.json</c> cloudToken found on start is copied into the machine store (under
    /// the lock) when — and only when — the store holds no credential. The sink itself is NOT deleted here
    /// (delete-source semantics are the f4 follow-up, one release later); the connection keeps its
    /// read-fallback to the sink for the same window.
    /// </para>
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the whole store/adopt/refresh/commit
    /// lifecycle is unit-testable with a temp-directory <see cref="MachineCredentialStore"/> + a fake
    /// <see cref="HttpMessageHandler"/>. Never logs token material.
    /// </para>
    /// </summary>
    public sealed class GodotAccountAuth : IDisposable
    {
        /// <summary>
        /// Backoff before the ONE bounded second-phase retry (03 F1 failure path: "P retries with
        /// backoff") — applied before an exchange retry (<c>AgentOnly</c>) and before a
        /// plugin-family commit retry (<c>PluginCommitBusy</c>/<c>StoreUnreadable</c>).
        /// </summary>
        public static readonly TimeSpan SecondPhaseRetryBackoff = TimeSpan.FromMilliseconds(750);

        readonly MachineCredentialStore _store;
        readonly MachineCredentialLock _lock;
        readonly Func<string> _asBaseUrlProvider;
        readonly HttpClient? _httpClient;
        readonly ILogger? _logger;
        readonly Func<DateTimeOffset>? _clock;
        readonly Func<TimeSpan, CancellationToken, Task> _delay;
        readonly string _clientId;

        // The provider is REBUILT (auto-adopting from the store) after every guarded store mutation —
        // login commit, machine-wide sign-out, legacy migration — because the guarded helpers write the
        // store directly and the provider's in-memory credential would otherwise go stale. Swapped
        // atomically; readers go through Provider below. Retired providers are parked (not disposed)
        // until this coordinator is disposed: a token resolution captured just before a swap may still
        // be executing, and disposing under it would surface a transient ObjectDisposedException into
        // the connection's credential resolution (review A2). Swaps are rare (sign-in / sign-out /
        // migration), so the parked list stays tiny.
        PluginCredentialProvider _provider;
        readonly object _retiredGate = new object();
        readonly System.Collections.Generic.List<PluginCredentialProvider> _retiredProviders = new();

        // The forwarding subscription onto the CURRENT provider's R3 surfaces (State +
        // OnSignInRequired), rebuilt on every provider swap so the stable events below keep firing
        // across ReloadFromStore. _lastForwardedAuthState de-dups the replay a fresh
        // ReadOnlyReactiveProperty subscription emits (the current value fires immediately on
        // subscribe), so AuthStateChanged stays edge-like across swaps. Guarded by _eventGate.
        readonly object _eventGate = new object();
        IDisposable? _providerEventsSubscription;
        AuthState? _lastForwardedAuthState;

        /// <summary>
        /// Construct the coordinator. <paramref name="asBaseUrlProvider"/> supplies the authorization-server
        /// base URL (read live so a <c>.env</c> cloud-URL override applies) used for refreshes of
        /// target-less credentials, token exchange, and revocation. The remaining arguments are injectable
        /// seams for tests: a temp-directory <paramref name="store"/>, a fake-handler
        /// <paramref name="httpClient"/>, a deterministic <paramref name="clock"/>, a test
        /// <paramref name="credentialLock"/>. Constructing this READS the machine store (the auto-adopt), so
        /// a pre-existing credential leaves the coordinator <see cref="IsSignedIn"/> immediately.
        /// </summary>
        public GodotAccountAuth(
            Func<string> asBaseUrlProvider,
            MachineCredentialStore? store = null,
            HttpClient? httpClient = null,
            ILogger? logger = null,
            string? clientId = null,
            Func<DateTimeOffset>? clock = null,
            MachineCredentialLock? credentialLock = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _asBaseUrlProvider = asBaseUrlProvider ?? throw new ArgumentNullException(nameof(asBaseUrlProvider));
            _store = store ?? new MachineCredentialStore();
            _lock = credentialLock ?? new MachineCredentialLock(_store.BaseDirectory);
            _httpClient = httpClient;
            _logger = logger;
            _clock = clock;
            _delay = delay ?? Task.Delay;
            _clientId = string.IsNullOrEmpty(clientId) ? GodotDeviceAuthFlow.DefaultClientId : clientId!;

            // Auto-adopt: PluginCredentialProvider reads the store at construction. No UI, no device flow.
            _provider = BuildProvider();
            HookProviderEvents(_provider);
        }

        PluginCredentialProvider BuildProvider()
            => new PluginCredentialProvider(
                _store,
                refresher: new GodotTokenRefresher(_asBaseUrlProvider, _clientId, _httpClient),
                logger: _logger,
                clock: _clock,
                credentialLock: _lock); // 04 §2: refresh writes run under the machine-wide lock

        PluginCredentialProvider Provider => Volatile.Read(ref _provider);

        /// <summary>True when a usable machine-store credential is present (the zero-button signed-in state).</summary>
        public bool IsSignedIn => Provider.IsSignedIn;

        /// <summary>The account id (<c>sub</c>) the current credential resolves to, if known (diagnostic only).</summary>
        public string? Subject => Provider.Subject;

        /// <summary>
        /// The CURRENT provider's credential state (oauth-client-error-hygiene e2, 02 §C3: engine UIs read
        /// the provider's own <c>State</c>, not just the connection's rejection signal). Stable across the
        /// internal provider swaps.
        /// </summary>
        public AuthState AuthState => Provider.State.CurrentValue;

        /// <summary>
        /// Raised when the CURRENT provider surfaces a terminal sign-in-required verdict (its
        /// <c>OnSignInRequired</c> — e.g. an <c>invalid_grant</c> refresh rejection). Survives the internal
        /// provider swaps (<see cref="ReloadFromStore"/> re-hooks the fresh provider). May fire on ANY
        /// thread — UI subscribers must marshal to the editor main thread themselves. Never carries token
        /// material.
        /// </summary>
        public event Action? SignInRequired;

        /// <summary>
        /// Raised when the credential state changes (SignedOut / SignedIn / SignInRequired), de-duplicated
        /// across the internal provider swaps (a swap re-subscribes and would otherwise replay the current
        /// value). May fire on ANY thread — UI subscribers must marshal themselves.
        /// </summary>
        public event Action<AuthState>? AuthStateChanged;

        /// <summary>
        /// (Re)hook the stable <see cref="SignInRequired"/> / <see cref="AuthStateChanged"/> events onto
        /// <paramref name="provider"/>'s R3 surfaces, replacing the previous provider's subscription. Called
        /// at construction and after every swap (<see cref="ReloadFromStore"/>) — without the re-hook, the
        /// panel's subscriptions would silently go dead on the first sign-in/sign-out/migration. The State
        /// replay a fresh subscription emits is de-duplicated against the last forwarded value so
        /// <see cref="AuthStateChanged"/> stays edge-like.
        /// </summary>
        void HookProviderEvents(PluginCredentialProvider provider)
        {
            var signInRequired = provider.OnSignInRequired.Subscribe(_ => SignInRequired?.Invoke());
            var stateChanged = provider.State.Subscribe(state =>
            {
                lock (_eventGate)
                {
                    if (_lastForwardedAuthState == state)
                        return;
                    _lastForwardedAuthState = state;
                }
                AuthStateChanged?.Invoke(state);
            });

            IDisposable? previous;
            lock (_eventGate)
            {
                previous = _providerEventsSubscription;
                _providerEventsSubscription = Disposable.Combine(signInRequired, stateChanged);
            }
            previous?.Dispose();
        }

        /// <summary>
        /// The <c>Func&lt;Task&lt;string?&gt;&gt;</c> to compose into the connection's credential provider. It
        /// returns the current (proactively-refreshed) access token, or null when signed out — so a signed-out
        /// machine transparently falls back to the connection's existing token resolution. Late-bound to the
        /// CURRENT provider so a post-commit/migration rebuild is picked up by an already-captured delegate.
        /// </summary>
        public Func<Task<string?>> AccessTokenProvider => () => Provider.GetAccessTokenAsync(CancellationToken.None);

        /// <summary>
        /// Refresh the access token now (driven by the connection's 3-strike authorization-rejected signal).
        /// Returns true when a fresh token was persisted (or adopted from a peer's rotation); false when
        /// refresh failed or is impossible (in which case the provider has surfaced sign-in-required and the
        /// caller must NOT loop).
        /// </summary>
        public Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
            => Provider.RefreshAsync(cancellationToken);

        /// <summary>
        /// Run the F1 sign-in end-to-end: the RFC 8628 device flow against <paramref name="asBaseUrl"/> via
        /// <paramref name="flow"/> (scope <c>mcp:agent</c> — the flow's default), then the shared two-lock-hold
        /// commit + RFC 8693 derivation (<see cref="MachineCredentialLoginCommit.CommitAsync"/>). The caller
        /// owns the flow so it can subscribe to <see cref="GodotDeviceAuthFlow.OnStateChanged"/> for the
        /// browser-open. On <see cref="GodotAccountSignInStatus.SignedIn"/> every AI-Game-Dev tool on the
        /// machine is signed in (F1.5).
        /// <para>
        /// The device-grant token response carries no <c>sub</c> (only the exchange response does — O5), so
        /// the commit's first-hold subject guard runs in its no-sub form (F7.3: proceed); the exchange
        /// response's subject is backfilled into the store by the commit helper.
        /// </para>
        /// </summary>
        public async Task<GodotAccountSignInResult> SignInAsync(
            GodotDeviceAuthFlow flow, string asBaseUrl, CancellationToken cancellationToken = default)
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (string.IsNullOrEmpty(asBaseUrl))
                throw new ArgumentException("The authorization-server base URL is required.", nameof(asBaseUrl));

            var result = await flow.AuthorizeAsync(asBaseUrl).ConfigureAwait(false);
            if (result == null)
                return new GodotAccountSignInResult(GodotAccountSignInStatus.NotAuthorized, flow.ErrorMessage);

            var agentFamily = new MachineCredentialFamily
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresAt = result.ExpiresAt,
                // D8: written from the value actually presented, never inferred — the flow presents
                // GodotDeviceAuthFlow.DefaultClientId on the RFC 8628 wire (its own hardcoded id), so
                // that is what the agent family is stamped with, regardless of any injected _clientId.
                ClientId = GodotDeviceAuthFlow.DefaultClientId,
                Scope = result.Scope ?? GodotDeviceAuthFlow.AgentScope,
            };

            var exchangeClient = new TokenExchangeClient(asBaseUrl, _clientId, _httpClient);
            var revocationClient = new TokenRevocationClient(asBaseUrl, _httpClient);

            var commit = await MachineCredentialLoginCommit.CommitAsync(
                _store,
                _lock,
                agentFamily,
                serverTarget: asBaseUrl,
                subject: null, // device-grant response carries no sub (O5 covers exchange/enroll only)
                exchangeClient: exchangeClient,
                confirmedReplaceOfSubject: null,
                revocationClient: revocationClient,
                logger: _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Second-phase completion (03 F1 failure path / review B1): a failed exchange gets ONE
            // bounded retry with backoff (the agent family is committed — no device flow needed), a
            // retryable second-hold outcome gets ONE bounded CommitPluginFamilyAsync retry with the
            // CARRIED mint (never a re-exchange), and a mint this coordinator is about to abandon is
            // best-effort revoked (b3 twin rule 4's premise: retryable outcomes skip revocation only
            // because the caller retries the same mint — once we stop retrying, we revoke).
            commit = await CompleteSecondPhaseAsync(
                commit, result.AccessToken, asBaseUrl, exchangeClient, revocationClient, cancellationToken)
                .ConfigureAwait(false);

            // The guarded helper wrote the store directly — rebuild the provider so its in-memory state
            // (IsSignedIn / AccessTokenProvider) reflects what was actually committed.
            ReloadFromStore();

            return commit.Status switch
            {
                LoginCommitStatus.FullyCommitted =>
                    new GodotAccountSignInResult(GodotAccountSignInStatus.SignedIn, null),
                LoginCommitStatus.AgentOnly or LoginCommitStatus.PluginCommitBusy or LoginCommitStatus.StoreUnreadable =>
                    new GodotAccountSignInResult(GodotAccountSignInStatus.PartiallyAuthorized, commit.Detail),
                LoginCommitStatus.Busy =>
                    new GodotAccountSignInResult(GodotAccountSignInStatus.Busy, commit.Detail),
                LoginCommitStatus.SubjectMismatch or LoginCommitStatus.GuardPremiseChanged =>
                    new GodotAccountSignInResult(GodotAccountSignInStatus.SubjectConflict, commit.Detail),
                _ =>
                    new GodotAccountSignInResult(GodotAccountSignInStatus.Failed, commit.Detail),
            };
        }

        /// <summary>True for the second-hold outcomes the commit helper designed to be retried with the SAME mint.</summary>
        static bool IsRetryableSecondPhase(LoginCommitStatus status)
            => status == LoginCommitStatus.PluginCommitBusy || status == LoginCommitStatus.StoreUnreadable;

        /// <summary>
        /// The bounded second-phase completion behind <see cref="SignInAsync"/> (03 F1 failure path,
        /// review B1). Exactly one backoff'd retry per stage, never a second device flow:
        /// <list type="number">
        ///   <item><b><see cref="LoginCommitStatus.AgentOnly"/>:</b> the agent family is committed, so
        ///   retry the RFC 8693 exchange once (backoff first) and, if it succeeds, commit the plugin
        ///   family via <see cref="MachineCredentialLoginCommit.CommitPluginFamilyAsync"/>.</item>
        ///   <item><b><see cref="LoginCommitStatus.PluginCommitBusy"/> /
        ///   <see cref="LoginCommitStatus.StoreUnreadable"/>:</b> retry the plugin-family commit once
        ///   with the CARRIED <see cref="LoginCommitResult.ExchangeResult"/> — the helper returns it
        ///   precisely so the caller retries without re-exchanging.</item>
        ///   <item><b>Still retryable after the retry:</b> this coordinator stops retrying (the user's
        ///   next action is a fresh device flow), so the mint is best-effort revoked before being
        ///   discarded — otherwise it would stay live server-side for up to 30 d, invisible to every
        ///   component (twin rule 4's exact scenario). Terminal hold-2 aborts
        ///   (<c>SubjectMismatch</c>/<c>StoreSignedOut</c>) are already revoked inside the helper.</item>
        /// </list>
        /// The expected subject for a retried commit is the mint's own <c>sub</c> (O5) — the plugin
        /// family derives from the agent token, so a store that now belongs to a DIFFERENT known
        /// account voids the premise (F7) and the helper aborts + revokes internally.
        /// </summary>
        async Task<LoginCommitResult> CompleteSecondPhaseAsync(
            LoginCommitResult commit,
            string agentAccessToken,
            string asBaseUrl,
            ITokenExchangeClient exchangeClient,
            ITokenRevocationClient revocationClient,
            CancellationToken cancellationToken)
        {
            // Stage 1 — AgentOnly: one exchange retry with backoff (03 F1: "P retries with backoff").
            if (commit.Status == LoginCommitStatus.AgentOnly)
            {
                await _delay(SecondPhaseRetryBackoff, cancellationToken).ConfigureAwait(false);

                TokenExchangeResult retryExchange;
                try
                {
                    retryExchange = await exchangeClient
                        .ExchangeAsync(agentAccessToken, asBaseUrl, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Token-exchange retry threw: {message}", ex.Message); // never token material
                    return commit; // still AgentOnly — the committed agent family stands (F1 failure path)
                }

                if (retryExchange == null || !retryExchange.Succeeded || string.IsNullOrEmpty(retryExchange.AccessToken))
                    return commit; // still AgentOnly

                commit = await MachineCredentialLoginCommit.CommitPluginFamilyAsync(
                    _store, _lock, retryExchange, _clientId,
                    expectedSubject: retryExchange.Subject,
                    serverTarget: asBaseUrl,
                    revocationClient: revocationClient,
                    logger: _logger,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                // Fall through — this commit may itself be retryable, and gets the stage-2 retry below.
            }

            // Stage 2 — a retryable second-hold outcome with a carried mint: one in-place retry.
            if (IsRetryableSecondPhase(commit.Status)
                && commit.ExchangeResult is { Succeeded: true } mint
                && !string.IsNullOrEmpty(mint.AccessToken))
            {
                await _delay(SecondPhaseRetryBackoff, cancellationToken).ConfigureAwait(false);

                commit = await MachineCredentialLoginCommit.CommitPluginFamilyAsync(
                    _store, _lock, mint, _clientId,
                    expectedSubject: mint.Subject,
                    serverTarget: asBaseUrl,
                    revocationClient: revocationClient,
                    logger: _logger,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Stage 3 — still not committed: the mint is being abandoned; revoke it (twin rule 4).
                if (IsRetryableSecondPhase(commit.Status) && commit.ExchangeResult is { } abandoned)
                    await RevokeAbandonedMintAsync(revocationClient, abandoned, asBaseUrl, cancellationToken).ConfigureAwait(false);
            }

            return commit;
        }

        /// <summary>
        /// Best-effort RFC 7009 revocation of a minted-but-never-committed plugin family this
        /// coordinator stops retrying (refresh token preferred — revoking it kills the family;
        /// mirror of the helper's own orphan revocation). Single attempt; failures only logged —
        /// the family also dies naturally at its own expiry.
        /// </summary>
        async Task RevokeAbandonedMintAsync(
            ITokenRevocationClient revocationClient,
            TokenExchangeResult mint,
            string? serverTarget,
            CancellationToken cancellationToken)
        {
            var token = !string.IsNullOrEmpty(mint.RefreshToken) ? mint.RefreshToken : mint.AccessToken;
            if (string.IsNullOrEmpty(token))
                return;

            try
            {
                var acknowledged = await revocationClient
                    .RevokeAsync(token!, _clientId, serverTarget, cancellationToken)
                    .ConfigureAwait(false);
                if (!acknowledged)
                    _logger?.LogWarning("Best-effort revocation of the abandoned plugin mint was not acknowledged; it expires naturally.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Best-effort revocation of the abandoned plugin mint threw: {message}", ex.Message); // never token material
            }
        }

        /// <summary>
        /// Machine-wide sign-out (F6): best-effort RFC 7009 revocation of EVERY stored family (each with its
        /// stored <c>clientId</c>; the component default for a legacy family), then the 04 §2 lock-protocol
        /// store delete. All other components on the machine observe the store gone and sign out (F6.3);
        /// offline revocation failures never block the local delete (F6.4). The caller shows the
        /// "signs out all tools on this machine" confirmation BEFORE calling this (F6.1).
        /// </summary>
        public async Task<MachineSignOutResult> SignOutMachineWideAsync(CancellationToken cancellationToken = default)
        {
            var result = await MachineWideSignOut.SignOutAsync(
                _store,
                _lock,
                new TokenRevocationClient(_asBaseUrlProvider(), _httpClient),
                _clientId,
                _logger,
                cancellationToken).ConfigureAwait(false);

            ReloadFromStore();
            return result;
        }

        /// <summary>
        /// O8 / F11.2 legacy migration (migrate-on-touch): copy a pre-existing
        /// <c>user://godot-mcp-config.json</c> <paramref name="legacyCloudToken"/> into the machine store —
        /// UNDER the machine lock — when the store holds no credential. The v1-shaped document written here
        /// is normalized by the store's write contract into a v2 <c>families.legacy</c> entry (+ the v1
        /// compat mirror), i.e. exactly the F11.1 legacy-adoption shape. Rules:
        /// <list type="bullet">
        ///   <item>An existing store credential ALWAYS wins — the sink is never allowed to overwrite it.</item>
        ///   <item>An unreadable store is never overwritten (04 §1) — migration is skipped.</item>
        ///   <item>A busy lock skips this attempt; the migration is idempotent and re-runs next boot.</item>
        ///   <item>The sink file is NOT modified — read-fallback + delete-source are the f4 follow-up.</item>
        ///   <item><paramref name="legacyCloudToken"/> is SECRET MATERIAL: it is never logged and never
        ///   surfaced in the returned result.</item>
        /// </list>
        /// </summary>
        public GodotLegacyTokenMigrationResult TryMigrateLegacyCloudToken(string? legacyCloudToken, string? serverTarget)
        {
            if (string.IsNullOrEmpty(legacyCloudToken))
                return GodotLegacyTokenMigrationResult.NoSinkToken;

            // Cheap lock-free pre-check (F3.1): only an empty store is a migration candidate.
            var preRead = _store.TryRead();
            if (preRead.Status == MachineCredentialStoreStatus.Unreadable)
                return GodotLegacyTokenMigrationResult.SkippedStoreUnreadable;
            if (HasCredentialMaterial(preRead))
                return GodotLegacyTokenMigrationResult.SkippedStoreHasCredential;

            var hold = _lock.TryAcquire();
            if (hold == null)
                return GodotLegacyTokenMigrationResult.Busy;

            using (hold)
            {
                // Re-read under the hold (double-checked): a peer login may have won the race.
                var read = _store.TryRead();
                if (read.Status == MachineCredentialStoreStatus.Unreadable)
                    return GodotLegacyTokenMigrationResult.SkippedStoreUnreadable;
                if (HasCredentialMaterial(read))
                    return GodotLegacyTokenMigrationResult.SkippedStoreHasCredential;

                if (!hold.IsStillOwned())
                    return GodotLegacyTokenMigrationResult.Busy;

                // v1-shaped document → the store's write contract normalizes it to a v2 families.legacy
                // entry + version stamp + compat mirror (the same path F11.1 legacy adoption uses).
                _store.Write(new MachineCredentials
                {
                    AccessToken = legacyCloudToken,
                    ServerTarget = serverTarget,
                });
            }

            ReloadFromStore();
            _logger?.LogInformation("Migrated the legacy Godot user:// cloud credential into the machine store (O8/F11.2)."); // never token material
            return GodotLegacyTokenMigrationResult.Migrated;
        }

        /// <summary>True when the read store document carries ANY token material (any family or the v1 top level).</summary>
        static bool HasCredentialMaterial(MachineCredentialReadResult read)
        {
            if (read.Status != MachineCredentialStoreStatus.Ok || read.Credentials == null)
                return false;

            var credentials = read.Credentials;
            return !string.IsNullOrEmpty(credentials.AccessToken)
                || !string.IsNullOrEmpty(credentials.RefreshToken)
                || FamilyHasMaterial(credentials.Families?.Agent)
                || FamilyHasMaterial(credentials.Families?.Plugin)
                || FamilyHasMaterial(credentials.Families?.Legacy);
        }

        static bool FamilyHasMaterial(MachineCredentialFamily? family)
            => family != null && (!string.IsNullOrEmpty(family.AccessToken) || !string.IsNullOrEmpty(family.RefreshToken));

        /// <summary>
        /// Sign out THIS provider only: delete the stored credential via the lock-protocol delete path and
        /// reset to signed-out. No server-side revocation — the machine-wide F6 path is
        /// <see cref="SignOutMachineWideAsync"/>.
        /// </summary>
        public void SignOut() => Provider.SignOut();

        /// <summary>
        /// Swap in a freshly-built provider so the in-memory state re-adopts whatever the machine store now
        /// holds (called after every guarded store mutation). The store read is cheap and lock-free (F3.1).
        /// The retired provider is PARKED, not disposed (review A2): a token resolution captured just before
        /// the swap may still be executing, and disposing under it would throw a transient
        /// <see cref="ObjectDisposedException"/> into the connection's credential resolution. All parked
        /// providers are disposed with this coordinator.
        /// </summary>
        void ReloadFromStore()
        {
            var fresh = BuildProvider();
            var old = Interlocked.Exchange(ref _provider, fresh);
            lock (_retiredGate)
                _retiredProviders.Add(old);

            // Re-hook the stable events onto the fresh provider (the old provider keeps its R3 surfaces,
            // but it no longer sees refreshes — a subscription left on it would go silently dead).
            HookProviderEvents(fresh);
        }

        public void Dispose()
        {
            IDisposable? events;
            lock (_eventGate)
            {
                events = _providerEventsSubscription;
                _providerEventsSubscription = null;
            }
            events?.Dispose();

            Provider.Dispose();
            lock (_retiredGate)
            {
                foreach (var retired in _retiredProviders)
                {
                    try { retired.Dispose(); } catch { /* teardown must never throw */ }
                }
                _retiredProviders.Clear();
            }
        }
    }
}
