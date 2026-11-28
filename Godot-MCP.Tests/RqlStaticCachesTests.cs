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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using com.IvanMurzak.McpPlugin;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public sealed class RqlCacheProbe
    {
        public string label { get; set; } = "x";
        public int size { get; set; } = 3;
    }

    /// <summary>
    /// <see cref="RqlStaticCaches"/>: the hot-reload release of RQL's process-wide Type-keyed caches finds and clears them
    /// (the reflection targets still exist in the pinned RQL version), so an addon type a query touched is no longer
    /// referenced by them.
    /// </summary>
    [Collection(RqlToolBindingCollection.Name)]
    public class RqlStaticCachesTests
    {
        [Fact]
        public void ClearReleasesTheSharedModelsTypeCache()
        {
            var engine = new RqlEngine(); // no adapters: the shared, process-wide model
            engine.AddSource("probes", () => new List<RqlCacheProbe> { new RqlCacheProbe() });
            Assert.True(engine.Execute("probes | select label size").Ok);
            Assert.True(SharedCacheHolds(typeof(RqlCacheProbe)));

            var cleared = RqlStaticCaches.Clear();

            Assert.True(cleared > 0, "no RQL cache was found to clear — the RQL internals changed; update RqlStaticCaches");
            Assert.False(SharedCacheHolds(typeof(RqlCacheProbe)));
            // The engine keeps working after the release (caches refill on demand).
            Assert.True(engine.Execute("probes | select label size").Ok);
        }

        private static bool SharedCacheHolds(Type type)
        {
            var field = typeof(TypeModel).GetField("_cache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var cache = (System.Collections.IDictionary)field.GetValue(TypeModel.Shared)!;
            return cache.Contains(type);
        }
    }

    /// <summary>RQL is the only tool data layer: the escaping is persisted, an old config's toolFormat key is ignored.</summary>
    public class RqlToolDataLayerConfigTests
    {
        [Fact]
        public void MinimalEscapingIsTheDefault() => Assert.Equal(ToolTextEscaping.Minimal, new GodotMcpConfig().ToolTextEscaping);

        [Fact]
        public void TheEscapingIsPersisted()
        {
            var saved = new GodotMcpConfig { ToolTextEscaping = ToolTextEscaping.Ascii };
            var json = System.Text.Json.JsonSerializer.Serialize(saved, GodotMcpConfigJsonContext.Default.GodotMcpConfig);
            Assert.DoesNotContain("toolFormat", json);
            var loaded = System.Text.Json.JsonSerializer.Deserialize(json, GodotMcpConfigJsonContext.Default.GodotMcpConfig)!;
            Assert.Equal(ToolTextEscaping.Ascii, GodotMcpConfigStore.ApplyPersisted(new GodotMcpConfig(), loaded).ToolTextEscaping);
        }

        [Theory]
        [InlineData("Json")]
        [InlineData("Rql")]
        public void AnOldConfigWithAToolFormatStillLoads(string format)
        {
            var json = "{\"toolFormat\": \"" + format + "\", \"toolTextEscaping\": \"Ascii\"}";
            var loaded = System.Text.Json.JsonSerializer.Deserialize(json, GodotMcpConfigJsonContext.Default.GodotMcpConfig)!;
            Assert.Equal(ToolTextEscaping.Ascii, GodotMcpConfigStore.ApplyPersisted(new GodotMcpConfig(), loaded).ToolTextEscaping);
        }
    }
}
