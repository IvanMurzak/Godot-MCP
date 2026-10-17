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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rql;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Extensions.Tests
{
    /// <summary>
    /// The checks every extension's agent guide must pass, shared by the extension test projects (each links this file;
    /// the test assemblies are separate, so the type exists once per assembly). A derived class names its guide: every
    /// example parses, the guide markdown carries them, and the live e2e manifest (<c>tests/e2e/e2e-tools.json</c>, copied
    /// next to the test assembly) runs exactly the guide examples, in order, each with the tool its last stage needs
    /// (rql-mutate for set / call, rql-query otherwise).
    /// </summary>
    public abstract class GuideExampleTests
    {
        /// <summary>The guide's examples: one per line, the query, three or more spaces, a note.</summary>
        protected abstract string Examples { get; }

        /// <summary>The guide markdown the module registers.</summary>
        protected abstract string Markdown { get; }

        /// <summary>The queries of a guide's examples, in order (the note after three or more spaces is dropped).</summary>
        public static string[] Queries(string examples) =>
            examples.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();

        [Fact]
        public void EveryExampleParses()
        {
            var failures = Queries(Examples)
                .Select(q => RqlParser.TryParse(q, out _, out var error) ? null : $"'{q}': {error?.Message}")
                .Where(f => f != null).ToList();
            Assert.Empty(failures);
        }

        [Fact]
        public void GuideCarriesItsExamples()
        {
            Assert.NotEmpty(Queries(Examples));
            Assert.Contains(Examples, Markdown);
            Assert.All(Queries(Examples), q => Assert.DoesNotContain("   ", q));
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
            foreach (var example in Queries(Examples))
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
