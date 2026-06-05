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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the <c>logLevel</c> config field: default, serialize / round-trip through
    /// <see cref="GodotMcpConfigStore"/>, the <see cref="GodotMcpConfig.EnvLogLevel"/> override precedence
    /// (env &gt; persisted &gt; default), and the <c>.env</c>-file layer applied by
    /// <see cref="GodotMcpEnvFile.Apply"/> (file &gt; persisted, with env still winning live). Mirrors the
    /// shape of <see cref="GodotMcpAuthOptionTests"/>.
    ///
    /// <para>Env-mutating tests use <c>[Collection("env")]</c> + a restoring scope to avoid leakage.</para>
    /// </summary>
    [Collection("env")]
    public class GodotMcpLogLevelConfigTests
    {
        // --- Default ---

        [Fact]
        public void DefaultLogLevel_IsInfo()
        {
            using var _ = LogEnvScope.ClearAll();
            var config = new GodotMcpConfig();
            Assert.Equal(GodotMcpLogLevel.Info, config.LogLevel);
            Assert.Equal(GodotMcpLogLevel.Info, config.ActiveLogLevel);
        }

        // --- Env override precedence ---

        [Theory]
        [InlineData("Trace", GodotMcpLogLevel.Trace)]
        [InlineData("trace", GodotMcpLogLevel.Trace)]
        [InlineData("WARNING", GodotMcpLogLevel.Warning)]
        [InlineData("None", GodotMcpLogLevel.None)]
        public void EnvLogLevel_Overrides_ConfiguredValue(string envValue, GodotMcpLogLevel expected)
        {
            using var _ = LogEnvScope.Set(GodotMcpConfig.EnvLogLevel, envValue);
            // Configured = Info; env should win.
            var config = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Info };
            Assert.Equal(expected, config.ActiveLogLevel);
            // The persisted field is NOT mutated by the live override.
            Assert.Equal(GodotMcpLogLevel.Info, config.LogLevel);
        }

        [Theory]
        [InlineData("not-a-level")]
        [InlineData("2")]   // numeric strings are rejected (parity with mode/auth resolvers)
        [InlineData("")]
        public void EnvLogLevel_Unrecognized_FallsBackToConfigured(string envValue)
        {
            using var _ = LogEnvScope.Set(GodotMcpConfig.EnvLogLevel, envValue);
            var config = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Error };
            Assert.Equal(GodotMcpLogLevel.Error, config.ActiveLogLevel);
        }

        // --- Serialization round-trip via the store ---

        [Fact]
        public void LogLevel_Serializes_AsNamedValue()
        {
            var config = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Warning };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            Assert.Contains("\"logLevel\"", json);
            Assert.Contains("Warning", json);
        }

        [Fact]
        public void LogLevel_RoundTrips_ThroughStore_AndApplyPersisted()
        {
            using var _ = LogEnvScope.ClearAll();
            var path = Path.Combine(Path.GetTempPath(), $"godot-mcp-loglevel-{Guid.NewGuid():N}.json");
            try
            {
                var saved = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Debug };
                GodotMcpConfigStore.Save(path, saved);

                var loaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(loaded);
                Assert.Equal(GodotMcpLogLevel.Debug, loaded!.LogLevel);

                // ApplyPersisted seeds the level onto a fresh target (the boot precedence path).
                var target = new GodotMcpConfig();
                GodotMcpConfigStore.ApplyPersisted(target, loaded);
                Assert.Equal(GodotMcpLogLevel.Debug, target.LogLevel);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void OlderConfig_WithoutLogLevelKey_DefaultsToInfo()
        {
            using var _ = LogEnvScope.ClearAll();
            var path = Path.Combine(Path.GetTempPath(), $"godot-mcp-loglevel-old-{Guid.NewGuid():N}.json");
            try
            {
                // A config file written before logLevel existed (no key present).
                File.WriteAllText(path, "{ \"connectionMode\": \"Custom\" }");
                var loaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(loaded);
                Assert.Equal(GodotMcpLogLevel.Info, loaded!.LogLevel); // property initializer default
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        // --- .env file layer (file > persisted; env still wins live) ---

        [Fact]
        public void EnvFile_LogLevel_WritesPersistedField()
        {
            using var _ = LogEnvScope.ClearAll();
            var config = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Info };
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_LOG_LEVEL=Trace" });
            GodotMcpEnvFile.Apply(config, values);

            Assert.Equal(GodotMcpLogLevel.Trace, config.LogLevel);
            Assert.Equal(GodotMcpLogLevel.Trace, config.ActiveLogLevel); // no env override, so file value is live
        }

        [Fact]
        public void EnvFile_LogLevel_Numeric_Ignored()
        {
            using var _ = LogEnvScope.ClearAll();
            var config = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Warning };
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_LOG_LEVEL=0" });
            GodotMcpEnvFile.Apply(config, values);

            // Numeric string rejected → persisted value unchanged.
            Assert.Equal(GodotMcpLogLevel.Warning, config.LogLevel);
        }

        [Fact]
        public void ProcessEnv_Wins_Over_EnvFileLogLevel()
        {
            using var _ = LogEnvScope.Set(GodotMcpConfig.EnvLogLevel, "Error");
            var config = new GodotMcpConfig { LogLevel = GodotMcpLogLevel.Info };
            // .env says Trace, but the process env says Error and is read live by ActiveLogLevel.
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_LOG_LEVEL=Trace" });
            GodotMcpEnvFile.Apply(config, values);

            Assert.Equal(GodotMcpLogLevel.Trace, config.LogLevel);          // .env wrote the field…
            Assert.Equal(GodotMcpLogLevel.Error, config.ActiveLogLevel);    // …but the env override wins live.
        }

        /// <summary>Restoring env scope covering the log-level key.</summary>
        sealed class LogEnvScope : IDisposable
        {
            static readonly string[] Names = { GodotMcpConfig.EnvLogLevel };
            readonly Dictionary<string, string?> _prior = new();

            LogEnvScope()
            {
                foreach (var name in Names)
                    _prior[name] = Environment.GetEnvironmentVariable(name);
            }

            public static LogEnvScope ClearAll()
            {
                var scope = new LogEnvScope();
                foreach (var name in Names)
                    Environment.SetEnvironmentVariable(name, null);
                return scope;
            }

            public static LogEnvScope Set(string name, string? value)
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
