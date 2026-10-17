/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rql;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    /// <summary>
    /// The agent guide: every example parses, and the live e2e manifest runs exactly the guide examples, in order, each with
    /// the tool its last stage needs (rql-mutate for set / call, rql-query otherwise).
    /// </summary>
    public class BeehaveGuideTests
    {
        public static IEnumerable<object[]> Examples() => BeehaveGuide.Queries.Select(q => new object[] { q });

        [Theory]
        [MemberData(nameof(Examples))]
        public void ExampleParses(string query) =>
            Assert.True(RqlParser.TryParse(query, out _, out var error), $"'{query}': {error?.Message}");

        [Fact]
        public void GuideCarriesItsExamples()
        {
            Assert.NotEmpty(BeehaveGuide.Queries);
            Assert.Contains(BeehaveGuide.Examples, BeehaveGuide.Markdown);
            Assert.All(BeehaveGuide.Queries, q => Assert.DoesNotContain("   ", q));
        }

        [Fact]
        public void TheLiveE2eRunsEveryGuideExampleInOrder()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "e2e-tools.json")));
            var calls = doc.RootElement.GetProperty("tools").EnumerateArray()
                .Where(c => c.TryGetProperty("input", out var input) && input.TryGetProperty("query", out _))
                .Select(c => (tool: c.GetProperty("tool").GetString()!, query: c.GetProperty("input").GetProperty("query").GetString()!))
                .ToList();
            var next = 0;
            foreach (var example in BeehaveGuide.Queries)
            {
                while (next < calls.Count && calls[next].query != example) next++;
                Assert.True(next < calls.Count, $"tests/e2e/e2e-tools.json does not run the guide example (in order): {example}");
                var last = example.Split('|').Last().TrimStart();
                var mutates = last.StartsWith("set ", StringComparison.Ordinal) || last.StartsWith("call ", StringComparison.Ordinal);
                Assert.Equal(mutates ? "rql-mutate" : "rql-query", calls[next].tool);
                next++;
            }
        }
    }
}
