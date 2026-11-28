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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Modules;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.Godot.MCP.Tools;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// csharp-exec results: the type shown before the value, the plain form made before the snippet unloads
    /// (<see cref="ScriptResultText.Plain"/>: the snippet's own types become maps and lists, host values stay), and the text
    /// the RQL tool binding presents it as (<c>Engine.Present</c>, as any tool result) — with a plain engine and the Godot
    /// value adapters (pure-managed Godot structs only: native-backed values need a live Godot, see smoke_csharp_exec.py).
    /// </summary>
    public class ScriptResultTextTests
    {
        public record Item(string Name, int Price);

        public enum SnippetMode { Fast, Slow }

        public sealed class Snippety
        {
            public string Label = "x";
            public int Count => 3;
            public int Broken => throw new InvalidOperationException("nope");
            public List<Item> Items { get; } = new List<Item> { new Item("a", 1) };
        }

        private static readonly Type[] SnippetTypes = { typeof(Item), typeof(Snippety), typeof(SnippetMode) };

        // Stands in for Code-Exec's snippet-assembly test: the test's own record / class play the snippet's types.
        private static bool IsSnippet(Type t) =>
            SnippetTypes.Contains(t) || (t.HasElementType && IsSnippet(t.GetElementType()!)) || (t.IsGenericType && t.GetGenericArguments().Any(IsSnippet))
            || t.Name.Contains("AnonymousType", StringComparison.Ordinal);

        private static string Presented(object? value, string typeName)
        {
            var engine = new RqlEngine();
            engine.AddModules(new[] { typeof(GodotValuesModule) });
            var binding = new RqlToolBinding(() => engine, mainThread: null);
            var method = typeof(ScriptResultTextTests).GetMethod(nameof(Presented), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var plain = ScriptResultText.Plain(value, IsSnippet);
            return string.Join("\n", binding.Result(method, new ScriptExecuteResult { Result = plain, TypeName = typeName, MaxValueChars = 20_000 })!.Content.Select(c => c.Text));
        }

        [Theory]
        [InlineData(typeof(int), "System.Int32")]
        [InlineData(typeof(string), "System.String")]
        [InlineData(typeof(bool), "System.Boolean")]
        [InlineData(typeof(List<global::Godot.Vector3>), "List<Vector3>")]
        [InlineData(typeof(Dictionary<string, int>), "Dictionary<string, int>")]
        [InlineData(typeof(int[]), "int[]")]
        [InlineData(typeof((int, string)), "(int, string)")]
        public void ShownTypeNames(Type type, string expected) => Assert.Equal(expected, ScriptResultText.ShownTypeName(type));

        [Fact]
        public void AnonymousTypesReadAnonymous() => Assert.Equal("anonymous", ScriptResultText.ShownTypeName(new { a = 1 }.GetType()));

        [Fact]
        public void HostValuesStayAsTheyAre()
        {
            var list = new List<int> { 1, 2 };
            Assert.Same(list, ScriptResultText.Plain(list, IsSnippet));
            var map = new Dictionary<string, global::Godot.Vector3>();
            Assert.Same(map, ScriptResultText.Plain(map, IsSnippet));
            Assert.Equal(42, ScriptResultText.Plain(42, IsSnippet));
        }

        [Fact]
        public void SnippetValuesBecomeMapsAndLists()
        {
            var plain = Assert.IsType<Dictionary<string, object?>>(ScriptResultText.Plain(new Snippety(), IsSnippet));
            Assert.Equal("x", plain["Label"]);
            Assert.Equal(3, plain["Count"]);
            Assert.Equal("<InvalidOperationException>", plain["Broken"]);
            var items = Assert.IsType<List<object?>>(plain["Items"]);
            var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(items));
            Assert.Equal(new Dictionary<string, object?> { ["Name"] = "a", ["Price"] = 1 }, item);
            // A host collection that holds snippet values is copied too; nothing of the snippet type survives.
            var mixed = Assert.IsType<List<object?>>(ScriptResultText.Plain(new List<object> { 1, new Item("b", 2) }, IsSnippet));
            Assert.Equal(1, mixed[0]);
            Assert.IsType<Dictionary<string, object?>>(mixed[1]);
        }

        [Fact]
        public void SnippetEnumsNestedSequencesAndCodeAreNotKept()
        {
            var plain = Assert.IsType<Dictionary<string, object?>>(ScriptResultText.Plain(
                new { mode = SnippetMode.Slow, xs = Enumerable.Range(0, 3).Select(i => i * 2), f = (Func<int>)(() => 1) }, IsSnippet));
            Assert.Equal("Slow", plain["mode"]);
            Assert.Equal(new List<object?> { 0, 2, 4 }, plain["xs"]);
            Assert.IsType<string>(plain["f"]);
            // Host enums and collections stay as they are.
            Assert.Equal(DayOfWeek.Monday, ScriptResultText.Plain(DayOfWeek.Monday, IsSnippet));
        }

        [Fact]
        public void ASelfContainingValueStops()
        {
            var list = new List<object>();
            list.Add(list);
            var plain = Assert.IsType<List<object?>>(ScriptResultText.Plain(list, IsSnippet));
            Assert.IsType<string>(Assert.Single(plain));
        }

        [Fact]
        public void ScalarsReadAsThemselves()
        {
            Assert.Equal("System.Int32: 3", Presented(3, "System.Int32"));
            Assert.Equal("System.String: 4.3.0", Presented("4.3.0", "System.String"));
        }

        [Fact]
        public void ADictionaryShowsItsEntries() =>
            Assert.Equal("Dictionary<string, int>: {a:1,b:2}", Presented(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, "Dictionary<string, int>"));

        [Fact]
        public void AnAnonymousObjectShowsItsMembers() =>
            Assert.Equal("anonymous: {name:x,count:3}", Presented(new { name = "x", count = 3 }, "anonymous"));

        [Fact]
        public void GodotStructsReadAsRqlLiterals()
        {
            Assert.Equal("List<Vector3>: 1 result\n(1, 2, 3)", Presented(new List<global::Godot.Vector3> { new global::Godot.Vector3(1, 2, 3) }, "List<Vector3>"));
            Assert.Equal("Basis: (1, 0, 0, 0, 1, 0, 0, 0, 1)", Presented(global::Godot.Basis.Identity, "Basis"));
        }

        [Fact]
        public void ARecordListReadsAsRows() =>
            Assert.Equal("List<Item>: 2 results\nName|Price\na|1\nb|2", Presented(new List<Item> { new Item("a", 1), new Item("b", 2) }, "List<Item>"));
    }
}
