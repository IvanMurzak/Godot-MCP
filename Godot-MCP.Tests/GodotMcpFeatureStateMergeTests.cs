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
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed MCP-feature enable-map merge/capture logic (<see cref="GodotMcpFeatureStateMerge"/>):
    /// the boot REAPPLY (saved-map ⊕ live-names → explicit SetEnabled calls), the CAPTURE (live → persistable
    /// list), and the single-item UPSERT used on a user toggle. These rules are the unit-testable core behind the
    /// <c>#if TOOLS</c> editor wiring that talks to the live managers; the wiring itself is verified via the
    /// headless Godot smoke (test.md Suite 3), not here.
    /// </summary>
    public class GodotMcpFeatureStateMergeTests
    {
        static GodotMcpFeatureState S(string name, bool enabled) => new(name, enabled);

        [Fact]
        public void ComputeReapply_default_empty_map_disables_nothing()
        {
            // DEFAULT-EMPTY = ALL-ENABLED: with no saved entries, no explicit SetEnabled calls are emitted, so
            // every live item stays at the manager default (enabled).
            var live = new[] { "a", "b", "c" };
            var result = GodotMcpFeatureStateMerge.ComputeReapply(live, new List<GodotMcpFeatureState>());

            Assert.Empty(result);
        }

        [Fact]
        public void ComputeReapply_applies_only_saved_entries_matching_live_items()
        {
            var live = new[] { "a", "b", "c" };
            var saved = new List<GodotMcpFeatureState> { S("a", false), S("c", true) };

            var result = GodotMcpFeatureStateMerge.ComputeReapply(live, saved);

            // Only the two saved names are emitted; "b" (no saved entry) is omitted (stays at default).
            Assert.Equal(2, result.Count);
            Assert.False(result["a"]);
            Assert.True(result["c"]);
            Assert.False(result.ContainsKey("b"));
        }

        [Fact]
        public void ComputeReapply_prunes_unknown_saved_names()
        {
            // A saved entry for a removed/renamed item ("ghost") is dropped — UNKNOWN-NAME PRUNING.
            var live = new[] { "a" };
            var saved = new List<GodotMcpFeatureState> { S("a", false), S("ghost", false) };

            var result = GodotMcpFeatureStateMerge.ComputeReapply(live, saved);

            Assert.Single(result);
            Assert.False(result["a"]);
            Assert.False(result.ContainsKey("ghost"));
        }

        [Fact]
        public void ComputeReapply_last_wins_on_duplicate_saved_names()
        {
            var live = new[] { "a" };
            var saved = new List<GodotMcpFeatureState> { S("a", true), S("a", false) };

            var result = GodotMcpFeatureStateMerge.ComputeReapply(live, saved);

            Assert.False(result["a"]); // last entry wins
        }

        [Fact]
        public void ComputeReapply_ignores_null_and_empty_named_saved_entries()
        {
            var live = new[] { "a" };
            var saved = new List<GodotMcpFeatureState> { null!, S("", false), S("a", false) };

            var result = GodotMcpFeatureStateMerge.ComputeReapply(live, saved);

            Assert.Single(result);
            Assert.False(result["a"]);
        }

        [Fact]
        public void Capture_snapshots_every_live_item()
        {
            var live = new (string, bool)[] { ("a", true), ("b", false), ("c", true) };

            var captured = GodotMcpFeatureStateMerge.Capture(live);

            Assert.Equal(3, captured.Count);
            Assert.Equal("a", captured[0].Name);
            Assert.True(captured[0].Enabled);
            Assert.Equal("b", captured[1].Name);
            Assert.False(captured[1].Enabled);
        }

        [Fact]
        public void Capture_skips_empty_names_and_dedupes()
        {
            var live = new (string, bool)[] { ("a", true), ("", false), ("a", false) };

            var captured = GodotMcpFeatureStateMerge.Capture(live);

            Assert.Single(captured);
            Assert.Equal("a", captured[0].Name);
            Assert.True(captured[0].Enabled); // first "a" wins (dedupe keeps first)
        }

        [Fact]
        public void Capture_then_ComputeReapply_round_trips_disabled_state()
        {
            // A user disables "b"; capturing then reapplying restores exactly that disable.
            var live = new (string Name, bool Enabled)[] { ("a", true), ("b", false), ("c", true) };
            var saved = GodotMcpFeatureStateMerge.Capture(live);

            var reapply = GodotMcpFeatureStateMerge.ComputeReapply(live.Select(x => x.Name), saved);

            Assert.True(reapply["a"]);
            Assert.False(reapply["b"]);
            Assert.True(reapply["c"]);
        }

        [Fact]
        public void Upsert_inserts_a_new_entry()
        {
            var saved = new List<GodotMcpFeatureState>();

            GodotMcpFeatureStateMerge.Upsert(saved, "a", false);

            Assert.Single(saved);
            Assert.Equal("a", saved[0].Name);
            Assert.False(saved[0].Enabled);
        }

        [Fact]
        public void Upsert_updates_an_existing_entry_in_place()
        {
            var saved = new List<GodotMcpFeatureState> { S("a", true), S("b", true) };

            GodotMcpFeatureStateMerge.Upsert(saved, "a", false);

            Assert.Equal(2, saved.Count); // no new entry added
            Assert.False(saved.Single(s => s.Name == "a").Enabled);
            Assert.True(saved.Single(s => s.Name == "b").Enabled);
        }

        [Fact]
        public void Upsert_ignores_empty_name()
        {
            var saved = new List<GodotMcpFeatureState>();

            GodotMcpFeatureStateMerge.Upsert(saved, "", true);

            Assert.Empty(saved);
        }
    }
}
