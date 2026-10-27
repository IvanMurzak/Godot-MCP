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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The view RQL gets of a Godot array (<see cref="GodotVariants.Elements"/>): elements are the held values; the view is
    /// read-only. Here over a list of boxed values standing in for Variants; the live smoke covers a real Godot array.
    /// </summary>
    public class ConvertingListTests
    {
        private sealed class Box { public Box(object? v) { V = v; } public object? V { get; } }

        private static ConvertingList<Box> Make(params object?[] values) =>
            new ConvertingList<Box>(values.Select(v => new Box(v)).ToList(), b => b.V);

        [Fact]
        public void ElementsAreTheHeldValues()
        {
            var view = Make("a", 2L, null);
            Assert.Equal(new object?[] { "a", 2L, null }, view.ToArray());
            Assert.Equal(3, view.Count);
            Assert.Equal(2L, view[1]);
            Assert.Equal(1, view.IndexOf(2L));
            Assert.True(view.Contains("a"));
            Assert.Equal(new object?[] { "a", 2L, null }, ((IEnumerable)view).Cast<object?>().ToArray());
            var copy = new object?[4];
            view.CopyTo(copy, 1);
            Assert.Equal(new object?[] { null, "a", 2L, null }, copy);
        }

        [Fact]
        public void IsAReadOnlyListOfObjectsForRql()
        {
            IList view = Make(1L);
            Assert.IsAssignableFrom<IReadOnlyList<object?>>(view);
            Assert.True(view.IsReadOnly); // RQL refuses an index write into it, as it did for the Godot array itself
            Assert.Throws<NotSupportedException>(() => view[0] = 2L);
            Assert.Throws<NotSupportedException>(() => view.Add(3L));
            Assert.Equal(1L, view[0]);
        }
    }
}
