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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed connection-config logic: Cloud/Custom mode selection, cloud URL
    /// resolution (default + env override + trailing-/mcp normalization), custom host override, and
    /// mode-aware token selection. These exercise the same code the editor plugin runs at boot, with
    /// no Godot native types or SignalR client in play — CI-friendly.
    ///
    /// <para>
    /// All tests here mutate process environment variables, so the class is marked
    /// <c>[Collection("env")]</c> to disable cross-class parallelism against the same env, and every
    /// test restores the prior values via <see cref="EnvScope"/>.
    /// </para>
    /// </summary>
    [Collection("env")]
    public class GodotMcpConfigTests
    {
        // --- Mode resolution ---

        [Fact]
        public void DefaultMode_IsCloud()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig();
            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ActiveMode);
        }

        [Theory]
        [InlineData("Custom", GodotMcpConnectionMode.Custom)]
        [InlineData("custom", GodotMcpConnectionMode.Custom)]
        [InlineData("CLOUD", GodotMcpConnectionMode.Cloud)]
        public void EnvConnectionMode_Overrides_ConfiguredMode(string envValue, GodotMcpConnectionMode expected)
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvConnectionMode, envValue);
            // Configured value is Cloud; env should win (and round-trip for the Cloud case too).
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            Assert.Equal(expected, config.ActiveMode);
        }

        [Fact]
        public void EnvConnectionMode_Unrecognized_FallsBackToConfigured()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvConnectionMode, "not-a-mode");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            Assert.Equal(GodotMcpConnectionMode.Custom, config.ActiveMode);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        public void EnvConnectionMode_NumericString_FallsBackToConfigured(string envValue)
        {
            // Enum.TryParse would accept numeric strings; we deliberately reject them so only
            // named values (Cloud/Custom) override the configured mode.
            using var _ = EnvScope.Set(GodotMcpConfig.EnvConnectionMode, envValue);
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            Assert.Equal(GodotMcpConnectionMode.Custom, config.ActiveMode);
        }

        // --- Cloud URL resolution ---

        [Fact]
        public void CloudUrl_Default_AppendsMcpHubPath()
        {
            using var _ = EnvScope.ClearAll();
            Assert.Equal("https://ai-game.dev/mcp", GodotMcpConfig.ResolveCloudUrl());
        }

        [Fact]
        public void CloudUrl_EnvOverride_IsHonored()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvCloudUrl, "https://staging.ai-game.dev");
            Assert.Equal("https://staging.ai-game.dev/mcp", GodotMcpConfig.ResolveCloudUrl());
        }

        [Fact]
        public void CloudUrl_EnvOverride_WithTrailingMcp_DoesNotDouble()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvCloudUrl, "https://staging.ai-game.dev/mcp");
            Assert.Equal("https://staging.ai-game.dev/mcp", GodotMcpConfig.ResolveCloudUrl());
        }

        [Fact]
        public void CloudUrl_EnvOverride_WithTrailingSlash_IsTrimmed()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvCloudUrl, "https://staging.ai-game.dev/");
            Assert.Equal("https://staging.ai-game.dev/mcp", GodotMcpConfig.ResolveCloudUrl());
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("ftp://ai-game.dev")]
        [InlineData("   ")]
        public void CloudUrl_InvalidEnvOverride_FallsBackToDefault(string envValue)
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvCloudUrl, envValue);
            Assert.Equal("https://ai-game.dev/mcp", GodotMcpConfig.ResolveCloudUrl());
        }

        [Fact]
        public void Host_InCloudMode_ReturnsCloudUrl()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            Assert.Equal("https://ai-game.dev/mcp", config.Host);
        }

        // --- Custom host resolution ---

        [Fact]
        public void Host_InCustomMode_ReturnsCustomHost()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:5300"
            };
            Assert.Equal("http://localhost:5300", config.Host);
        }

        [Fact]
        public void Host_InCustomMode_EnvHostOverrides()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvHost, "http://localhost:9999");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:5300"
            };
            Assert.Equal("http://localhost:9999", config.Host);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Host_InCustomMode_NullOrBlankCustomHost_FallsBackToDefault(string? customHost)
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = customHost!
            };
            Assert.Equal(GodotMcpConfig.DefaultCustomHost, config.Host);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Host_InCustomMode_EnvHostBlank_FallsBackToConfigured(string envHost)
        {
            // A blank EnvHost must not shadow a valid configured CustomHost.
            using var _ = EnvScope.Set(GodotMcpConfig.EnvHost, envHost);
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:5300"
            };
            Assert.Equal("http://localhost:5300", config.Host);
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("ftp://localhost")]
        public void Host_InCustomMode_InvalidEnvHost_FallsBackToDefault(string envHost)
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvHost, envHost);
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:5300"
            };
            Assert.Equal(GodotMcpConfig.DefaultCustomHost, config.Host);
        }

        [Fact]
        public void Host_Setter_WritesCustomHost()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            config.Host = "http://localhost:1234";
            Assert.Equal("http://localhost:1234", config.CustomHost);
            Assert.Equal("http://localhost:1234", config.Host);
        }

        // --- Token selection ---

        [Fact]
        public void Token_InCloudMode_ReturnsCloudToken()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Cloud,
                CloudToken = "cloud-tok",
                CustomToken = "custom-tok"
            };
            Assert.Equal("cloud-tok", config.Token);
        }

        [Fact]
        public void Token_InCustomMode_ReturnsCustomToken()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                // Custom-mode token only flows to the bearer when auth is token (the auth-option gate).
                AuthOption = McpServerConsts.AuthOption.token,
                CloudToken = "cloud-tok",
                CustomToken = "custom-tok"
            };
            Assert.Equal("custom-tok", config.Token);
        }

        [Fact]
        public void Token_EnvOverride_WinsInCloudMode()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvToken, "env-tok");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Cloud,
                CloudToken = "cloud-tok"
            };
            Assert.Equal("env-tok", config.Token);
        }

        [Fact]
        public void Token_EnvOverride_WinsInCustomMode()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvToken, "env-tok");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                // Token routing in Custom mode is gated on auth being token (see AuthOption tests).
                AuthOption = McpServerConsts.AuthOption.token,
                CustomToken = "custom-tok"
            };
            Assert.Equal("env-tok", config.Token);
        }

        [Fact]
        public void Token_EnvOverride_StripsWrappingQuotes()
        {
            // GODOT_MCP_TOKEN="abc" (shell-quoted) must not send a literal-quote bearer value.
            using var _ = EnvScope.Set(GodotMcpConfig.EnvToken, "\"env-tok\"");
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            Assert.Equal("env-tok", config.Token);
        }

        [Fact]
        public void Token_Setter_RoutesByActiveMode()
        {
            using var _ = EnvScope.ClearAll();

            var cloud = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            cloud.Token = "x";
            Assert.Equal("x", cloud.CloudToken);
            Assert.Null(cloud.CustomToken);

            var custom = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Custom };
            custom.Token = "y";
            Assert.Equal("y", custom.CustomToken);
            Assert.Null(custom.CloudToken);
        }

        // --- Defaults / serialization shape ---

        [Fact]
        public void Defaults_KeepConnected_True()
        {
            using var _ = EnvScope.ClearAll();
            // KeepConnected drives the reused client's auto-reconnect/backoff; must default on.
            Assert.True(new GodotMcpConfig().KeepConnected);
        }

        [Fact]
        public void Serialization_UsesStableJsonNames()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:5300",
                CustomToken = "ctok",
                CloudToken = "cloudtok"
            };

            var json = JsonSerializer.Serialize(config);

            Assert.Contains("\"host\"", json);
            Assert.Contains("\"token\"", json);
            Assert.Contains("\"cloudToken\"", json);
            Assert.Contains("\"connectionMode\"", json);
        }

        // --- MCP-client URL resolution (the URL an external AI client POSTs to — distinct from Host) ---

        [Fact]
        public void McpClientUrl_Cloud_IsTheCloudMcpUrl()
        {
            using var _ = EnvScope.ClearAll();
            // Cloud Host already ends in /mcp (via ResolveCloudUrl); the MCP-client URL equals it, no double-suffix.
            var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
            Assert.Equal("https://ai-game.dev/mcp", GodotMcpConfig.ResolveMcpClientUrl(config));
        }

        [Fact]
        public void McpClientUrl_Custom_AppendsMcpToHost()
        {
            using var _ = EnvScope.ClearAll();
            // The plugin connects to <host>/hub/mcp-server; the AI client connects to <host>/mcp.
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:8080"
            };
            Assert.Equal("http://localhost:8080/mcp", GodotMcpConfig.ResolveMcpClientUrl(config));
        }

        [Fact]
        public void McpClientUrl_Custom_TrailingSlashHost_IsNormalized()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:8080/"
            };
            Assert.Equal("http://localhost:8080/mcp", GodotMcpConfig.ResolveMcpClientUrl(config));
        }

        [Fact]
        public void McpClientUrl_Custom_HostAlreadyEndingInMcp_IsNotDoubleSuffixed()
        {
            using var _ = EnvScope.ClearAll();
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:8080/mcp"
            };
            Assert.Equal("http://localhost:8080/mcp", GodotMcpConfig.ResolveMcpClientUrl(config));
        }

        [Fact]
        public void McpClientUrl_Custom_HonorsEnvHostOverride()
        {
            using var _ = EnvScope.Set(GodotMcpConfig.EnvHost, "http://10.0.0.5:9000");
            var config = new GodotMcpConfig
            {
                ConnectionMode = GodotMcpConnectionMode.Custom,
                CustomHost = "http://localhost:8080"
            };
            Assert.Equal("http://10.0.0.5:9000/mcp", GodotMcpConfig.ResolveMcpClientUrl(config));
        }

        /// <summary>
        /// Sets/clears process env vars relevant to <see cref="GodotMcpConfig"/> and restores the prior
        /// values on dispose. Keeps env-driven tests isolated and re-entry-safe.
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

    /// <summary>
    /// Pins the <c>ping</c> tool's echo/pong contract — the single tool that proves the MCP path.
    /// Pure-managed; no server needed.
    /// </summary>
    public class PingToolTests
    {
        [Fact]
        public void Ping_WithNoMessage_ReturnsPong()
        {
            var tool = new com.IvanMurzak.Godot.MCP.Tools.Tool_Ping();
            Assert.Equal("pong", tool.Ping());
        }

        [Fact]
        public void Ping_WithEmptyMessage_ReturnsPong()
        {
            var tool = new com.IvanMurzak.Godot.MCP.Tools.Tool_Ping();
            Assert.Equal("pong", tool.Ping(""));
        }

        [Fact]
        public void Ping_WithMessage_EchoesIt()
        {
            var tool = new com.IvanMurzak.Godot.MCP.Tools.Tool_Ping();
            Assert.Equal("hello", tool.Ping("hello"));
        }
    }
}
