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
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed config persistence layer (<see cref="GodotMcpConfigStore"/>): the
    /// Save→Load round-trip of the serialized fields (mode/host/token/cloudToken), the missing/corrupt
    /// file → <c>null</c> (no-throw) contract, and the documented precedence — a persisted value is used
    /// when no env/.env override is present, but an env or <c>.env</c> override still WINS over the
    /// persisted value (process env &gt; .env &gt; persisted config &gt; default).
    ///
    /// <para>
    /// The precedence tests mutate process env vars + apply the <c>.env</c> layer, so this class joins the
    /// shared <c>env</c> collection (no cross-class parallel env races) and restores prior env via
    /// <see cref="EnvScope"/>. Filesystem fixtures use a unique temp dir per test, cleaned on dispose.
    /// </para>
    /// </summary>
    [Collection("env")]
    public class GodotMcpConfigStoreTests : IDisposable
    {
        readonly string _dir;

        public GodotMcpConfigStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "godot-mcp-store-" + Guid.NewGuid().ToString("N"));
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

        // --- Round-trip ---

        [Fact]
        public void SaveLoad_RoundTripsAllSerializedFields()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("config.json");
            var original = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:5300",
                CustomToken = "custom-tok",
                CloudToken = "cloud-tok"
            };

            GodotMcpConfigStore.Save(path, original);
            var loaded = GodotMcpConfigStore.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal(GodotMcpConnectionMode.Custom, loaded!.ConnectionMode);
            Assert.Equal("http://localhost:5300", loaded.CustomHost);
            Assert.Equal("custom-tok", loaded.CustomToken);
            Assert.Equal("cloud-tok", loaded.CloudToken);
        }

        [Fact]
        public void SaveLoad_RoundTripsSelectedAgentId_AndApplyPersistedCarriesIt()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("config-agent.json");
            var original = new GodotMcpConfig { SelectedAgentId = "cursor" };

            GodotMcpConfigStore.Save(path, original);

            var json = System.IO.File.ReadAllText(path);
            Assert.Contains("\"selectedAgentId\"", json);

            var loaded = GodotMcpConfigStore.Load(path);
            Assert.Equal("cursor", loaded!.SelectedAgentId);

            // ApplyPersisted carries the selected agent onto a fresh target (default would have been claude-code).
            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, loaded);
            Assert.Equal("cursor", target.SelectedAgentId);
        }

        [Fact]
        public void ApplyPersisted_MissingSelectedAgentId_KeepsTargetDefault()
        {
            using var _ = EnvScope.ClearAll();

            // An older config (no selectedAgentId key) deserializes to the property default; if a persisted value
            // were somehow blank, ApplyPersisted must keep the target's default rather than blanking it.
            var persisted = new GodotMcpConfig { SelectedAgentId = "" };
            var target = new GodotMcpConfig(); // default "claude-code"
            GodotMcpConfigStore.ApplyPersisted(target, persisted);
            Assert.Equal("claude-code", target.SelectedAgentId);
        }

        [Fact]
        public void Save_WritesEnumByName_NotNumber()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("by-name.json");
            GodotMcpConfigStore.Save(path, new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud });

            var json = File.ReadAllText(path);
            // Enum serialized by name so it matches how GODOT_MCP_CONNECTION_MODE is parsed by the env/.env layers.
            Assert.Contains("\"connectionMode\": \"Cloud\"", json);
        }

        [Fact]
        public void Save_CreatesMissingParentDirectory()
        {
            using var _ = EnvScope.ClearAll();

            var path = Path.Combine(_dir, "nested", "deep", "config.json");
            GodotMcpConfigStore.Save(path, new GodotMcpConfig { CustomHost = "http://localhost:9" });

            Assert.True(File.Exists(path));
            Assert.NotNull(GodotMcpConfigStore.Load(path));
        }

        // --- Missing / corrupt → null (no throw) ---

        [Fact]
        public void Load_MissingFile_ReturnsNull()
        {
            Assert.Null(GodotMcpConfigStore.Load(PathFor("does-not-exist.json")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Load_BlankPath_ReturnsNull(string? path)
        {
            Assert.Null(GodotMcpConfigStore.Load(path));
        }

        [Fact]
        public void Load_EmptyFile_ReturnsNull()
        {
            var path = PathFor("empty.json");
            File.WriteAllText(path, "");
            Assert.Null(GodotMcpConfigStore.Load(path));
        }

        [Fact]
        public void Load_CorruptJson_ReturnsNull_NoThrow()
        {
            var path = PathFor("corrupt.json");
            File.WriteAllText(path, "{ this is not valid json ::: ");
            // Must not throw — a corrupt config is the same as "no persisted config".
            var loaded = GodotMcpConfigStore.Load(path);
            Assert.Null(loaded);
        }

        // --- Precedence: persisted used when no override ---

        [Fact]
        public void Precedence_PersistedValueUsed_WhenNoEnvOrFileOverride()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("persisted.json");
            GodotMcpConfigStore.Save(path, new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                // Persisting Required so the Custom-mode bearer flows (auth-option gate); it round-trips too.
                AuthOption = GodotMcpAuthOption.Required,
                CustomHost = "http://persisted-host:1234",
                CustomToken = "persisted-tok"
            });

            // Boot order: seed from persisted, then apply an EMPTY .env layer (no override).
            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, GodotMcpConfigStore.Load(path));
            GodotMcpEnvFile.Apply(target, new Dictionary<string, string>());

            Assert.Equal(GodotMcpConnectionMode.Custom, target.ActiveMode);
            Assert.Equal(GodotMcpAuthOption.Required, target.ActiveAuthOption);
            Assert.Equal("http://persisted-host:1234", target.Host);
            Assert.Equal("persisted-tok", target.Token);
        }

        // --- Precedence: process env WINS over persisted ---

        [Fact]
        public void Precedence_ProcessEnv_WinsOverPersisted()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvHost, "http://env-host:9999");

            var path = PathFor("persisted.json");
            GodotMcpConfigStore.Save(path, new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://persisted-host:1234"
            });

            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, GodotMcpConfigStore.Load(path));

            // Live process-env getter shadows the persisted CustomHost field.
            Assert.Equal(GodotMcpConnectionMode.Custom, target.ActiveMode);
            Assert.Equal("http://env-host:9999", target.Host);
        }

        [Fact]
        public void Precedence_ProcessEnvToken_WinsOverPersisted()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvToken, "env-tok");

            var path = PathFor("persisted.json");
            GodotMcpConfigStore.Save(path, new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Cloud,
                CloudToken = "persisted-cloud-tok"
            });

            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, GodotMcpConfigStore.Load(path));

            Assert.Equal("env-tok", target.Token);
        }

        // --- Precedence: .env file WINS over persisted ---

        [Fact]
        public void Precedence_EnvFile_WinsOverPersisted()
        {
            using var _ = EnvScope.ClearAll();

            var path = PathFor("persisted.json");
            GodotMcpConfigStore.Save(path, new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://persisted-host:1234"
            });

            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, GodotMcpConfigStore.Load(path));
            // .env layer overrides the persisted host.
            GodotMcpEnvFile.Apply(target, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GodotMcpConfig.EnvHost] = "http://envfile-host:5555"
            });

            Assert.Equal(GodotMcpConnectionMode.Custom, target.ActiveMode);
            Assert.Equal("http://envfile-host:5555", target.Host);
        }

        /// <summary>
        /// Sets/clears the four <c>GODOT_MCP_*</c> process env vars and restores prior values on dispose.
        /// Mirrors the env isolation used by the config / env-file test classes so the precedence tests
        /// (which read env live) stay deterministic under the shared <c>env</c> collection.
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

            public static EnvScope Set(string name, string? value)
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
