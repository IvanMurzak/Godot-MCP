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
using System.IO;
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Rql;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The agent guide of the RQL tools (<see cref="RqlGuide"/>): every example parses, mutate examples end with set / call,
    /// and <c>Godot-Tests/rql-guide-examples.json</c> — the list the live CI smoke (scripts/smoke_rql.py) runs against a real
    /// editor — mirrors the guide exactly.
    /// </summary>
    public class RqlGuideTests
    {
        public static IEnumerable<object[]> QueryExamples() => RqlGuide.QueryExamples.Select(e => new object[] { e });
        public static IEnumerable<object[]> MutateExamples() => RqlGuide.MutateExamples.Select(e => new object[] { e });

        [Theory]
        [MemberData(nameof(QueryExamples))]
        public void QueryExamplesParse(string query)
        {
            Assert.True(RqlParser.TryParse(query, out var plan, out var error), $"'{query}': {error?.Message}");
            Assert.NotNull(plan);
        }

        [Theory]
        [MemberData(nameof(MutateExamples))]
        public void MutateExamplesParseAndEndWithSetOrCall(string query)
        {
            Assert.True(RqlParser.TryParse(query, out _, out var error), $"'{query}': {error?.Message}");
            var last = query.Split('|').Last().TrimStart();
            Assert.True(last.StartsWith("set ") || last.StartsWith("call "), $"'{query}' must end with a set or call stage");
        }

        [Fact]
        public void ExampleListsAreNotEmptyAndHaveNoNotesLeft()
        {
            Assert.Equal(28, RqlGuide.QueryExamples.Length);
            Assert.Equal(41, RqlGuide.MutateExamples.Length);
            Assert.All(RqlGuide.MutateExamples, e => Assert.DoesNotContain("   ", e));
        }

        [Fact]
        public void TheGuideTeachesTheStringFunctionsGroupByKeysAndTargetScope()
        {
            foreach (var function in new[] { "replace(", "replace-regex(", "substring(", "split(", "trim(", "pad(" })
                Assert.Contains(function, RqlGuide.Language);
            Assert.Contains("group-by a, b", RqlGuide.Language);
            Assert.Contains("$index", RqlGuide.MutateStages);
            Assert.Contains(RqlGuide.MutateExamples, e => e.Contains("$index") && e.Contains("set "));
            Assert.Contains(RqlGuide.MutateExamples, e => e.Contains(".replace("));
            Assert.Contains(RqlGuide.QueryExamples, e => e.Contains("group-by class, "));
            Assert.Contains(RqlGuide.QueryExamples, e => e.Contains("items.avg("));
            Assert.Contains("a boolean condition", RqlGuide.Language);
            Assert.Contains("has(Mesh)", RqlGuide.QuerySyntaxShort);
            // The shared rules are the runtime's, with Godot's creation call (conditions, list equality, receipts, call, bulk create,
            // vectors, map entries, literal kinds).
            var rules = global::Rql.Execution.RqlLanguageGuide.RulesWith(new global::Rql.Execution.RqlRuleExamples
            {
                CreateCall = RqlGuide.RuleCreateCall,
                Grid = RqlGuide.RuleGridExample,
                PerEntry = RqlGuide.RulePerEntryExample,
                Named = RqlGuide.RuleNamedExample,
            });
            Assert.EndsWith(rules, RqlGuide.Language);
            Assert.Equal(RqlGuide.SharedRules, rules);
            // RQL aacfbba: the rules show Godot's own creation calls (real classes, CreateNode's parameter names), no placeholder.
            Assert.Contains("range(0, 300) | call " + RqlGuide.RuleGridExample, RqlGuide.SharedRules);
            Assert.Contains(RqlGuide.RulePerEntryExample, RqlGuide.SharedRules);
            Assert.Contains("range(0, 10) | call " + RqlGuide.RuleNamedExample, RqlGuide.SharedRules);
            Assert.DoesNotContain("stands for", RqlGuide.SharedRules);
            Assert.DoesNotContain("\"Cube\"", RqlGuide.SharedRules);
            Assert.DoesNotContain("editor.CreateObject", RqlGuide.Language);
            Assert.Contains("Lists compare by value", RqlGuide.Language);
            Assert.Contains(RqlGuide.MutateExamples, e => e.StartsWith("range(0, ") && e.Contains("call editor.CreateNode("));
            Assert.Contains(RqlGuide.MutateExamples, e => e.StartsWith("[[") && e.Contains("call editor.InstantiateScene("));
            Assert.Contains(RqlGuide.MutateExamples, e => e.Contains("set Scale = Scale * 1.5"));
            // RQL 2f4e263: list arithmetic and vector functions; wave-6 agent rules.
            Assert.Contains(RqlGuide.MutateExamples, e => e.Contains("[Position.X, 0, Position.Z] * 2"));
            Assert.Contains(RqlGuide.QueryExamples, e => e.Contains("distance(Position, "));
            foreach (var function in new[] { "length(v)", "normalize(v)", "distance(a, b)", "dot(a, b)", "lerp(a, b, t)" })
                Assert.Contains(function, RqlGuide.Language);
            Assert.Contains("Units: meters in 3D, pixels in 2D", RqlGuide.Language);
            Assert.Contains("list what matches with rql-query and show it", RqlGuide.MutateDescription);
            Assert.Contains("tell the user", RqlGuide.MutateDescription);
            Assert.Contains("{$type: \"BoxMesh\", Size: [1, 2, 1]}", RqlGuide.MutateDescription);
            Assert.Contains("one undo step", RqlGuide.MutateDescription);
            Assert.Contains("unchanged assignment(s) not listed", RqlGuide.MutateDescription);
        }

        [Fact]
        public void TheToolDescriptionsCarryTheGuide()
        {
            Assert.Contains(RqlGuide.Language, RqlGuide.QueryDescription);
            Assert.Contains(RqlGuide.QueryExampleText, RqlGuide.QueryDescription);
            Assert.Contains(RqlGuide.MutateExampleText, RqlGuide.MutateDescription);
            Assert.Contains("SaveScene", RqlGuide.MutateDescription);
        }

        [Fact]
        public void TheLiveSmokeRunsExactlyTheGuideExamples()
        {
            var path = Path.Combine(RepoRoot(), "Godot-Tests", "rql-guide-examples.json");
            Assert.True(File.Exists(path), $"{path} is missing");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var query = doc.RootElement.GetProperty("query").EnumerateArray().Select(e => e.GetString()).ToArray();
            var mutate = doc.RootElement.GetProperty("mutate").EnumerateArray().Select(e => e.GetString()).ToArray();
            Assert.True(RqlGuide.QueryExamples.SequenceEqual(query!),
                "Godot-Tests/rql-guide-examples.json 'query' differs from RqlGuide.QueryExampleText; copy the examples over (same order)");
            Assert.True(RqlGuide.MutateExamples.SequenceEqual(mutate!),
                "Godot-Tests/rql-guide-examples.json 'mutate' differs from RqlGuide.MutateExampleText; copy the examples over (same order)");
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Godot-MCP.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
