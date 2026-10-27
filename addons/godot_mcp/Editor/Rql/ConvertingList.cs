/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// A live view of a list whose elements are read and written through converters. A Godot array holds Variants, which RQL
    /// would stream as Variant structs (members VariantType and Obj only): <c>get props["items"] | where props["price"] &gt; 10</c>
    /// found nothing. Through this view the elements are the values the Variants hold (a Resource, a number, a nested array), and
    /// an index write goes into the underlying array. Pure (no Godot API): unit-tested over a plain list.
    /// </summary>
    public sealed class ConvertingList<T> : IList, IReadOnlyList<object?>
    {
        private readonly Func<T, object?> _read;
        private readonly Func<object?, T> _write;

        public ConvertingList(IList<T> inner, Func<T, object?> read, Func<object?, T> write)
        {
            Inner = inner;
            _read = read;
            _write = write;
        }

        /// <summary>The list the view reads and writes.</summary>
        public IList<T> Inner { get; }

        public object? this[int index]
        {
            get => _read(Inner[index]);
            set => Inner[index] = _write(value);
        }

        public int Count => Inner.Count;
        public bool IsReadOnly => Inner.IsReadOnly;
        public bool IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        public int Add(object? value)
        {
            Inner.Add(_write(value));
            return Inner.Count - 1;
        }

        public void Clear() => Inner.Clear();
        public bool Contains(object? value) => IndexOf(value) >= 0;

        public int IndexOf(object? value)
        {
            for (int i = 0; i < Inner.Count; i++)
                if (Equals(_read(Inner[i]), value)) return i;
            return -1;
        }

        public void Insert(int index, object? value) => Inner.Insert(index, _write(value));

        public void Remove(object? value)
        {
            var i = IndexOf(value);
            if (i >= 0) Inner.RemoveAt(i);
        }

        public void RemoveAt(int index) => Inner.RemoveAt(index);

        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < Inner.Count; i++) array.SetValue(_read(Inner[i]), index + i);
        }

        public IEnumerator<object?> GetEnumerator()
        {
            for (int i = 0; i < Inner.Count; i++) yield return _read(Inner[i]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
