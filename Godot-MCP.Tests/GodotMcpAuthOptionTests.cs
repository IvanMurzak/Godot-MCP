/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.IO;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the Custom-mode authorization option — now the SHARED
    /// <c>com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server.AuthOption</c> (<c>none</c>/<c>oauth</c>/<c>token</c>),
    /// the per-engine <c>GodotMcpAuthOption</c> having been retired in mcp-authorize g6: serialize / round-trip
    /// through <see cref="GodotMcpConfigStore"/>, the legacy <c>required</c> → <c>token</c> migration, the env
    /// override precedence (env &gt; persisted &gt; default), and the <see cref="GodotMcpConfig.ResolveCustomToken"/>
    /// routing (no bearer when auth is <c>none</c>; the account JWT in <c>oauth</c>).
    ///
    /// <para>
    /// Tests that mutate process env are marked <c>[Collection("env")]</c> to serialize against the
    /// shared env, and restore prior values via <see cref="AuthEnvScope"/>.
    /// </para>
    /// </summary>
    [Collection("env")]
    public class GodotMcpAuthOptionTests
    {
        // --- Default ---

        [Fact]
        public void DefaultAuthOption_IsNone()
        {
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig();
            Assert.Equal(McpServerConsts.AuthOption.none, config.ActiveAuthOption);
        }

        // --- Env override precedence ---

        [Theory]
        [InlineData("token", McpServerConsts.AuthOption.token)]
        [InlineData("Token", McpServerConsts.AuthOption.token)]
        [InlineData("oauth", McpServerConsts.AuthOption.oauth)]
        [InlineData("OAUTH", McpServerConsts.AuthOption.oauth)]
        [InlineData("none", McpServerConsts.AuthOption.none)]
        [InlineData("NONE", McpServerConsts.AuthOption.none)]
        public void EnvAuthOption_Overrides_ConfiguredValue(string envValue, McpServerConsts.AuthOption expected)
        {
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, envValue);
            // Configured = none; env should win (including the round-trip none case).
            var config = new GodotMcpConfig { AuthOption = McpServerConsts.AuthOption.none };
            Assert.Equal(expected, config.ActiveAuthOption);
        }

        [Theory]
        [InlineData("Required")]
        [InlineData("required")]
        public void EnvAuthOption_LegacyRequired_NormalizesToToken(string envValue)
        {
            // The retired `required` shared-token name is accepted and normalized to the offline `token` mode
            // everywhere (so an un-migrated env value never reaches the fail-closed shared launch builder).
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, envValue);
            var config = new GodotMcpConfig { AuthOption = McpServerConsts.AuthOption.none };
            Assert.Equal(McpServerConsts.AuthOption.token, config.ActiveAuthOption);
        }

        [Theory]
        [InlineData("not-an-option")]
        [InlineData("1")]
        [InlineData("")]
        public void EnvAuthOption_Unrecognized_FallsBackToConfigured(string envValue)
        {
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, envValue);
            var config = new GodotMcpConfig { AuthOption = McpServerConsts.AuthOption.token };
            Assert.Equal(McpServerConsts.AuthOption.token, config.ActiveAuthOption);
        }

        // --- NormalizeAuthOption (legacy + unknown mapping) ---

        [Theory]
        [InlineData(McpServerConsts.AuthOption.none, McpServerConsts.AuthOption.none)]
        [InlineData(McpServerConsts.AuthOption.oauth, McpServerConsts.AuthOption.oauth)]
        [InlineData(McpServerConsts.AuthOption.token, McpServerConsts.AuthOption.token)]
        [InlineData(McpServerConsts.AuthOption.required, McpServerConsts.AuthOption.token)] // retired → offline token
        [InlineData(McpServerConsts.AuthOption.unknown, McpServerConsts.AuthOption.none)]   // crash-safe fallback
        public void NormalizeAuthOption_MapsLegacyAndUnknown(McpServerConsts.AuthOption input, McpServerConsts.AuthOption expected)
        {
            Assert.Equal(expected, GodotMcpConfig.NormalizeAuthOption(input));
        }

        // --- ResolveCustomToken routing ---

        [Fact]
        public void ResolveCustomToken_None_ReturnsNull_EvenWithStoredToken()
        {
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = McpServerConsts.AuthOption.none,
                CustomToken = "stored-token-keeps-its-value"
            };
            // Anonymous connection: no bearer on the wire, but the stored value is NOT discarded.
            Assert.Null(config.ResolveCustomToken());
            Assert.Equal("stored-token-keeps-its-value", config.CustomToken);
        }

        [Fact]
        public void ResolveCustomToken_Token_ReturnsStoredToken()
        {
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = McpServerConsts.AuthOption.token,
                CustomToken = "abc123"
            };
            Assert.Equal("abc123", config.ResolveCustomToken());
        }

        [Fact]
        public void ResolveCustomToken_Token_EnvTokenWinsOverStored()
        {
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvToken, "env-token");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = McpServerConsts.AuthOption.token,
                CustomToken = "stored"
            };
            Assert.Equal("env-token", config.ResolveCustomToken());
        }

        [Fact]
        public void ResolveCustomToken_EnvAuthToken_OverridesPersistedNone()
        {
            // Persisted none, but env forces token => the stored token now flows to the bearer.
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, "token");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = McpServerConsts.AuthOption.none,
                CustomToken = "stored"
            };
            Assert.Equal(McpServerConsts.AuthOption.token, config.ActiveAuthOption);
            Assert.Equal("stored", config.ResolveCustomToken());
        }

        [Fact]
        public void ResolveCustomToken_Oauth_ReturnsAccountJwt()
        {
            // oauth-local presents the signed-in ACCOUNT JWT (the same credential the Cloud path uses), which
            // the loopback server validates against ai-game.dev's JWKS (design Part A).
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = McpServerConsts.AuthOption.oauth,
                CloudToken = "account-jwt",
                CustomToken = "unused-static-secret"
            };
            Assert.Equal("account-jwt", config.ResolveCustomToken());
        }

        [Fact]
        public void ResolveCustomToken_Oauth_NoAccountJwt_ReturnsNull()
        {
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = McpServerConsts.AuthOption.oauth,
                CloudToken = null
            };
            Assert.Null(config.ResolveCustomToken());
        }

        // --- Serialization round-trip via the store ---

        [Fact]
        public void AuthOption_Serializes_AsNamedValue()
        {
            var config = new GodotMcpConfig { AuthOption = McpServerConsts.AuthOption.token };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            Assert.Contains("\"authOption\"", json);
            Assert.Contains("token", json);
        }

        [Fact]
        public void AuthOption_RoundTrips_ThroughStore()
        {
            using var _ = AuthEnvScope.ClearAll();
            var path = Path.Combine(Path.GetTempPath(), $"godot-mcp-auth-{Guid.NewGuid():N}.json");
            try
            {
                var saved = new GodotMcpConfig
                {
                    ConnectionMode = GodotMcpConnectionMode.Custom,
                    AuthOption = McpServerConsts.AuthOption.token,
                    CustomToken = "round-trip-token"
                };
                GodotMcpConfigStore.Save(path, saved);

                var loaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(loaded);
                Assert.Equal(McpServerConsts.AuthOption.token, loaded!.AuthOption);

                // ApplyPersisted seeds the auth option onto a fresh target (the boot precedence path).
                var target = new GodotMcpConfig();
                GodotMcpConfigStore.ApplyPersisted(target, loaded);
                Assert.Equal(McpServerConsts.AuthOption.token, target.AuthOption);
                Assert.Equal("round-trip-token", target.CustomToken);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void Store_MigratesLegacyRequiredConfig_ToToken()
        {
            // An old config written with the retired `authorization=required` (persisted as `AuthOption.required`)
            // must self-heal to the offline `token` mode on load — else the addon keeps launching the local
            // server with `required`, which the shared launch-arg builder fail-closed rejects (mcp-authorize g6).
            using var _ = AuthEnvScope.ClearAll();
            var path = Path.Combine(Path.GetTempPath(), $"godot-mcp-auth-legacy-{Guid.NewGuid():N}.json");
            try
            {
                // Hand-craft a legacy on-disk config (case-insensitive enum read maps "Required" → required).
                File.WriteAllText(path, "{\"host\":\"http://localhost:8080\",\"authOption\":\"Required\",\"token\":\"legacy-secret\"}");

                var loaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(loaded);
                Assert.Equal(McpServerConsts.AuthOption.token, loaded!.AuthOption);
                Assert.Equal("legacy-secret", loaded.CustomToken);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void ApplyPersisted_Default_RoundTripsNone()
        {
            using var _ = AuthEnvScope.ClearAll();
            var persisted = new GodotMcpConfig { AuthOption = McpServerConsts.AuthOption.none };
            var target = new GodotMcpConfig { AuthOption = McpServerConsts.AuthOption.token };
            GodotMcpConfigStore.ApplyPersisted(target, persisted);
            Assert.Equal(McpServerConsts.AuthOption.none, target.AuthOption);
        }

        /// <summary>
        /// Local env scope covering the keys these auth tests touch (auth option + token). Restores prior
        /// values on dispose so cross-test env leakage cannot occur.
        /// </summary>
        sealed class AuthEnvScope : IDisposable
        {
            static readonly string[] Names =
            {
                GodotMcpConfig.EnvAuthOption,
                GodotMcpConfig.EnvToken,
                GodotMcpConfig.EnvConnectionMode,
                GodotMcpConfig.EnvHost
            };

            readonly System.Collections.Generic.Dictionary<string, string?> _prior = new();

            AuthEnvScope()
            {
                foreach (var name in Names)
                    _prior[name] = Environment.GetEnvironmentVariable(name);
            }

            public static AuthEnvScope ClearAll()
            {
                var scope = new AuthEnvScope();
                foreach (var name in Names)
                    Environment.SetEnvironmentVariable(name, null);
                return scope;
            }

            public static AuthEnvScope Set(string name, string? value)
            {
                var scope = ClearAll();
                Environment.SetEnvironmentVariable(name, value);
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
