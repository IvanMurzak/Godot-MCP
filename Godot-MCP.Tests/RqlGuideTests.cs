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
            Assert.Equal(27, RqlGuide.QueryExamples.Length);
            Assert.Equal(37, RqlGuide.MutateExamples.Length);
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
            Assert.Contains("a condition must be a boolean", RqlGuide.Language);
            Assert.Contains("has(Mesh)", RqlGuide.QuerySyntaxShort);
            Assert.Contains("Lists compare by value", RqlGuide.Language);
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
