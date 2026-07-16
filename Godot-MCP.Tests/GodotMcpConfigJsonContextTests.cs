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
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Regression tests for the SOURCE-GENERATED persisted-config serialization (godotengine/godot#78513).
    /// <see cref="GodotMcpConfigStore"/> must (de)serialize <see cref="GodotMcpConfig"/> through
    /// <see cref="GodotMcpConfigJsonContext"/> — NOT the default reflection path, whose process-wide
    /// reflection-emit accessor cache pins this collectible addon type across a Godot hot-reload. These tests
    /// pin (a) that the source generator actually produced compiled metadata for the whole config graph, and
    /// (b) a full-fidelity round-trip (enums-as-string + nested feature map) so a regression to reflection or
    /// a broken context is caught in CI.
    /// </summary>
    public class GodotMcpConfigJsonContextTests
    {
        [Fact]
        public void Context_ProvidesCompiledMetadata_ForTheWholeConfigGraph()
        {
            // A source-generated context exposes a strongly-typed, NON-null JsonTypeInfo whose Kind is Object
            // for a POCO — proof the generator ran and these types never need the reflection-emit accessor cache.
            JsonTypeInfo<GodotMcpConfig> info = GodotMcpConfigJsonContext.Default.GodotMcpConfig;
            Assert.NotNull(info);
            Assert.Equal(JsonTypeInfoKind.Object, info.Kind);
            // The generator wires the context as its own resolver.
            Assert.NotNull(GodotMcpConfigJsonContext.Default.Options.TypeInfoResolver);
        }

        [Fact]
        public void RoundTrip_ThroughContext_PreservesEnumsAndNestedFeatureMap()
        {
            var original = new GodotMcpConfig
            {
                CustomHost = "http://localhost:9999",
                CustomToken = "tok",
                CloudToken = "cloud",
                ConnectionMode = GodotMcpConnectionMode.Custom,   // enum-as-string
                AuthOption = McpServerConsts.AuthOption.token,
                LogLevel = GodotMcpLogLevel.Trace,
                SelectedAgentId = "cursor",
            };
            original.Features.Tools.Add(new GodotMcpFeatureState { Name = "ping", Enabled = false });

            var json = JsonSerializer.Serialize(original, GodotMcpConfigJsonContext.Default.GodotMcpConfig);

            // Enums serialize by NAME (UseStringEnumConverter), matching the .env/process-env parse layers.
            Assert.Contains("\"Custom\"", json);
            Assert.Contains("\"token\"", json);

            var back = JsonSerializer.Deserialize(json, GodotMcpConfigJsonContext.Default.GodotMcpConfig);
            Assert.NotNull(back);
            Assert.Equal(GodotMcpConnectionMode.Custom, back!.ConnectionMode);
            Assert.Equal(McpServerConsts.AuthOption.token, back.AuthOption);
            Assert.Equal(GodotMcpLogLevel.Trace, back.LogLevel);
            Assert.Equal("http://localhost:9999", back.CustomHost);
            Assert.Equal("cursor", back.SelectedAgentId);
            Assert.Single(back.Features.Tools);
            Assert.Equal("ping", back.Features.Tools[0].Name);
            Assert.False(back.Features.Tools[0].Enabled);
        }
    }
}
