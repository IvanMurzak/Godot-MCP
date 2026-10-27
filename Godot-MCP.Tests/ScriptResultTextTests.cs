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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// <see cref="ScriptResultText"/>: script-execute results in the RQL format read like rql-query rows — a dictionary's
    /// entries (not its Count/Capacity internals), a list's items (not name/typeName/value tables), an anonymous object's
    /// members — with C#-style type names.
    /// </summary>
    public class ScriptResultTextTests
    {
        private static readonly Func<object, string?> NoEngine = _ => null;

        // Stands in for an engine value (a Godot struct / node) the caller renders itself.
        private readonly struct Vec { public Vec(int x, int y) { X = x; Y = y; } public int X { get; } public int Y { get; } }

        private static string? Engine(object o) => o is Vec v ? $"({v.X}, {v.Y})" : null;

        public record Item(string Name, int Price);

        [Theory]
        [InlineData(42)]
        [InlineData("text")]
        [InlineData(true)]
        [InlineData(1.5)]
        public void ScalarsKeepTheirSerializedForm(object value) => Assert.Null(ScriptResultText.Format(value, NoEngine));

        [Fact]
        public void NullKeepsItsSerializedForm() => Assert.Null(ScriptResultText.Format(null, NoEngine));

        [Fact]
        public void DictionaryShowsItsEntries() =>
            Assert.Equal("Dictionary<string, int>: {a: 1, b: 2}",
                ScriptResultText.Format(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, NoEngine));

        [Fact]
        public void KeysThatCouldBeMisreadAreQuoted() =>
            Assert.Equal("Dictionary<string, int>: {res://levels/a.tscn: 3, \"x, y\": 1, \"\": 0}",
                ScriptResultText.Format(new Dictionary<string, int> { ["res://levels/a.tscn"] = 3, ["x, y"] = 1, [""] = 0 }, NoEngine));

        [Fact]
        public void ListShowsItsItemsQuoted() =>
            Assert.Equal("List<string>: [\"a\", \"b \\\"q\\\"\"]", ScriptResultText.Format(new List<string> { "a", "b \"q\"" }, NoEngine));

        [Fact]
        public void ArraysAndNestingAndEngineValues()
        {
            Assert.Equal("int[]: [1, 2, 3]", ScriptResultText.Format(new[] { 1, 2, 3 }, NoEngine));
            Assert.Equal("List<Vec>: [(1, 2), (3, 4)]", ScriptResultText.Format(new List<Vec> { new Vec(1, 2), new Vec(3, 4) }, Engine));
            Assert.Equal("Dictionary<string, List<int>>: {odd: [1, 3], even: []}",
                ScriptResultText.Format(new Dictionary<string, List<int>> { ["odd"] = new List<int> { 1, 3 }, ["even"] = new List<int>() }, NoEngine));
        }

        [Fact]
        public void AnonymousObjectsRecordsAndTuplesShowTheirMembers()
        {
            Assert.Equal("anonymous: {name: \"x\", count: 3, tags: [\"a\"]}",
                ScriptResultText.Format(new { name = "x", count = 3, tags = new[] { "a" } }, NoEngine));
            Assert.Equal("Item: {Name: \"Sword\", Price: 25}", ScriptResultText.Format(new Item("Sword", 25), NoEngine));
            Assert.Equal("(string, int): (\"x\", 3)", ScriptResultText.Format(("x", 3), NoEngine));
        }

        [Fact]
        public void GenericOnlyMapsShowEntries() =>
            Assert.Equal("ReadOnlyMap: {k: 7}", ScriptResultText.Format(new ReadOnlyMap(), NoEngine));

        [Fact]
        public void LongCollectionsAreCounted()
        {
            var text = ScriptResultText.Format(Enumerable.Range(0, 250).ToList(), NoEngine)!;
            Assert.StartsWith("List<int>: [0, 1, 2", text);
            Assert.EndsWith(", … (250 items)]", text);
            Assert.Equal(ScriptResultText.MaxItems, text.Split(',').Length - 1); // 100 items, then the count
        }

        [Fact]
        public void DeepNestingStops()
        {
            var text = ScriptResultText.Format(new List<object> { new List<object> { new List<object> { new List<object> { new List<object> { 1 } } } } }, NoEngine);
            Assert.Equal("List<object>: [[[[…]]]]", text);
        }

        [Fact]
        public void AThrowingMemberIsNamedNotFatal() =>
            Assert.Equal("Throwing: {Ok: 1, Bad: \"<InvalidOperationException>\"}", ScriptResultText.Format(new Throwing(), NoEngine));

        private sealed class ReadOnlyMap : IReadOnlyDictionary<string, int>
        {
            private readonly Dictionary<string, int> _d = new Dictionary<string, int> { ["k"] = 7 };
            public int this[string key] => _d[key];
            public IEnumerable<string> Keys => _d.Keys;
            public IEnumerable<int> Values => _d.Values;
            public int Count => _d.Count;
            public bool ContainsKey(string key) => _d.ContainsKey(key);
            public bool TryGetValue(string key, out int value) => _d.TryGetValue(key, out value);
            public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _d.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _d.GetEnumerator();
        }

        public sealed class Throwing
        {
            public int Ok => 1;
            public int Bad => throw new InvalidOperationException("boom");
        }
    }
}
