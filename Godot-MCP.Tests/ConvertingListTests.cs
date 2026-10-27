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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The view RQL gets of a Godot array (<see cref="GodotVariants.Elements"/>): elements are the held values, writes go into the
    /// array. Here over a list of boxed strings standing in for Variants; the live smoke covers a real Godot array.
    /// </summary>
    public class ConvertingListTests
    {
        private sealed class Box { public Box(object? v) { V = v; } public object? V { get; } }

        private static (List<Box> inner, ConvertingList<Box> view) Make(params object?[] values)
        {
            var inner = values.Select(v => new Box(v)).ToList();
            return (inner, new ConvertingList<Box>(inner, b => b.V, v => new Box(v)));
        }

        [Fact]
        public void ElementsAreTheHeldValues()
        {
            var (_, view) = Make("a", 2L, null);
            Assert.Equal(new object?[] { "a", 2L, null }, view.ToArray());
            Assert.Equal(3, view.Count);
            Assert.Equal(2L, view[1]);
            Assert.Equal(new object?[] { "a", 2L, null }, ((IEnumerable)view).Cast<object?>().ToArray());
        }

        [Fact]
        public void WritesGoIntoTheUnderlyingList()
        {
            var (inner, view) = Make("a", "b");
            view[1] = "B";
            view.Add("c");
            view.Insert(0, "z");
            view.Remove("a");
            Assert.Equal(new object?[] { "z", "B", "c" }, inner.Select(b => b.V).ToArray());
            Assert.Equal(1, view.IndexOf("B"));
            Assert.True(view.Contains("c"));
            view.RemoveAt(0);
            Assert.Equal(new object?[] { "B", "c" }, inner.Select(b => b.V).ToArray());
            var copy = new object?[3];
            view.CopyTo(copy, 1);
            Assert.Equal(new object?[] { null, "B", "c" }, copy);
            view.Clear();
            Assert.Empty(inner);
        }

        [Fact]
        public void IsAListOfObjectsForRql()
        {
            var (_, view) = Make(1L);
            Assert.IsAssignableFrom<IList>(view);
            Assert.IsAssignableFrom<IReadOnlyList<object?>>(view);
            Assert.False(view.IsFixedSize);
        }
    }
}
