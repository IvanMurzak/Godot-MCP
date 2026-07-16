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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using Xunit;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// BUG-B regression guard (mcp-authorize i3 — the Godot analog of the Unity i2 local-token-lifecycle
    /// bug). On Unity, in Custom → <c>token</c> mode the local secret REGENERATED after the Configure button
    /// wrote the AI-client configs, so the already-written config held a STALE token that no longer matched
    /// the running local server's launch token → Claude Code got a 401. The audit (see the i3 PR description)
    /// found the bug is NOT present on Godot, and these tests PIN the structural reasons so a future change
    /// cannot silently reintroduce it:
    ///
    /// <list type="number">
    ///   <item><b>Stable offline secret.</b> The Custom-mode secret is a single plain persisted field
    ///   <see cref="GodotMcpConfig.CustomToken"/> (serialized as <c>token</c>) with a deterministic
    ///   System.Text.Json round-trip — NOT a mode-routed computed property that serializes. The routing
    ///   accessor <see cref="GodotMcpConfig.Token"/> is <c>[JsonIgnore]</c>, so the JSON-field-order
    ///   regeneration that bit Unity cannot occur, and there is NO <c>SetDefault</c>/generate-on-reconstruct
    ///   path — <see cref="GodotMcpTokenGenerator.Generate"/> is only ever called from two explicit user
    ///   actions in <c>ConnectionPanel</c> (the "New" button; switch-to-token-when-empty).</item>
    ///   <item><b>One source for both readers.</b> The value the AI-client config write embeds
    ///   (<see cref="AgentConfiguratorSettingsFactory.Create"/> → <see cref="GodotMcpConfig.Token"/> →
    ///   <see cref="AgentConfiguratorCredentialPolicy.ResolveSettingsToken"/>) and the value the local server
    ///   is launched with (<c>ConnectionPanel.OnServerStartStopPressed</c> →
    ///   <see cref="GodotMcpConfig.ResolveCustomToken"/>) BOTH resolve from the same
    ///   <see cref="GodotMcpConfig.CustomToken"/> with identical env precedence, so they cannot diverge at any
    ///   instant.</item>
    /// </list>
    ///
    /// <para>
    /// These are pure-managed asserts (no Godot native types / no <c>#if TOOLS</c>): the launch path reads
    /// <see cref="GodotMcpConfig.ResolveCustomToken"/> directly, and the config-write path's token byte-source
    /// is <see cref="AgentConfiguratorCredentialPolicy.ResolveSettingsToken"/> over <see cref="GodotMcpConfig.Token"/> —
    /// both unit-testable here. The tests read env live (via <see cref="GodotMcpConfig.ResolveCustomToken"/>),
    /// so they join the shared <c>env</c> collection and clear/restore the <c>GODOT_MCP_*</c> vars via
    /// <see cref="EnvScope"/> (mirroring <c>GodotMcpConfigStoreTests</c>).
    /// </para>
    /// </summary>
    [Collection("env")]
    public class GodotMcpLocalTokenLifecycleTests : IDisposable
    {
        const string Secret = "offline-secret-tok-ABC123";

        readonly string _dir;

        public GodotMcpLocalTokenLifecycleTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "godot-mcp-tokenlife-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_dir))
                    Directory.Delete(_dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; a leaked temp dir is harmless.
            }
        }

        string PathFor(string name) => Path.Combine(_dir, name);

        static GodotMcpConfig NewCustomTokenConfig() => new()
        {
            ConnectionMode = GodotMcpConnectionMode.Custom,
            AuthOption = McpServerConsts.AuthOption.token,
            CustomHost = "http://localhost:5300",
            CustomToken = Secret
        };

        /// <summary>
        /// The exact token bytes the AI-client config write path embeds. Models the REAL Configure path
        /// (<c>AgentConfiguratorsPanel.CurrentCredentialMode</c> → <see cref="AgentConfiguratorSettingsFactory.Create"/>):
        /// the credential mode is RESOLVED by the same pure-managed policy the panel uses — a Custom+<c>token</c>
        /// config FORCES <see cref="AgentConfig.HttpCredentialMode.AccessToken"/> (mcp-authorize g5). Deriving it
        /// (rather than hardcoding AccessToken) also pins that forcing, so a regression of it — which would write a
        /// tokenless URL-only client config against the Bearer-gated local server, a BUG-B-class mismatch — is
        /// caught here too. The default-configurator golden-path inputs (<c>supportsOAuth: true</c>,
        /// <c>useAccessToken: false</c>) are the case where a lost forcing would silently drop the token.
        /// </summary>
        static string ConfigWriteToken(GodotMcpConfig c)
        {
            AgentConfig.HttpCredentialMode credentialMode = AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                c.ActiveMode, c.ActiveAuthOption, supportsOAuth: true, useAccessToken: false);
            return AgentConfiguratorCredentialPolicy.ResolveSettingsToken(credentialMode, c.Token);
        }

        /// <summary>The exact token bytes the local server is launched with (token mode).</summary>
        static string? ServerLaunchToken(GodotMcpConfig c) => c.ResolveCustomToken();

        // --- 1. The offline secret is STABLE across a Save→Load round-trip (no regeneration on load). ---

        [Fact]
        public void CustomToken_StableAcrossSaveLoadRoundTrip_InTokenMode()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("config.json");
            GodotMcpConfigStore.Save(path, NewCustomTokenConfig());

            var loaded = GodotMcpConfigStore.Load(path);

            Assert.NotNull(loaded);
            // Reload must NOT regenerate the secret: the persisted backing field, the routing accessor, and the
            // server-launch resolver all still equal the ORIGINAL secret.
            Assert.Equal(Secret, loaded!.CustomToken);
            Assert.Equal(Secret, loaded.Token);
            Assert.Equal(Secret, ServerLaunchToken(loaded));
        }

        // --- 2. The config-write token == the server-launch token (before AND after a round-trip). ---

        [Fact]
        public void ConfigWriteToken_EqualsServerLaunchToken_InTokenMode()
        {
            using var _ = EnvScope.ClearAll();

            var config = NewCustomTokenConfig();

            // The bytes the AI-client config embeds == the bytes the local server is launched with == the secret.
            Assert.Equal(Secret, ConfigWriteToken(config));
            Assert.Equal(Secret, ServerLaunchToken(config));
            Assert.Equal(ServerLaunchToken(config), ConfigWriteToken(config));
        }

        [Fact]
        public void ConfigWriteToken_EqualsServerLaunchToken_AfterSaveLoadRoundTrip_InTokenMode()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("config.json");
            GodotMcpConfigStore.Save(path, NewCustomTokenConfig());
            var loaded = GodotMcpConfigStore.Load(path);
            Assert.NotNull(loaded);

            // This is the exact BUG-B invariant: after Configure (config-write) in token mode, the written
            // client-config token still equals the server launch token even across an editor-reload round-trip.
            Assert.Equal(Secret, ConfigWriteToken(loaded!));
            Assert.Equal(Secret, ServerLaunchToken(loaded));
            Assert.Equal(ServerLaunchToken(loaded), ConfigWriteToken(loaded));
        }

        // --- 3. The boot seed path (ApplyPersisted) does NOT regenerate or clear the token. ---

        [Fact]
        public void ApplyPersisted_DoesNotRegenerateToken_InTokenMode()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("config.json");
            GodotMcpConfigStore.Save(path, NewCustomTokenConfig());
            var persisted = GodotMcpConfigStore.Load(path);
            Assert.NotNull(persisted);

            // Boot order: seed a fresh default config from the persisted layer (no .env / process-env override).
            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, persisted);
            GodotMcpEnvFile.Apply(target, new Dictionary<string, string>());

            Assert.Equal(GodotMcpConnectionMode.Custom, target.ActiveMode);
            Assert.Equal(McpServerConsts.AuthOption.token, target.ActiveAuthOption);
            // The seeded token is the persisted secret, unchanged — no SetDefault-style regeneration on boot.
            Assert.Equal(Secret, target.CustomToken);
            Assert.Equal(Secret, target.Token);
            Assert.Equal(ServerLaunchToken(target), ConfigWriteToken(target));
        }

        // --- 4. The offline secret is idempotent across a DOUBLE round-trip (never re-minted on persist). ---

        [Fact]
        public void CustomToken_Idempotent_AcrossDoubleRoundTrip()
        {
            using var _ = EnvScope.ClearAll();

            var path1 = PathFor("config-1.json");
            var path2 = PathFor("config-2.json");

            GodotMcpConfigStore.Save(path1, NewCustomTokenConfig());
            var loaded1 = GodotMcpConfigStore.Load(path1);
            Assert.NotNull(loaded1);

            GodotMcpConfigStore.Save(path2, loaded1!);
            var loaded2 = GodotMcpConfigStore.Load(path2);
            Assert.NotNull(loaded2);

            Assert.Equal(Secret, loaded2!.CustomToken);
            // The persisted `token` bytes are identical across both writes — the secret is round-tripped, not re-minted.
            Assert.Equal(File.ReadAllText(path1), File.ReadAllText(path2));
        }

        /// <summary>
        /// Sets/clears the five <c>GODOT_MCP_*</c> process env vars that <see cref="GodotMcpConfig"/> reads
        /// live and restores prior values on dispose (mirrors <c>GodotMcpConfigStoreTests.EnvScope</c>), so the
        /// token-resolution asserts stay deterministic under the shared <c>env</c> collection.
        /// </summary>
        sealed class EnvScope : IDisposable
        {
            static readonly string[] Names =
            {
                GodotMcpConfig.EnvCloudUrl,
                GodotMcpConfig.EnvHost,
                GodotMcpConfig.EnvToken,
                GodotMcpConfig.EnvConnectionMode,
                GodotMcpConfig.EnvAuthOption
            };

            readonly Dictionary<string, string?> _prior = new();

            EnvScope()
            {
                foreach (var name in Names)
                    _prior[name] = Environment.GetEnvironmentVariable(name);
            }

            public static EnvScope ClearAll()
            {
                var scope = new EnvScope();
                foreach (var name in Names)
                    Environment.SetEnvironmentVariable(name, null);
                return scope;
            }

            public void Dispose()
            {
                foreach (var kv in _prior)
                    Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }
    }
}
