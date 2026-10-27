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
    /// A read-only view of a list whose elements are read through a converter. A Godot array holds Variants, which RQL would
    /// stream as Variant structs (members VariantType and Obj only): <c>get props["items"] | where props["price"] &gt; 10</c> found
    /// nothing. Through this view the elements are the values the Variants hold (a Resource, a number, a nested array); writes
    /// through an element object (its props) work, while an index write is refused like before: it would change the shared array
    /// behind the property's setter, so no undo step and no save. Assign the whole list instead. Pure: unit-tested.
    /// </summary>
    public sealed class ConvertingList<T> : IList, IReadOnlyList<object?>
    {
        private readonly Func<T, object?> _read;

        public ConvertingList(IList<T> inner, Func<T, object?> read)
        {
            Inner = inner;
            _read = read;
        }

        /// <summary>The list the view reads.</summary>
        public IList<T> Inner { get; }

        public object? this[int index]
        {
            get => _read(Inner[index]);
            set => throw ReadOnly();
        }

        public int Count => Inner.Count;
        public bool IsReadOnly => true;
        public bool IsFixedSize => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        public bool Contains(object? value) => IndexOf(value) >= 0;

        public int IndexOf(object? value)
        {
            for (int i = 0; i < Inner.Count; i++)
                if (Equals(_read(Inner[i]), value)) return i;
            return -1;
        }

        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < Inner.Count; i++) array.SetValue(_read(Inner[i]), index + i);
        }

        public IEnumerator<object?> GetEnumerator()
        {
            for (int i = 0; i < Inner.Count; i++) yield return _read(Inner[i]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        int IList.Add(object? value) => throw ReadOnly();
        void IList.Clear() => throw ReadOnly();
        void IList.Insert(int index, object? value) => throw ReadOnly();
        void IList.Remove(object? value) => throw ReadOnly();
        void IList.RemoveAt(int index) => throw ReadOnly();

        private static NotSupportedException ReadOnly() =>
            new NotSupportedException("a Godot array is changed by assigning the whole list (set props[\"name\"] = [...]), not element by element");
    }
}
#endif
