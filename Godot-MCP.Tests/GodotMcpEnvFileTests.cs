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
    /// Pins the pure-managed project-root <c>.env</c> layer (<see cref="GodotMcpEnvFile"/>): line
    /// parsing (comment/blank/quoted/whitespace/unknown-key), the env &gt; file &gt; config &gt; default
    /// precedence for each key, mode-aware token routing, loopback host → Custom auto-mode, explicit
    /// mode overriding loopback, and the missing-file no-op. No Godot native types or filesystem-bound
    /// parser surface — CI-friendly.
    ///
    /// <para>
    /// Tests that exercise the loopback / env-precedence path mutate process env vars, so the class
    /// shares the <c>env</c> collection with <see cref="GodotMcpConfigTests"/> (no cross-class parallel
    /// env races) and restores prior values via <see cref="EnvFileScope"/>.
    /// </para>
    /// </summary>
    [Collection("env")]
    public class GodotMcpEnvFileTests
    {
        // --- Parse: line handling ---

        [Fact]
        public void Parse_KeyValue_IsRecognized()
        {
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_HOST=http://localhost:5300" });
            Assert.Equal("http://localhost:5300", values[GodotMcpConfig.EnvHost]);
        }

        [Fact]
        public void Parse_SkipsBlankAndCommentLines()
        {
            var values = GodotMcpEnvFile.Parse(new[]
            {
                "",
                "   ",
                "# a comment",
                "   # indented comment",
                "GODOT_MCP_TOKEN=tok"
            });
            Assert.Single(values);
            Assert.Equal("tok", values[GodotMcpConfig.EnvToken]);
        }

        [Theory]
        [InlineData("GODOT_MCP_TOKEN=\"quoted\"", "quoted")]
        [InlineData("GODOT_MCP_TOKEN='quoted'", "quoted")]
        [InlineData("GODOT_MCP_TOKEN=  spaced  ", "spaced")]
        [InlineData("  GODOT_MCP_TOKEN  =  trim-key  ", "trim-key")]
        public void Parse_SanitizesWhitespaceAndQuotes(string line, string expected)
        {
            var values = GodotMcpEnvFile.Parse(new[] { line });
            Assert.Equal(expected, values[GodotMcpConfig.EnvToken]);
        }

        [Fact]
        public void Parse_IgnoresUnknownKeys()
        {
            var values = GodotMcpEnvFile.Parse(new[]
            {
                "PATH=/usr/bin",
                "GODOT_MCP_UNKNOWN=x",
                "SOME_OTHER=y",
                "GODOT_MCP_HOST=http://localhost:8080"
            });
            Assert.Single(values);
            Assert.Equal("http://localhost:8080", values[GodotMcpConfig.EnvHost]);
        }

        [Fact]
        public void Parse_LineWithoutEquals_IsSkipped()
        {
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_HOST", "novalue" });
            Assert.Empty(values);
        }

        [Fact]
        public void Parse_BlankValue_IsSkipped()
        {
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_TOKEN=", "GODOT_MCP_HOST=   " });
            Assert.Empty(values);
        }

        [Fact]
        public void Parse_ValueWithEquals_KeepsRemainder()
        {
            // Only the FIRST '=' splits key/value (tokens may contain '=').
            var values = GodotMcpEnvFile.Parse(new[] { "GODOT_MCP_TOKEN=a=b=c" });
            Assert.Equal("a=b=c", values[GodotMcpConfig.EnvToken]);
        }

        [Fact]
        public void Parse_DuplicateKey_LastWins()
        {
            var values = GodotMcpEnvFile.Parse(new[]
            {
                "GODOT_MCP_TOKEN=first",
                "GODOT_MCP_TOKEN=second"
            });
            Assert.Equal("second", values[GodotMcpConfig.EnvToken]);
        }

        [Fact]
        public void Parse_AllFourRecognizedKeys()
        {
            var values = GodotMcpEnvFile.Parse(new[]
            {
                "GODOT_MCP_CONNECTION_MODE=Custom",
                "GODOT_MCP_HOST=http://localhost:5300",
                "GODOT_MCP_CLOUD_URL=https://staging.ai-game.dev",
                "GODOT_MCP_TOKEN=tok"
            });
            Assert.Equal(4, values.Count);
            Assert.Equal("Custom", values[GodotMcpConfig.EnvConnectionMode]);
            Assert.Equal("http://localhost:5300", values[GodotMcpConfig.EnvHost]);
            Assert.Equal("https://staging.ai-game.dev", values[GodotMcpConfig.EnvCloudUrl]);
            Assert.Equal("tok", values[GodotMcpConfig.EnvToken]);
        }

        // --- LoadFile: missing-file no-op ---

        [Fact]
        public void LoadFile_MissingPath_ReturnsEmpty()
        {
            var path = Path.Combine(Path.GetTempPath(), "godot-mcp-no-such-" + Guid.NewGuid().ToString("N") + ".env");
            Assert.Empty(GodotMcpEnvFile.LoadFile(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void LoadFile_NullOrBlankPath_ReturnsEmpty(string? path)
        {
            Assert.Empty(GodotMcpEnvFile.LoadFile(path));
        }

        [Fact]
        public void LoadFile_RealFile_ParsesContents()
        {
            var path = Path.Combine(Path.GetTempPath(), "godot-mcp-" + Guid.NewGuid().ToString("N") + ".env");
            File.WriteAllText(path,
                "# project-root .env\nGODOT_MCP_HOST=http://localhost:5300\nGODOT_MCP_TOKEN=\"file-tok\"\n");
            try
            {
                var values = GodotMcpEnvFile.LoadFile(path);
                Assert.Equal("http://localhost:5300", values[GodotMcpConfig.EnvHost]);
                Assert.Equal("file-tok", values[GodotMcpConfig.EnvToken]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // --- Apply: missing/empty values no-op ---

        [Fact]
        public void Apply_EmptyValues_IsNoOp()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud, CloudToken = "orig" };
            GodotMcpEnvFile.Apply(config, new Dictionary<string, string>());
            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ConnectionMode);
            Assert.Equal("orig", config.CloudToken);
        }

        [Fact]
        public void Apply_NullValues_IsNoOp()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            var same = GodotMcpEnvFile.Apply(config, null);
            Assert.Same(config, same);
        }

        // --- Precedence: file value beats configured field / default ---

        [Fact]
        public void Apply_FileHost_OverridesConfiguredCustomHost()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:1111"
            };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvHost, "http://localhost:5300"));
            Assert.Equal("http://localhost:5300", config.Host);
        }

        [Fact]
        public void Apply_FileToken_OverridesConfiguredField_Cloud()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud, CloudToken = "old" };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvToken, "file-tok"));
            Assert.Equal("file-tok", config.Token);
            Assert.Equal("file-tok", config.CloudToken);
        }

        // --- Precedence: process env beats file ---

        [Fact]
        public void EnvHost_Beats_FileHost()
        {
            using var _ = EnvFileScope.Set(GodotMcpConfig.EnvHost, "http://localhost:9999");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvHost, "http://localhost:5300"));
            // Env wins live in the getter even though the file value was written to the field.
            Assert.Equal("http://localhost:9999", config.Host);
        }

        [Fact]
        public void EnvToken_Beats_FileToken()
        {
            using var _ = EnvFileScope.Set(GodotMcpConfig.EnvToken, "env-tok");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvToken, "file-tok"));
            Assert.Equal("env-tok", config.Token);
        }

        [Fact]
        public void EnvMode_Beats_FileMode()
        {
            // File says Cloud, env says Custom → env wins (ActiveMode reads env live).
            using var _ = EnvFileScope.Set(GodotMcpConfig.EnvConnectionMode, "Custom");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvConnectionMode, "Cloud"));
            Assert.Equal(GodotMcpConnectionMode.Custom, config.ActiveMode);
        }

        // --- Auth option: file layer + env precedence ---

        [Fact]
        public void Apply_FileAuthOption_WritesConfiguredField()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvAuthOption, "Required"));
            Assert.Equal(GodotMcpAuthOption.Required, config.ActiveAuthOption);
        }

        [Fact]
        public void EnvAuthOption_Beats_FileAuthOption()
        {
            // File says None, env says Required → env wins (ActiveAuthOption reads env live).
            using var _ = EnvFileScope.Set(GodotMcpConfig.EnvAuthOption, "Required");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvAuthOption, "None"));
            Assert.Equal(GodotMcpAuthOption.Required, config.ActiveAuthOption);
        }

        [Fact]
        public void Apply_FileAuthOption_Numeric_IsIgnored()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvAuthOption, "1"));
            // Numeric rejected → configured default (None) stands.
            Assert.Equal(GodotMcpAuthOption.None, config.ActiveAuthOption);
        }

        // --- Default wins when neither env nor file present ---

        [Fact]
        public void Default_WhenNoEnvNoFile_ConfigValuesIntact()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig();
            GodotMcpEnvFile.Apply(config, new Dictionary<string, string>());
            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ActiveMode);
            Assert.Equal("https://ai-game.dev/mcp", config.Host);
        }

        // --- Token mode-routing ---

        [Fact]
        public void Apply_FileToken_RoutesToCustomToken_InCustomMode()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvToken, "ctok"));
            Assert.Equal("ctok", config.CustomToken);
            Assert.Null(config.CloudToken);
        }

        [Fact]
        public void Apply_FileToken_RoutesToCloudToken_InCloudMode()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvToken, "cltok"));
            Assert.Equal("cltok", config.CloudToken);
            Assert.Null(config.CustomToken);
        }

        [Fact]
        public void Apply_FileToken_RoutesByFileMode_WhenFileSetsCustom()
        {
            // Config defaults Cloud, but the file flips mode to Custom and supplies a token in the same
            // apply: the token must route to CustomToken (mode is settled before the token routes).
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GodotMcpConfig.EnvConnectionMode] = "Custom",
                [GodotMcpConfig.EnvToken] = "ctok"
            });
            Assert.Equal("ctok", config.CustomToken);
            Assert.Null(config.CloudToken);
        }

        // --- Loopback auto-mode ---

        [Theory]
        [InlineData("http://localhost:5300")]
        [InlineData("http://127.0.0.1:5300")]
        [InlineData("http://[::1]:5300")]
        public void Apply_LoopbackFileHost_AutoSelectsCustomMode(string host)
        {
            using var _ = EnvFileScope.ClearAll();
            // Config defaults to Cloud; a loopback host in the file with no explicit mode infers Custom.
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvHost, host));
            Assert.Equal(GodotMcpConnectionMode.Custom, config.ActiveMode);
            Assert.Equal(host, config.Host);
        }

        [Fact]
        public void Apply_RemoteFileHost_DoesNotChangeMode()
        {
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvHost, "https://remote.example.com"));
            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ActiveMode);
        }

        [Fact]
        public void Apply_ExplicitFileMode_OverridesLoopbackInference()
        {
            using var _ = EnvFileScope.ClearAll();
            // Loopback host WOULD infer Custom, but an explicit file mode=Cloud wins.
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GodotMcpConfig.EnvHost] = "http://localhost:5300",
                [GodotMcpConfig.EnvConnectionMode] = "Cloud"
            });
            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ActiveMode);
        }

        [Fact]
        public void Apply_LoopbackEnvHost_AutoSelectsCustom_EvenWithoutFileHost()
        {
            // Env host is loopback (no file host). The loopback inference reads the EFFECTIVE host,
            // which prefers env, so Custom is inferred.
            using var _ = EnvFileScope.Set(GodotMcpConfig.EnvHost, "http://127.0.0.1:5300");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvToken, "tok"));
            Assert.Equal(GodotMcpConnectionMode.Custom, config.ActiveMode);
        }

        [Fact]
        public void Apply_FileMode_NumericString_DoesNotChangeMode()
        {
            // Numeric strings are rejected (parity with ResolveActiveMode); the loopback fallback also
            // does not fire here (no loopback host), so the configured mode stands.
            using var _ = EnvFileScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            GodotMcpEnvFile.Apply(config, Map(GodotMcpConfig.EnvConnectionMode, "1"));
            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ActiveMode);
        }

        // --- helpers ---

        static Dictionary<string, string> Map(string key, string value) =>
            new(StringComparer.Ordinal) { [key] = value };

        /// <summary>
        /// Sets/clears the four <c>GODOT_MCP_*</c> process env vars and restores prior values on dispose.
        /// Mirrors <see cref="GodotMcpConfigTests"/>'s env isolation so the loopback/precedence tests
        /// (which read env live) stay deterministic.
        /// </summary>
        sealed class EnvFileScope : IDisposable
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

            EnvFileScope()
            {
                foreach (var name in Names)
                    _prior[name] = Environment.GetEnvironmentVariable(name);
            }

            public static EnvFileScope ClearAll()
            {
                var scope = new EnvFileScope();
                foreach (var name in Names)
                    Environment.SetEnvironmentVariable(name, null);
                return scope;
            }

            public static EnvFileScope Set(string name, string? value)
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
