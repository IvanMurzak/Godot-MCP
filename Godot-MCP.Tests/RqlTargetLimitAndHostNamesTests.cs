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
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Rql;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// rql-mutate's target limit (<see cref="RqlTargetLimit"/>: default 200, max_targets up to 1000) and the pure halves of the
    /// host-name hooks (<see cref="RqlHostNames"/>: known members of polymorphic streams, enum aliases), over a real RqlEngine.
    /// </summary>
    public class RqlTargetLimitAndHostNamesTests
    {
        public enum Preset { TopLeft = 0, Center = 8, FullRect = 15 }

        public class Item
        {
            public string name { get; set; } = "";
            public int value { get; set; }
            public Preset preset { get; set; }
        }

        /// <summary>A subclass member a stream typed Item cannot reach statically (like MeshInstance3D.Mesh in nodes).</summary>
        public sealed class Special : Item
        {
            public int extra { get; set; }
        }

        private static RqlEngine Engine(int count)
        {
            var items = Enumerable.Range(0, count).Select(i => new Item { name = "n" + i }).ToList();
            var engine = new RqlEngine();
            engine.Mutations.Enabled = true;
            RqlTargetLimit.Configure(engine.Mutations);
            engine.Defaults.BudgetsAdjustable = false;
            engine.AddSource("items", () => items);
            return engine;
        }

        private static QueryResult Mutate(RqlEngine engine, string query, int maxTargets = RqlTargetLimit.Default) =>
            engine.Execute(query, RqlTargetLimit.BudgetsFor(maxTargets), new QueryOptions { AllowMutation = true });

        private static string Text(QueryResult r) => string.Join("; ", r.Diagnostics.Select(d => $"{d.Code}: {d.Message} ({d.Hint})"));

        // ------------------------------------------------------------ target limit

        [Fact]
        public void TheDefaultLimitIs200AndTheHintNamesMaxTargets()
        {
            Assert.True(Mutate(Engine(200), "items | set value = 1").Ok);
            var r = Mutate(Engine(300), "items | set value = 1");
            Assert.False(r.Ok);
            var error = Assert.Single(r.Diagnostics, d => d.Code == "mutation-limit");
            Assert.Contains("300 objects matched", error.Message);
            Assert.Contains("max_targets = 300", error.Hint);
        }

        [Fact]
        public void MaxTargetsRaisesTheLimitUpToTheCeiling()
        {
            var engine = Engine(300);
            var r = Mutate(engine, "items | set value = 1", maxTargets: 300);
            Assert.True(r.Ok, Text(r));
            Assert.Equal(300, r.Rows!.Count);
            Assert.False(Mutate(Engine(1001), "items | set value = 1", maxTargets: RqlTargetLimit.Ceiling).Ok);
        }

        [Fact]
        public void MaxTargetsOutsideOneToTheCeilingIsRefused()
        {
            Assert.Null(RqlTargetLimit.Problem(1));
            Assert.Null(RqlTargetLimit.Problem(RqlTargetLimit.Ceiling));
            Assert.Contains("between 1 and 1000", RqlTargetLimit.Problem(1001));
            Assert.NotNull(RqlTargetLimit.Problem(0));
        }

        [Fact]
        public void TheGuideStatesTheLimitAndTheConditional()
        {
            foreach (var text in new[] { RqlGuide.MaxTargetsDescription, RqlGuide.MutateDescription })
            {
                Assert.Contains($"{RqlTargetLimit.Default}", text);
                Assert.Contains($"{RqlTargetLimit.Ceiling}", text);
            }
            Assert.Contains(RqlTargetLimit.Argument, RqlGuide.MutateDescription);
            Assert.DoesNotContain("20 targets", RqlGuide.MutateDescription);
            Assert.Contains("cond ? a : b", RqlGuide.Language);
            Assert.Contains("if(cond, a, b)", RqlGuide.Language);
        }

        // ------------------------------------------------------------ known members

        [Fact]
        public void AMemberOnlyASubclassDeclaresIsNotATypoWhenTheHostKnowsIt()
        {
            const string query = "items | where extra > 0 | set value = 1";
            var typo = Mutate(Engine(3), query);
            Assert.False(typo.Ok);
            Assert.Contains(typo.Diagnostics, d => d.Code == "unknown-member" && d.Message.Contains("nothing was changed"));

            var known = RqlHostNames.MemberNames(new[] { typeof(Special) });
            Assert.Contains("EXTRA", known); // case-insensitive
            var engine = Engine(3);
            engine.IsKnownMember = known.Contains;
            var r = Mutate(engine, query);
            Assert.True(r.Ok, Text(r));
            Assert.Contains(r.Diagnostics, d => d.Code == "unknown-member" && d.Severity == DiagnosticSeverity.Warning);

            // A real typo stays an error with the hook installed.
            Assert.False(Mutate(engine, "items | where extar > 0 | set value = 1").Ok);
        }

        // ------------------------------------------------------------ enum aliases

        [Fact]
        public void EnumAliasesMapOtherNamesToEntriesAndDropAmbiguousOnes()
        {
            var aliases = RqlHostNames.EnumAliases(typeof(Preset), new List<(string, long)>
            {
                ("PRESET_TOP_LEFT", 0), ("PRESET_FULL_RECT", 15), ("Full Rect", 15),
                ("FullRect", 15), // already the entry name: left to RQL
                ("Dup", 0), ("DUP", 8), // two entries: dropped
                ("PRESET_UNKNOWN", 99), // no entry with that value
            });
            Assert.NotNull(aliases);
            Assert.Equal(new Dictionary<string, string>
            {
                ["PRESET_TOP_LEFT"] = "TopLeft", ["PRESET_FULL_RECT"] = "FullRect", ["Full Rect"] = "FullRect",
            }, aliases!.ToDictionary(kv => kv.Key, kv => kv.Value));
            Assert.Null(RqlHostNames.EnumAliases(typeof(Preset), new List<(string, long)> { ("Center", 8) }));
            Assert.Null(RqlHostNames.EnumAliases(typeof(Item), new List<(string, long)> { ("x", 0) }));
        }

        [Fact]
        public void TheEngineResolvesEnumAliasesInWritesAndComparisons()
        {
            var engine = Engine(2);
            engine.EnumAliases = t => t == typeof(Preset)
                ? RqlHostNames.EnumAliases(t, new List<(string, long)> { ("PRESET_FULL_RECT", 15), ("Full Rect", 15) })
                : null;
            var r = Mutate(engine, "items | where name == \"n0\" | set preset = \"PRESET_FULL_RECT\"");
            Assert.True(r.Ok, Text(r));
            var q = engine.Execute("items | where preset == \"Full Rect\" | select name");
            Assert.True(q.Ok, Text(q));
            Assert.Single(q.Rows!);
        }
    }
}
