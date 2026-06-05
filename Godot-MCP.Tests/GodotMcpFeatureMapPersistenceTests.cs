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
using System.IO;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the persistence of the MCP-feature enable-map (<see cref="GodotMcpFeatureMap"/>) through the
    /// pure-managed <see cref="GodotMcpConfigStore"/>: the <c>features</c> key Save→Load round-trip, the
    /// default-empty (fresh install disables nothing) baseline, the <see cref="GodotMcpConfigStore.ApplyPersisted"/>
    /// seeding of the boot path, and backwards-compatibility with an older config file that has NO <c>features</c>
    /// key. Filesystem fixtures use a unique temp dir per test, cleaned on dispose.
    /// </summary>
    public class GodotMcpFeatureMapPersistenceTests : IDisposable
    {
        readonly string _dir;

        public GodotMcpFeatureMapPersistenceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "godot-mcp-features-" + Guid.NewGuid().ToString("N"));
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

        [Fact]
        public void Fresh_config_has_an_empty_feature_map_disabling_nothing()
        {
            var config = new GodotMcpConfig();

            Assert.NotNull(config.Features);
            Assert.Empty(config.Features.Tools);
            Assert.Empty(config.Features.Prompts);
            Assert.Empty(config.Features.Resources);
        }

        [Fact]
        public void Save_then_Load_round_trips_the_feature_map()
        {
            var path = PathFor("config.json");
            var config = new GodotMcpConfig();
            config.Features.Tools.Add(new GodotMcpFeatureState("ping", false));
            config.Features.Tools.Add(new GodotMcpFeatureState("node-find", true));
            config.Features.Prompts.Add(new GodotMcpFeatureState("greet", false));
            config.Features.Resources.Add(new GodotMcpFeatureState("res-a", false));

            GodotMcpConfigStore.Save(path, config);
            var loaded = GodotMcpConfigStore.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Features.Tools.Count);
            Assert.False(loaded.Features.Tools.Single(t => t.Name == "ping").Enabled);
            Assert.True(loaded.Features.Tools.Single(t => t.Name == "node-find").Enabled);
            Assert.False(loaded.Features.Prompts.Single(p => p.Name == "greet").Enabled);
            Assert.False(loaded.Features.Resources.Single(r => r.Name == "res-a").Enabled);
        }

        [Fact]
        public void Saved_json_uses_the_features_key_and_per_kind_lists()
        {
            var path = PathFor("config.json");
            var config = new GodotMcpConfig();
            config.Features.Tools.Add(new GodotMcpFeatureState("ping", false));

            GodotMcpConfigStore.Save(path, config);
            var json = File.ReadAllText(path);

            Assert.Contains("\"features\"", json);
            Assert.Contains("\"tools\"", json);
            Assert.Contains("\"ping\"", json);
        }

        [Fact]
        public void ApplyPersisted_seeds_the_feature_map_onto_the_boot_config()
        {
            var persisted = new GodotMcpConfig();
            persisted.Features.Tools.Add(new GodotMcpFeatureState("ping", false));

            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, persisted);

            Assert.Single(target.Features.Tools);
            Assert.False(target.Features.Tools[0].Enabled);
        }

        [Fact]
        public void Loading_an_older_config_without_a_features_key_yields_an_empty_map()
        {
            // Backwards-compat: a config file written before the features section existed has no `features`
            // key — it must deserialize to a non-null, empty map (the property initializer), not null.
            var path = PathFor("legacy.json");
            File.WriteAllText(path, "{ \"host\": \"http://localhost:9000\", \"connectionMode\": \"Custom\" }");

            var loaded = GodotMcpConfigStore.Load(path);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.Features);
            Assert.Empty(loaded.Features.Tools);
            Assert.Empty(loaded.Features.Prompts);
            Assert.Empty(loaded.Features.Resources);

            // ApplyPersisted onto a fresh config likewise leaves an empty (non-null) map.
            var target = new GodotMcpConfig();
            GodotMcpConfigStore.ApplyPersisted(target, loaded);
            Assert.NotNull(target.Features);
            Assert.Empty(target.Features.Tools);
        }
    }
}
