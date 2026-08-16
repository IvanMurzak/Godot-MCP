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
using System.Linq;
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
    /// Covers <see cref="GodotAccountAuth"/> — the machine-store account coordinator on the shared
    /// McpPlugin 8.1 credential stack (unified-machine-auth task f1). Verifies, against a temp-directory
    /// <see cref="MachineCredentialStore"/> + a scripted fake authorization server
    /// (<see cref="FakeAuthServer"/>):
    /// <list type="bullet">
    ///   <item>boot auto-adopt (zero-button rule), including v1/legacy-shaped store adoption;</item>
    ///   <item>the F1 sign-in: device flow (scope <c>mcp:agent</c>) → two-lock-hold commit → RFC 8693
    ///   derivation → agent + plugin families + v1 mirror in the store; the AgentOnly failure path;</item>
    ///   <item>the 04 §3 refresh WIRE SHAPE the adapter must preserve — the family's STORED
    ///   <c>clientId</c>, never the component default, and NO <c>scope</c>/<c>resource</c> (the G-SEC-1
    ///   plant-3 discriminators) — plus the proactive expiry self-heal (08 A1);</item>
    ///   <item>the F6 machine-wide sign-out (revoke every family with its stored id, delete the store);</item>
    ///   <item>the O8/F11.2 legacy <c>user://</c> cloudToken migration (the G-SEC-1 plant-2 target).</item>
    /// </list>
    /// No token is ever asserted into a log surface.
    /// </summary>
    public class GodotAccountAuthTests
    {
        const string AsBaseUrl = "https://ai-game.dev";
        const string ComponentClientId = "godot-mcp-plugin"; // = GodotDeviceAuthFlow.DefaultClientId
        static readonly DateTimeOffset FarFuture = DateTimeOffset.UtcNow.AddDays(14);
        static readonly DateTimeOffset AlreadyExpired = DateTimeOffset.UtcNow.AddMinutes(-10);

        // --- Boot auto-adopt (the zero-button rule) ---

        [Fact]
        public void EmptyStore_NotSignedIn()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, new FakeAuthServer());

            Assert.False(account.IsSignedIn);
        }

        [Fact]
        public async Task EmptyStore_AccessTokenProvider_ReturnsNull()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, new FakeAuthServer());

            var token = await account.AccessTokenProvider();

            Assert.Null(token);
        }

        [Fact]
        public async Task PrePopulatedStore_AutoAdopts_WithoutNetworkIo()
        {
            using var tmp = new TempDir();
            // No expiry → no proactive refresh is due, so the provider must return the stored token
            // WITHOUT touching the network (the throwing FakeAuthServer proves it).
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "stored-access",
                RefreshToken = "stored-refresh",
                ExpiresAt = null,
                ServerTarget = AsBaseUrl,
            });

            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());

            Assert.True(account.IsSignedIn);
            Assert.Equal("stored-access", await account.AccessTokenProvider());
        }

        /// <summary>
        /// The DoD "v1 adoption" case: a store holding only v1-shaped (top-level) token material is read as
        /// the LEGACY family (04 §1 v1 read-compat) and signs the account in on boot with zero UI. The
        /// write contract this addon depends on has normalized the document to v2 (<c>families.legacy</c> +
        /// version + compat mirror) — asserted so a regression in the consumed package's normalization is
        /// caught here, not in an editor session.
        /// </summary>
        [Fact]
        public async Task V1ShapedStore_IsAdopted_AsLegacyFamily_AndSignsIn()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "v1-access",
                RefreshToken = "v1-refresh",
                ExpiresAt = null,
                ServerTarget = AsBaseUrl,
            });

            var persisted = Store(tmp).Read();
            Assert.NotNull(persisted?.Families?.Legacy);
            Assert.Equal("v1-access", persisted!.Families!.Legacy!.AccessToken);
            Assert.Equal("v1-access", persisted.AccessToken); // the v1 compat mirror survives

            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());

            Assert.True(account.IsSignedIn);
            Assert.Equal("v1-access", await account.AccessTokenProvider());
        }

        // --- Sign-in (F1: device flow → two-lock-hold commit → RFC 8693 derivation) ---

        [Fact]
        public async Task SignInAsync_Success_CommitsAgentAndPluginFamilies_AndAnotherCoordinatorAutoAdopts()
        {
            using var tmp = new TempDir();
            var server = new FakeAuthServer
            {
                DeviceAuthorize = Ok(DeviceAuthorizeJson("USER-1", "dev-1")),
                DeviceToken = Ok(TokenJson("acc-agent", "ref-agent", 3600, scope: "mcp:agent")),
                Exchange = Ok(ExchangeJson("acc-plugin", "ref-plugin", 3600, scope: "mcp:plugin", sub: "usr_1")),
            };
            using var account = MakeAccount(tmp, server);

            var outcome = await account.SignInAsync(MakeFlow(server), AsBaseUrl);

            Assert.Equal(GodotAccountSignInStatus.SignedIn, outcome.Status);
            Assert.True(outcome.Succeeded);
            Assert.True(account.IsSignedIn);

            var persisted = Store(tmp).Read();
            Assert.NotNull(persisted);

            // Agent family: the minted tokens, stamped with the clientId ACTUALLY presented (D8) + scope.
            var agent = persisted!.Families?.Agent;
            Assert.NotNull(agent);
            Assert.Equal("acc-agent", agent!.AccessToken);
            Assert.Equal("ref-agent", agent.RefreshToken);
            Assert.Equal(ComponentClientId, agent.ClientId);
            Assert.Equal("mcp:agent", agent.Scope);

            // Plugin family: the RFC 8693 derivation, stamped with the exchanging client's own id.
            var plugin = persisted.Families?.Plugin;
            Assert.NotNull(plugin);
            Assert.Equal("acc-plugin", plugin!.AccessToken);
            Assert.Equal("ref-plugin", plugin.RefreshToken);
            Assert.Equal(ComponentClientId, plugin.ClientId);
            Assert.Equal("mcp:plugin", plugin.Scope);

            // v1 compat mirror = the plugin family's token fields (04 §1); subject backfilled (O5);
            // serverTarget recorded.
            Assert.Equal("acc-plugin", persisted.AccessToken);
            Assert.Equal("usr_1", persisted.Subject);
            Assert.Equal(AsBaseUrl, persisted.ServerTarget);

            // The connection presents the PLUGIN (hub) token.
            Assert.Equal("acc-plugin", await account.AccessTokenProvider());

            // The wire: the device-authorization request carried scope=mcp:agent (F1.2), and the exchange
            // carried the token-exchange grant with this component's client_id.
            var authorizeBody = server.Requests.Single(r => r.Path.EndsWith("/oauth/device_authorization")).Body;
            Assert.Contains("scope=" + WebUtility.UrlEncode("mcp:agent"), authorizeBody);
            var exchangeBody = server.DecodedTokenRequests.Single(b => b.Contains("grant-type:token-exchange"));
            Assert.Contains("client_id=" + ComponentClientId, exchangeBody);
            Assert.Contains("subject_token=acc-agent", exchangeBody);

            // A fresh coordinator over the SAME store auto-adopts (the once-per-machine sign-in seen by a
            // second editor session / another engine).
            using var account2 = MakeAccount(tmp, FakeAuthServer.Throwing());
            Assert.True(account2.IsSignedIn);
        }

        [Fact]
        public async Task SignInAsync_Denied_DoesNotPersist()
        {
            using var tmp = new TempDir();
            var server = new FakeAuthServer
            {
                DeviceAuthorize = Ok(DeviceAuthorizeJson("USER-1", "dev-1")),
                DeviceToken = Error("access_denied"),
            };
            using var account = MakeAccount(tmp, server);

            var outcome = await account.SignInAsync(MakeFlow(server), AsBaseUrl);

            Assert.Equal(GodotAccountSignInStatus.NotAuthorized, outcome.Status);
            Assert.False(account.IsSignedIn);
            Assert.False(Store(tmp).Exists);
        }

        /// <summary>
        /// The F1 failure path: the exchange fails after the agent family committed under the first hold —
        /// the agent family STAYS committed (that is why the sequence uses two separate holds), the outcome
        /// is PartiallyAuthorized, and the account is NOT hub-signed-in (no plugin-plane credential).
        /// </summary>
        [Fact]
        public async Task SignInAsync_ExchangeFails_CommitsAgentFamilyOnly()
        {
            using var tmp = new TempDir();
            var server = new FakeAuthServer
            {
                DeviceAuthorize = Ok(DeviceAuthorizeJson("USER-1", "dev-1")),
                DeviceToken = Ok(TokenJson("acc-agent", "ref-agent", 3600, scope: "mcp:agent")),
                Exchange = () => JsonResponse(HttpStatusCode.BadRequest, "{ \"error\": \"invalid_request\" }"),
            };
            using var account = MakeAccount(tmp, server);

            var outcome = await account.SignInAsync(MakeFlow(server), AsBaseUrl);

            Assert.Equal(GodotAccountSignInStatus.PartiallyAuthorized, outcome.Status);
            Assert.False(account.IsSignedIn); // no plugin-plane family ⇒ not hub-signed-in

            var persisted = Store(tmp).Read();
            Assert.NotNull(persisted?.Families?.Agent);
            Assert.Equal("acc-agent", persisted!.Families!.Agent!.AccessToken);
            Assert.Null(persisted.Families.Plugin);
        }

        // --- Refresh wire shape (04 §3 — the G-SEC-1 plant-3 discriminators) ---

        /// <summary>
        /// 04 §3.2: the refresh request presents the family's STORED <c>clientId</c> — never the component
        /// default. Discriminates the adapter dropping the family context (the pre-b3 defect class).
        /// </summary>
        [Fact]
        public async Task Refresh_PresentsTheFamilysStoredClientId_NeverTheComponentDefault()
        {
            using var tmp = new TempDir();
            WritePluginFamily(tmp, accessToken: "acc-old", refreshToken: "ref-old",
                expiresAt: AlreadyExpired, clientId: "stored-custom-id");

            var server = new FakeAuthServer { Refresh = Ok(TokenJson("acc-new", "ref-new", 3600)) };
            using var account = MakeAccount(tmp, server);

            var refreshed = await account.RefreshAsync();

            Assert.True(refreshed);
            var refreshBody = server.DecodedTokenRequests.Single(b => b.Contains("grant_type=refresh_token"));
            Assert.Contains("client_id=stored-custom-id", refreshBody);
            Assert.DoesNotContain("client_id=" + ComponentClientId, refreshBody);
        }

        /// <summary>04 §3.3 / P0-3: the refresh request omits <c>scope</c> and <c>resource</c> entirely.</summary>
        [Fact]
        public async Task Refresh_OmitsScopeAndResourceEntirely()
        {
            using var tmp = new TempDir();
            WritePluginFamily(tmp, accessToken: "acc-old", refreshToken: "ref-old",
                expiresAt: AlreadyExpired, clientId: "stored-custom-id");

            var server = new FakeAuthServer { Refresh = Ok(TokenJson("acc-new", "ref-new", 3600)) };
            using var account = MakeAccount(tmp, server);

            var refreshed = await account.RefreshAsync();

            Assert.True(refreshed);
            var refreshBody = server.DecodedTokenRequests.Single(b => b.Contains("grant_type=refresh_token"));
            Assert.DoesNotContain("scope=", refreshBody);
            Assert.DoesNotContain("resource=", refreshBody);
        }

        /// <summary>04 §3.7: a legacy family of unknown id refreshes with the component default.</summary>
        [Fact]
        public async Task Refresh_LegacyFamily_PresentsTheComponentDefault_AndStillOmitsScope()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "acc-old",
                RefreshToken = "ref-old",
                ExpiresAt = AlreadyExpired,
                ServerTarget = AsBaseUrl,
            });

            var server = new FakeAuthServer { Refresh = Ok(TokenJson("acc-new", "ref-new", 3600)) };
            using var account = MakeAccount(tmp, server);

            var refreshed = await account.RefreshAsync();

            Assert.True(refreshed);
            var refreshBody = server.DecodedTokenRequests.Single(b => b.Contains("grant_type=refresh_token"));
            Assert.Contains("client_id=" + ComponentClientId, refreshBody);
            Assert.DoesNotContain("scope=", refreshBody);
        }

        // --- Expiry self-heal (08 A1): proactive refresh inside the access-token provider ---

        [Fact]
        public async Task AccessTokenProvider_ExpiredToken_RefreshesProactively_AndRotatesTheStore()
        {
            using var tmp = new TempDir();
            WritePluginFamily(tmp, accessToken: "acc-old", refreshToken: "ref-old",
                expiresAt: AlreadyExpired, clientId: ComponentClientId);

            var server = new FakeAuthServer { Refresh = Ok(TokenJson("acc-new", "ref-new", 3600)) };
            using var account = MakeAccount(tmp, server);

            // The provider notices the expired token inside the skew window and refreshes BEFORE returning.
            var token = await account.AccessTokenProvider();

            Assert.Equal("acc-new", token);
            var persisted = Store(tmp).Read();
            Assert.Equal("acc-new", persisted!.Families?.Plugin?.AccessToken);
            Assert.Equal("ref-new", persisted.Families?.Plugin?.RefreshToken);
            Assert.Equal(AsBaseUrl, persisted.ServerTarget); // identity fields preserved across rotation
        }

        [Fact]
        public async Task RefreshAsync_ServerRejects_FailsClosed_KeepsFailureShape()
        {
            using var tmp = new TempDir();
            WritePluginFamily(tmp, accessToken: "acc-old", refreshToken: "ref-old",
                expiresAt: AlreadyExpired, clientId: ComponentClientId);

            var server = new FakeAuthServer
            {
                Refresh = () => JsonResponse(HttpStatusCode.BadRequest, "{ \"error\": \"invalid_grant\" }"),
            };
            using var account = MakeAccount(tmp, server);

            var refreshed = await account.RefreshAsync();

            Assert.False(refreshed);
        }

        // --- Sign-out ---

        [Fact]
        public void SignOut_LocalOnly_WipesStore()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials { AccessToken = "a", RefreshToken = "r", ServerTarget = AsBaseUrl });

            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());
            Assert.True(account.IsSignedIn);

            account.SignOut();

            Assert.False(account.IsSignedIn);
            Assert.False(Store(tmp).Exists);
        }

        /// <summary>
        /// F6: machine-wide sign-out revokes EVERY stored family's refresh token — each with its stored
        /// <c>clientId</c> — then deletes the store via the lock protocol; every other coordinator observes
        /// the machine signed out.
        /// </summary>
        [Fact]
        public async Task SignOutMachineWideAsync_RevokesEveryFamily_AndDeletesTheStore()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials
            {
                ServerTarget = AsBaseUrl,
                Families = new MachineCredentialFamilies
                {
                    Agent = new MachineCredentialFamily
                    {
                        AccessToken = "acc-agent", RefreshToken = "ref-agent",
                        ClientId = "agent-client-id", Scope = "mcp:agent",
                    },
                    Plugin = new MachineCredentialFamily
                    {
                        AccessToken = "acc-plugin", RefreshToken = "ref-plugin",
                        ClientId = "plugin-client-id", Scope = "mcp:plugin",
                    },
                },
            });

            var server = new FakeAuthServer(); // /oauth/revoke answers 200 by default
            using var account = MakeAccount(tmp, server);
            Assert.True(account.IsSignedIn);

            var result = await account.SignOutMachineWideAsync();

            Assert.True(result.StoreDeleted);
            Assert.False(result.Busy);
            Assert.Equal(2, result.FamiliesRevoked);
            Assert.False(Store(tmp).Exists);
            Assert.False(account.IsSignedIn);

            // Each family was revoked with ITS OWN stored clientId (F6.2).
            var revokes = server.Requests.Where(r => r.Path.EndsWith("/oauth/revoke"))
                .Select(r => WebUtility.UrlDecode(r.Body)).ToList();
            Assert.Equal(2, revokes.Count);
            Assert.Contains(revokes, b => b.Contains("token=ref-agent") && b.Contains("client_id=agent-client-id"));
            Assert.Contains(revokes, b => b.Contains("token=ref-plugin") && b.Contains("client_id=plugin-client-id"));

            using var account2 = MakeAccount(tmp, FakeAuthServer.Throwing());
            Assert.False(account2.IsSignedIn);
        }

        // --- Second-phase completion (03 F1 failure path — review B1) ---

        /// <summary>
        /// 03 F1: "Token exchange fails → P retries with backoff; the agent family stays committed."
        /// A transiently failing exchange is retried ONCE (with the documented backoff) without a second
        /// device flow, and the retry completes the plugin-family commit.
        /// </summary>
        [Fact]
        public async Task SignInAsync_ExchangeFailsOnce_RetriesWithBackoff_AndFullyCommits()
        {
            using var tmp = new TempDir();
            var exchangeCalls = 0;
            var server = new FakeAuthServer
            {
                DeviceAuthorize = Ok(DeviceAuthorizeJson("USER-1", "dev-1")),
                DeviceToken = Ok(TokenJson("acc-agent", "ref-agent", 3600, scope: "mcp:agent")),
                Exchange = () => ++exchangeCalls == 1
                    ? JsonResponse(HttpStatusCode.BadRequest, "{ \"error\": \"temporarily_unavailable\" }")
                    : JsonResponse(HttpStatusCode.OK, ExchangeJson("acc-plugin", "ref-plugin", 3600, "mcp:plugin", "usr_1")),
            };
            var delays = new List<TimeSpan>();
            using var account = MakeAccount(tmp, server, delay: (d, _) => { delays.Add(d); return Task.CompletedTask; });

            var outcome = await account.SignInAsync(MakeFlow(server), AsBaseUrl);

            Assert.Equal(GodotAccountSignInStatus.SignedIn, outcome.Status);
            Assert.True(account.IsSignedIn);
            Assert.Equal(2, exchangeCalls); // exactly one retry — bounded
            Assert.Equal(new[] { GodotAccountAuth.SecondPhaseRetryBackoff }, delays); // with the documented backoff
            // ONE device flow only — the retry never re-runs RFC 8628 (no second browser round).
            Assert.Single(server.Requests, r => r.Path.EndsWith("/oauth/device_authorization", StringComparison.Ordinal));

            var persisted = Store(tmp).Read();
            Assert.Equal("acc-agent", persisted!.Families?.Agent?.AccessToken);
            Assert.Equal("acc-plugin", persisted.Families?.Plugin?.AccessToken);
        }

        /// <summary>
        /// Review B1: a retryable second-hold outcome (store unreadable between the holds) is retried
        /// with the CARRIED mint — never re-exchanged — and the retry commits the plugin family.
        /// </summary>
        [Fact]
        public async Task SignInAsync_StoreUnreadableAtSecondHold_RetriesTheCarriedMint_WithoutReExchanging()
        {
            using var tmp = new TempDir();
            var storePath = Store(tmp).CredentialsPath;
            byte[]? agentDocBytes = null;
            var server = new FakeAuthServer
            {
                DeviceAuthorize = Ok(DeviceAuthorizeJson("USER-1", "dev-1")),
                DeviceToken = Ok(TokenJson("acc-agent", "ref-agent", 3600, scope: "mcp:agent")),
                Exchange = () =>
                {
                    // Runs BETWEEN the holds: capture the agent-family document hold 1 wrote, then turn
                    // the store unreadable (garbage bytes: DPAPI unprotect fails on Windows, JSON parse
                    // fails on POSIX) so hold 2 lands on StoreUnreadable with the mint carried back.
                    agentDocBytes = File.ReadAllBytes(storePath);
                    File.WriteAllBytes(storePath, new byte[] { 0x00, 0x01, 0xFF });
                    return JsonResponse(HttpStatusCode.OK, ExchangeJson("acc-plugin", "ref-plugin", 3600, "mcp:plugin", "usr_1"));
                },
            };
            // The injected delay is the retry backoff hook — "repair" the store there, so the ONE
            // bounded retry finds it readable again.
            using var account = MakeAccount(tmp, server, delay: (_, _) =>
            {
                File.WriteAllBytes(storePath, agentDocBytes!);
                return Task.CompletedTask;
            });

            var outcome = await account.SignInAsync(MakeFlow(server), AsBaseUrl);

            Assert.Equal(GodotAccountSignInStatus.SignedIn, outcome.Status);
            Assert.True(account.IsSignedIn);
            // The carried ExchangeResult was committed — exactly ONE exchange on the wire.
            Assert.Single(server.DecodedTokenRequests, b => b.Contains("grant-type:token-exchange"));

            var persisted = Store(tmp).Read();
            Assert.Equal("acc-agent", persisted!.Families?.Agent?.AccessToken);
            Assert.Equal("acc-plugin", persisted.Families?.Plugin?.AccessToken);
            Assert.Equal("usr_1", persisted.Subject); // O5 sub backfilled by the retried commit
        }

        /// <summary>
        /// Review B1 / b3 twin rule 4: when the bounded retry ALSO fails, the coordinator stops retrying
        /// (the user's next step is a fresh device flow) — so the minted-but-never-committed plugin
        /// family must be best-effort revoked, not silently stranded live server-side for ≤30 d. The
        /// unreadable store is never overwritten on this path either.
        /// </summary>
        [Fact]
        public async Task SignInAsync_StoreStillUnreadableAfterRetry_RevokesTheAbandonedMint()
        {
            using var tmp = new TempDir();
            var storePath = Store(tmp).CredentialsPath;
            var garbage = new byte[] { 0x00, 0x01, 0xFF };
            var server = new FakeAuthServer
            {
                DeviceAuthorize = Ok(DeviceAuthorizeJson("USER-1", "dev-1")),
                DeviceToken = Ok(TokenJson("acc-agent", "ref-agent", 3600, scope: "mcp:agent")),
                Exchange = () =>
                {
                    File.WriteAllBytes(storePath, garbage); // unreadable — and never repaired
                    return JsonResponse(HttpStatusCode.OK, ExchangeJson("acc-plugin", "ref-plugin", 3600, "mcp:plugin", "usr_1"));
                },
            };
            using var account = MakeAccount(tmp, server);

            var outcome = await account.SignInAsync(MakeFlow(server), AsBaseUrl);

            Assert.Equal(GodotAccountSignInStatus.PartiallyAuthorized, outcome.Status);
            Assert.False(account.IsSignedIn);
            // No re-exchange, no second device flow — the same mint was retried, then abandoned.
            Assert.Single(server.DecodedTokenRequests, b => b.Contains("grant-type:token-exchange"));
            Assert.Single(server.Requests, r => r.Path.EndsWith("/oauth/device_authorization", StringComparison.Ordinal));
            // The abandoned mint was best-effort revoked — refresh token preferred, this component's id.
            var revokes = server.Requests.Where(r => r.Path.EndsWith("/oauth/revoke", StringComparison.Ordinal))
                .Select(r => WebUtility.UrlDecode(r.Body)).ToList();
            Assert.Contains(revokes, b => b.Contains("token=ref-plugin") && b.Contains("client_id=" + ComponentClientId));
            // 04 §1: the unreadable store was never overwritten by any of it.
            Assert.Equal(garbage, File.ReadAllBytes(storePath));
        }

        // --- O8 / F11.2 legacy user:// cloudToken migration (the G-SEC-1 plant-2 target) ---

        /// <summary>
        /// Review B2(a) / 04 §1 "never overwrite": an EXISTING-but-unreadable store (DPAPI after a
        /// password reset, corruption) may hold a real credential — migration must skip and leave the
        /// file byte-identical. Fails when either Unreadable guard is removed (the migration would then
        /// treat the unreadable store as empty and write over it).
        /// </summary>
        [Fact]
        public void Migrate_UnreadableStore_IsNeverOverwritten()
        {
            using var tmp = new TempDir();
            Directory.CreateDirectory(tmp.Path);
            var storePath = Store(tmp).CredentialsPath;
            var garbage = new byte[] { 0x00, 0x10, 0xFF, 0x42 };
            File.WriteAllBytes(storePath, garbage);
            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());

            var outcome = account.TryMigrateLegacyCloudToken("sink-cloud-token", AsBaseUrl);

            Assert.Equal(GodotLegacyTokenMigrationResult.SkippedStoreUnreadable, outcome);
            Assert.Equal(garbage, File.ReadAllBytes(storePath)); // byte-identical — never overwritten
            Assert.False(account.IsSignedIn);
        }

        /// <summary>
        /// Review B2(b) / D9: a held machine lock makes the migration fail as Busy — it never proceeds
        /// lock-free and writes nothing; once the lock frees, the SAME call migrates (the idempotent
        /// retry-next-boot claim, proven rather than asserted).
        /// </summary>
        [Fact]
        public void Migrate_BusyLock_SkipsWithoutWriting_ThenMigratesOnceTheLockFrees()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, FakeAuthServer.Throwing(),
                credentialLock: MakeShortBudgetLock(tmp.Path));

            using (var holder = new MachineCredentialLock(tmp.Path).TryAcquire())
            {
                Assert.NotNull(holder); // a peer holds the machine lock for the whole attempt

                var outcome = account.TryMigrateLegacyCloudToken("sink-cloud-token", AsBaseUrl);

                Assert.Equal(GodotLegacyTokenMigrationResult.Busy, outcome);
                Assert.False(Store(tmp).Exists); // nothing was written without the lock (D9)
            }

            var second = account.TryMigrateLegacyCloudToken("sink-cloud-token", AsBaseUrl);

            Assert.Equal(GodotLegacyTokenMigrationResult.Migrated, second);
            Assert.Equal("sink-cloud-token", Store(tmp).Read()!.Families?.Legacy?.AccessToken);
            Assert.True(account.IsSignedIn);
        }

        [Fact]
        public async Task Migrate_SeededSink_EmptyStore_WritesLegacyFamilyUnderLock_AndSignsIn()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());
            Assert.False(account.IsSignedIn);

            var outcome = account.TryMigrateLegacyCloudToken("sink-cloud-token", AsBaseUrl);

            Assert.Equal(GodotLegacyTokenMigrationResult.Migrated, outcome);

            // POSITIVE artifact (not just an absence): the machine store now HOLDS the sink credential,
            // as a v2 legacy family + v1 mirror, and the account adopted it.
            var persisted = Store(tmp).Read();
            Assert.NotNull(persisted?.Families?.Legacy);
            Assert.Equal("sink-cloud-token", persisted!.Families!.Legacy!.AccessToken);
            Assert.Equal("sink-cloud-token", persisted.AccessToken); // compat mirror
            Assert.Equal(AsBaseUrl, persisted.ServerTarget);
            Assert.True(account.IsSignedIn);
            Assert.Equal("sink-cloud-token", await account.AccessTokenProvider());
        }

        [Fact]
        public void Migrate_StoreAlreadyHoldsCredential_SkipsAndLeavesStoreUntouched()
        {
            using var tmp = new TempDir();
            Store(tmp).Write(new MachineCredentials
            {
                AccessToken = "existing-access",
                RefreshToken = "existing-refresh",
                ServerTarget = AsBaseUrl,
            });
            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());

            var outcome = account.TryMigrateLegacyCloudToken("sink-cloud-token", AsBaseUrl);

            Assert.Equal(GodotLegacyTokenMigrationResult.SkippedStoreHasCredential, outcome);
            var persisted = Store(tmp).Read();
            Assert.Equal("existing-access", persisted!.AccessToken); // the store always wins over the sink
        }

        [Fact]
        public void Migrate_NoSinkToken_NoOp()
        {
            using var tmp = new TempDir();
            using var account = MakeAccount(tmp, FakeAuthServer.Throwing());

            Assert.Equal(GodotLegacyTokenMigrationResult.NoSinkToken, account.TryMigrateLegacyCloudToken(null, AsBaseUrl));
            Assert.Equal(GodotLegacyTokenMigrationResult.NoSinkToken, account.TryMigrateLegacyCloudToken("", AsBaseUrl));
            Assert.False(Store(tmp).Exists);
        }

        // --- helpers ---

        static MachineCredentialStore Store(TempDir tmp) => new(tmp.Path);

        static GodotAccountAuth MakeAccount(
            TempDir tmp,
            HttpMessageHandler handler,
            Func<TimeSpan, CancellationToken, Task>? delay = null,
            MachineCredentialLock? credentialLock = null)
            => new(
                asBaseUrlProvider: () => AsBaseUrl,
                store: new MachineCredentialStore(tmp.Path),
                httpClient: new HttpClient(handler),
                credentialLock: credentialLock,
                // Instant by default so the bounded second-phase backoff costs no test wall-clock;
                // tests that assert the backoff inject a recording delegate instead.
                delay: delay ?? ((_, _) => Task.CompletedTask));

        /// <summary>
        /// A <see cref="MachineCredentialLock"/> with a SHORT acquire budget (400 ms instead of the 75 s
        /// contract value), via the package's internal timing ctor
        /// <c>(baseDirectory, hostId, acquireBudgetMs, staleMs, foreignStaleMs, diagnostics)</c> —
        /// reflection because InternalsVisibleTo covers only McpPlugin.Tests. Staleness bars stay REAL
        /// (60 s / 24 h) so the held peer lock is never taken over mid-test. If the ctor shape changes
        /// in an upstream bump, this fails loudly here rather than silently testing nothing.
        /// </summary>
        static MachineCredentialLock MakeShortBudgetLock(string baseDirectory)
        {
            var ctor = typeof(MachineCredentialLock).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(string), typeof(string), typeof(int), typeof(long), typeof(long), typeof(Action<string>) },
                modifiers: null);
            Assert.True(ctor != null,
                "MachineCredentialLock's internal timing ctor (baseDirectory, hostId, acquireBudgetMs, staleMs, foreignStaleMs, diagnostics) "
                + "was not found — the McpPlugin package shape changed; update this test seam.");
            return (MachineCredentialLock)ctor!.Invoke(new object?[] { baseDirectory, null, 400, 60_000L, 86_400_000L, null });
        }

        static GodotDeviceAuthFlow MakeFlow(HttpMessageHandler handler)
            => new(
                new GodotDeviceAuthService(new HttpClient(handler)),
                delay: (_, _) => Task.CompletedTask,
                utcNow: () => DateTime.UtcNow);

        /// <summary>Seed a v2 store with a PLUGIN family (the shape the F1 commit writes).</summary>
        static void WritePluginFamily(TempDir tmp, string accessToken, string refreshToken,
            DateTimeOffset? expiresAt, string clientId)
            => Store(tmp).Write(new MachineCredentials
            {
                ServerTarget = AsBaseUrl,
                Families = new MachineCredentialFamilies
                {
                    Plugin = new MachineCredentialFamily
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        ClientId = clientId,
                        Scope = "mcp:plugin",
                    },
                },
            });

        static Func<HttpResponseMessage> Ok(string json) => () => JsonResponse(HttpStatusCode.OK, json);

        static Func<HttpResponseMessage> Error(string error)
            => () => JsonResponse(HttpStatusCode.BadRequest, $"{{ \"error\": \"{error}\" }}");

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

        static string TokenJson(string access, string refresh, int expiresIn, string scope = "mcp:plugin") => $$"""
            {
              "access_token": "{{access}}",
              "refresh_token": "{{refresh}}",
              "token_type": "Bearer",
              "expires_in": {{expiresIn}},
              "scope": "{{scope}}"
            }
            """;

        static string ExchangeJson(string access, string refresh, int expiresIn, string scope, string sub) => $$"""
            {
              "access_token": "{{access}}",
              "refresh_token": "{{refresh}}",
              "token_type": "Bearer",
              "expires_in": {{expiresIn}},
              "scope": "{{scope}}",
              "issued_token_type": "urn:ietf:params:oauth:token-type:access_token",
              "sub": "{{sub}}"
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

        /// <summary>
        /// A scripted fake ai-game.dev authorization server, routing by path + grant type:
        /// <c>/oauth/device_authorization</c> → <see cref="DeviceAuthorize"/>; <c>/oauth/token</c> by
        /// <c>grant_type</c> → <see cref="DeviceToken"/> / <see cref="Exchange"/> / <see cref="Refresh"/>;
        /// <c>/oauth/revoke</c> → <see cref="Revoke"/> (200 by default — RFC 7009). Records every request's
        /// path + raw form body for wire-shape assertions; an unscripted route throws (so a test that
        /// expects no network proves it).
        /// </summary>
        sealed class FakeAuthServer : HttpMessageHandler
        {
            public Func<HttpResponseMessage>? DeviceAuthorize { get; set; }
            public Func<HttpResponseMessage>? DeviceToken { get; set; }
            public Func<HttpResponseMessage>? Exchange { get; set; }
            public Func<HttpResponseMessage>? Refresh { get; set; }
            public Func<HttpResponseMessage>? Revoke { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);

            public List<(string Path, string Body)> Requests { get; } = new();

            /// <summary>URL-decoded bodies of every <c>/oauth/token</c> POST (for grant/field assertions).</summary>
            public IEnumerable<string> DecodedTokenRequests =>
                Requests.Where(r => r.Path.EndsWith("/oauth/token", StringComparison.Ordinal))
                        .Select(r => WebUtility.UrlDecode(r.Body));

            /// <summary>A server where EVERY route throws — proves a code path performs no network I/O.</summary>
            public static FakeAuthServer Throwing() => new() { Revoke = null };

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri!.AbsolutePath;
                var body = request.Content != null
                    ? await request.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;
                lock (Requests)
                    Requests.Add((path, body));

                var decoded = WebUtility.UrlDecode(body);
                Func<HttpResponseMessage>? responder =
                    path.EndsWith("/oauth/device_authorization", StringComparison.Ordinal) ? DeviceAuthorize
                    : path.EndsWith("/oauth/revoke", StringComparison.Ordinal) ? Revoke
                    : !path.EndsWith("/oauth/token", StringComparison.Ordinal) ? null
                    : decoded.Contains("grant-type:token-exchange") ? Exchange
                    : decoded.Contains("grant_type=refresh_token") ? Refresh
                    : DeviceToken;

                if (responder == null)
                    throw new InvalidOperationException($"unexpected/unscripted request: {path}"); // path only — never form fields (a revoke body leads with the token)

                return responder();
            }
        }
    }
}
