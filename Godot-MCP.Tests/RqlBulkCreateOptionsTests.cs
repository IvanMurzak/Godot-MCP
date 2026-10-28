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
    /// <summary>A stand-in for the editor source: records what each bulk-create call received.</summary>
    public sealed class RqlFakeEditor
    {
        public readonly List<(string type, NodeOptions? options)> Created = new List<(string, NodeOptions?)>();

        [RqlCallable("test creation method")]
        public string CreateNode(string className, NodeOptions? options = null)
        {
            Created.Add((className, options));
            return className + Created.Count;
        }
    }

    /// <summary>
    /// The options object of <c>editor.CreateNode(class, {…})</c> / <c>editor.InstantiateScene(path, {…})</c> as RQL hands it over
    /// once per stream item (the node creation itself is native and verified by the live smoke): name, parent and Godot
    /// properties split, values kept as written, item metas evaluated per call, and the batch rule against duplicate names.
    /// </summary>
    public class RqlBulkCreateOptionsTests
    {
        private readonly RqlFakeEditor _editor = new RqlFakeEditor();

        private QueryResult Mutate(string q, bool dryRun = false)
        {
            var engine = new RqlEngine();
            engine.Mutations.Enabled = true;
            engine.AddSource("editor", () => _editor);
            engine.AddAdapter(new Godot_NodeOptions_Adapter());
            return engine.Execute(q, null, new QueryOptions { AllowMutation = true, DryRun = dryRun });
        }

        private static string Text(QueryResult r) => AgentFormat.Render(r, OutputFormat.Table);

        [Fact]
        public void ARangeCallsOncePerItemWithThatItemsOptions()
        {
            var r = Mutate("range(0, 3) | call editor.CreateNode(\"MeshInstance3D\", {name: \"Cube\" + $, parent: \"Props\", position: [$ * 2, 0, 0], Visible: false})");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(3, r.Rows!.Count);
            Assert.Equal(new[] { "Cube0", "Cube1", "Cube2" }, _editor.Created.Select(c => c.options!.Name));
            var last = _editor.Created[2].options!;
            Assert.Equal("Props", last.Parent);
            Assert.Equal(new[] { "position", "Visible" }, last.Properties.Select(p => p.Key));
            Assert.Equal(new object?[] { 4.0, 0.0, 0.0 }, ((IEnumerable<object?>)last.Properties[0].Value!).ToArray());
        }

        [Fact]
        public void AListLiteralStreamsItsEntries()
        {
            var r = Mutate("[[\"CoinA\", 1], [\"CoinB\", 2]] | call editor.CreateNode(\"Node3D\", {name: $[0]})");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new[] { "CoinA", "CoinB" }, _editor.Created.Select(c => c.options!.Name));
        }

        [Fact]
        public void OptionsAreOptional()
        {
            Assert.True(Mutate("range(0, 2) | call editor.CreateNode(\"Node3D\")").Ok);
            Assert.All(_editor.Created, c => Assert.Null(c.options));
        }

        [Theory]
        [InlineData("range(0, 2) | call editor.CreateNode(\"Node3D\", \"Cube\")", "object literal")]
        [InlineData("range(0, 2) | call editor.CreateNode(\"Node3D\", {name: 5})", "'name' must be a string")]
        public void ABadOptionsValueFailsBeforeTheFirstCall(string query, string expected)
        {
            var r = Mutate(query);
            Assert.False(r.Ok);
            Assert.Contains(expected, Text(r));
            Assert.Empty(_editor.Created);
        }

        [Fact]
        public void ADryRunCreatesNothing()
        {
            var r = Mutate("range(0, 5) | call editor.CreateNode(\"Node3D\", {name: \"N\" + $})", dryRun: true);
            Assert.True(r.Ok, Text(r));
            Assert.Equal(5, r.Rows!.Count);
            Assert.Empty(_editor.Created);
        }

        [Fact]
        public void DuplicateNamesUnderOneParentAreFound()
        {
            var root = new object();
            var props = new object();
            Assert.Null(NodeOptions.DuplicateName(new[] { (root, (string?)"A"), (root, "B"), (props, "A"), (root, null), (root, null) }));
            Assert.Equal("B", NodeOptions.DuplicateName(new[] { (root, (string?)"A"), (props, "B"), (props, "B") }));
        }
    }
}
