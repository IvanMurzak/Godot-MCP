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

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the Custom-mode authorization option (none/required): serialize / round-trip through
    /// <see cref="GodotMcpConfigStore"/>, the env override precedence (env &gt; persisted &gt; default),
    /// and the <see cref="GodotMcpConfig.ResolveCustomToken"/> gating (no bearer when auth is None).
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
            Assert.Equal(GodotMcpAuthOption.None, config.ActiveAuthOption);
        }

        // --- Env override precedence ---

        [Theory]
        [InlineData("Required", GodotMcpAuthOption.Required)]
        [InlineData("required", GodotMcpAuthOption.Required)]
        [InlineData("NONE", GodotMcpAuthOption.None)]
        public void EnvAuthOption_Overrides_ConfiguredValue(string envValue, GodotMcpAuthOption expected)
        {
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, envValue);
            // Configured = None; env should win (including the round-trip None case).
            var config = new GodotMcpConfig { AuthOption = GodotMcpAuthOption.None };
            Assert.Equal(expected, config.ActiveAuthOption);
        }

        [Theory]
        [InlineData("not-an-option")]
        [InlineData("1")]
        [InlineData("")]
        public void EnvAuthOption_Unrecognized_FallsBackToConfigured(string envValue)
        {
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, envValue);
            var config = new GodotMcpConfig { AuthOption = GodotMcpAuthOption.Required };
            Assert.Equal(GodotMcpAuthOption.Required, config.ActiveAuthOption);
        }

        // --- ResolveCustomToken gating ---

        [Fact]
        public void ResolveCustomToken_None_ReturnsNull_EvenWithStoredToken()
        {
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = GodotMcpAuthOption.None,
                CustomToken = "stored-token-keeps-its-value"
            };
            // Anonymous connection: no bearer on the wire, but the stored value is NOT discarded.
            Assert.Null(config.ResolveCustomToken());
            Assert.Equal("stored-token-keeps-its-value", config.CustomToken);
        }

        [Fact]
        public void ResolveCustomToken_Required_ReturnsStoredToken()
        {
            using var _ = AuthEnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = GodotMcpAuthOption.Required,
                CustomToken = "abc123"
            };
            Assert.Equal("abc123", config.ResolveCustomToken());
        }

        [Fact]
        public void ResolveCustomToken_Required_EnvTokenWinsOverStored()
        {
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvToken, "env-token");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = GodotMcpAuthOption.Required,
                CustomToken = "stored"
            };
            Assert.Equal("env-token", config.ResolveCustomToken());
        }

        [Fact]
        public void ResolveCustomToken_EnvAuthRequired_OverridesPersistedNone()
        {
            // Persisted None, but env forces Required => the stored token now flows to the bearer.
            using var _ = AuthEnvScope.Set(GodotMcpConfig.EnvAuthOption, "Required");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                AuthOption = GodotMcpAuthOption.None,
                CustomToken = "stored"
            };
            Assert.Equal(GodotMcpAuthOption.Required, config.ActiveAuthOption);
            Assert.Equal("stored", config.ResolveCustomToken());
        }

        // --- Serialization round-trip via the store ---

        [Fact]
        public void AuthOption_Serializes_AsNamedValue()
        {
            var config = new GodotMcpConfig { AuthOption = GodotMcpAuthOption.Required };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            Assert.Contains("\"authOption\"", json);
            Assert.Contains("Required", json);
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
                    AuthOption = GodotMcpAuthOption.Required,
                    CustomToken = "round-trip-token"
                };
                GodotMcpConfigStore.Save(path, saved);

                var loaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(loaded);
                Assert.Equal(GodotMcpAuthOption.Required, loaded!.AuthOption);

                // ApplyPersisted seeds the auth option onto a fresh target (the boot precedence path).
                var target = new GodotMcpConfig();
                GodotMcpConfigStore.ApplyPersisted(target, loaded);
                Assert.Equal(GodotMcpAuthOption.Required, target.AuthOption);
                Assert.Equal("round-trip-token", target.CustomToken);
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
            var persisted = new GodotMcpConfig { AuthOption = GodotMcpAuthOption.None };
            var target = new GodotMcpConfig { AuthOption = GodotMcpAuthOption.Required };
            GodotMcpConfigStore.ApplyPersisted(target, persisted);
            Assert.Equal(GodotMcpAuthOption.None, target.AuthOption);
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
