/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// script-execute: the editor, a game built with the in-game opt-in (docs/ARCHITECTURE.md), and the xUnit suite.
#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The compact text of a script-execute result in the RQL tool format, the vocabulary rql-query answers in:
    /// <c>Dictionary&lt;string, int&gt;: {a: 1, b: 2}</c>, <c>List&lt;Vector3&gt;: [(1, 2, 3)]</c>,
    /// <c>anonymous: {name: x, count: 3}</c>, a node as <c>Node3D#123(Geometry/Floor)</c>. ReflectorNet's form printed
    /// a dictionary's internals instead of its entries, a list as name/typeName/value tables and a node with the editor's
    /// own absolute path. Engine values (nodes, resources, Godot structs) are rendered by the caller's <c>special</c>
    /// function and engine wrappers (a Variant) unwrapped by its <c>unwrap</c> function. Pure managed: no Godot types
    /// (unit-tested).
    /// </summary>
    public static class ScriptResultText
    {
        /// <summary>Most items of a collection that are listed (the rest is counted).</summary>
        public const int MaxItems = 100;

        /// <summary>Most values rendered for one result; past it every value reads "…" (the text is clipped later anyway).</summary>
        public const int MaxValues = 5_000;

        const int MaxDepth = 4;
        const int MaxCounted = MaxItems * 100;

        /// <summary>
        /// "Type: value" for a value that is not a plain scalar, or null for a scalar / string (those keep the existing
        /// "System.Int32: 42" form). <paramref name="special"/> returns the text of an engine value, or null when it does not
        /// handle it; <paramref name="unwrap"/> returns the value an engine wrapper holds (or the value itself).
        /// <paramref name="typeName"/> replaces the value's own type name (a lazy sequence the caller enumerated).
        /// </summary>
        public static string? Format(object? value, Func<object, string?> special, Func<object, object?>? unwrap = null, string? typeName = null)
        {
            if (value == null || IsScalar(value.GetType())) return null;
            var walk = new Walk(special, unwrap ?? (o => o));
            return (typeName ?? TypeName(value.GetType())) + ": " + walk.Value(value, 0);
        }

        /// <summary>A C#-style type name: <c>List&lt;Vector3&gt;</c>, <c>Dictionary&lt;string, int&gt;</c>, <c>int[]</c>, <c>anonymous</c>.</summary>
        public static string TypeName(Type type)
        {
            if (IsAnonymous(type)) return "anonymous";
            if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
            if (Aliases.TryGetValue(type, out var alias)) return alias;
            if (typeof(ITuple).IsAssignableFrom(type) && type.IsGenericType)
                return "(" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ")";
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return type.IsGenericType ? name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">" : name;
        }

        /// <summary>One result's rendering: the caller's hooks, the remaining value budget and per-type facts.</summary>
        sealed class Walk
        {
            readonly Func<object, string?> _special;
            readonly Func<object, object?> _unwrap;
            readonly Dictionary<Type, TypeFacts> _facts = new Dictionary<Type, TypeFacts>();
            int _budget = MaxValues;

            public Walk(Func<object, string?> special, Func<object, object?> unwrap)
            {
                _special = special;
                _unwrap = unwrap;
            }

            /// <summary>The value's text: collections as [..], maps and objects as {k: v}, tuples as (a, b).</summary>
            public string Value(object? value, int depth)
            {
                if (value != null) value = _unwrap(value);
                switch (value)
                {
                    case null: return "null";
                    case string s: return Quote(s);
                    case bool b: return b ? "true" : "false";
                    case char c: return Quote(c.ToString());
                    case Enum e: return e.ToString();
                    case IFormattable f when IsNumber(value.GetType()): return f.ToString(null, CultureInfo.InvariantCulture);
                }
                if (_special(value) is { } text) return text;
                if (depth >= MaxDepth || --_budget < 0) return "…";
                var type = value.GetType();
                var facts = Facts(type);
                if (facts.Leaf) return Safe(value);
                if (value is ITuple tuple)
                    return "(" + string.Join(", ", Enumerable.Range(0, tuple.Length).Select(i => Value(tuple[i], depth + 1))) + ")";
                if (value is IEnumerable items)
                    return Items(value, items, facts, depth);
                if (facts.Members.Length == 0) return Safe(value);
                return "{" + string.Join(", ", facts.Members.Select(m => m.Name + ": " + Value(Read(m, value), depth + 1))) + "}";
            }

            string Items(object value, IEnumerable items, TypeFacts facts, int depth)
            {
                var known = value is ICollection collection ? collection.Count : facts.Count?.GetValue(value) as int?;
                var list = new List<string>();
                var count = 0;
                foreach (var item in items)
                {
                    if (count++ >= MaxItems)
                    {
                        // The size is known: no need to walk the rest; a huge or endless sequence: stop counting.
                        if (known != null || count > MaxCounted) break;
                        continue;
                    }
                    list.Add(facts.IsMap && item != null && KeyValue(item) is { } kv
                        ? Key(kv.key) + ": " + Value(kv.value, depth + 1)
                        : Value(item, depth + 1));
                }
                var shown = string.Join(", ", list);
                if (known != null ? known > list.Count : count > list.Count)
                    shown += known != null ? $", … ({known} items)" : count > MaxCounted ? $", … ({MaxCounted}+ items)" : $", … ({count} items)";
                return facts.IsMap ? "{" + shown + "}" : "[" + shown + "]";
            }

            // A key reads bare unless it could be confused with the separators.
            string Key(object? key)
            {
                if (key != null) key = _unwrap(key);
                var text = key is string or char ? key.ToString()! : Value(key, MaxDepth - 1);
                return text.Length == 0 || text.IndexOfAny(KeySeparators) >= 0 || text.Contains(": ", StringComparison.Ordinal)
                    ? Quote(text) : text;
            }

            TypeFacts Facts(Type type)
            {
                if (!_facts.TryGetValue(type, out var facts))
                    _facts[type] = facts = new TypeFacts(type);
                return facts;
            }
        }

        /// <summary>What the walk needs to know about a type, looked up once per result (never cached across results: the
        /// snippet's types must not outlive its collectible load context).</summary>
        sealed class TypeFacts
        {
            public readonly bool Leaf;
            public readonly bool IsMap;
            public readonly PropertyInfo? Count;
            public readonly MemberInfo[] Members;

            public TypeFacts(Type type)
            {
                var interfaces = type.GetInterfaces();
                Leaf = IsLeaf(type);
                IsMap = typeof(IDictionary).IsAssignableFrom(type) || interfaces.Any(i => i.IsGenericType
                    && (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
                // A collection that is only a generic one (HashSet<T>, Godot.Collections.Array<T> …) still has a size.
                Count = typeof(ICollection).IsAssignableFrom(type) ? null : interfaces
                    .Where(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)))
                    .Select(i => i.GetProperty("Count")).FirstOrDefault(p => p != null);
                Members = Leaf || typeof(IEnumerable).IsAssignableFrom(type) ? Array.Empty<MemberInfo>() : ReadMembers(type);
            }

            // Values read by their text: reflection objects, delegates, tasks, lazies and exceptions (their members lead far
            // away or run code: Task.Result blocks, Lazy.Value runs its factory), and any type with its own ToString
            // (DateTime, TimeSpan, Version, Uri …) except anonymous types and records, whose ToString lists the members.
            static bool IsLeaf(Type type) =>
                typeof(MemberInfo).IsAssignableFrom(type) || typeof(Assembly).IsAssignableFrom(type) || typeof(Module).IsAssignableFrom(type)
                || typeof(Delegate).IsAssignableFrom(type) || typeof(Task).IsAssignableFrom(type) || typeof(Exception).IsAssignableFrom(type)
                || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Lazy<>)
                || OwnToString(type) && !IsAnonymous(type) && type.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance) == null
                   && !typeof(IEnumerable).IsAssignableFrom(type) && !typeof(ITuple).IsAssignableFrom(type);

            static bool OwnToString(Type type)
            {
                var declaring = type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType;
                return declaring != null && declaring != typeof(object) && declaring != typeof(ValueType);
            }

            // Public instance properties without parameters, then public fields (records, anonymous types, plain classes).
            static MemberInfo[] ReadMembers(Type type) =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
                    .Cast<MemberInfo>()
                    .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    .ToArray();
        }

        static readonly Dictionary<Type, string> Aliases = new Dictionary<Type, string>
        {
            [typeof(object)] = "object", [typeof(string)] = "string", [typeof(bool)] = "bool", [typeof(int)] = "int",
            [typeof(long)] = "long", [typeof(float)] = "float", [typeof(double)] = "double", [typeof(decimal)] = "decimal",
            [typeof(byte)] = "byte", [typeof(short)] = "short", [typeof(uint)] = "uint", [typeof(ulong)] = "ulong", [typeof(char)] = "char",
        };

        static readonly char[] KeySeparators = { ',', '{', '}', '[', ']', '"' };

        static bool IsScalar(Type t) => t == typeof(string) || t == typeof(bool) || t == typeof(char) || t.IsEnum || IsNumber(t);

        static bool IsNumber(Type t) => t.IsPrimitive && t != typeof(bool) && t != typeof(char) || t == typeof(decimal);

        static bool IsAnonymous(Type t) =>
            t.Name.Contains("AnonymousType", StringComparison.Ordinal) && t.IsDefined(typeof(CompilerGeneratedAttribute), false);

        static string Safe(object value)
        {
            try { return (value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value.ToString()) ?? value.GetType().Name; }
            catch (Exception e) { return "<" + e.GetType().Name + ">"; }
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }

        // The Key and Value of a DictionaryEntry or a KeyValuePair<K, V> (any K, V), or null for another item.
        static (object? key, object? value)? KeyValue(object item)
        {
            if (item is DictionaryEntry e) return (e.Key, e.Value);
            var t = item.GetType();
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(KeyValuePair<,>)) return null;
            return (t.GetProperty("Key")!.GetValue(item), t.GetProperty("Value")!.GetValue(item));
        }

        static object? Read(MemberInfo m, object o)
        {
            try { return m is PropertyInfo p ? p.GetValue(o) : ((FieldInfo)m).GetValue(o); }
            catch (Exception e) { return "<" + (e.InnerException ?? e).GetType().Name + ">"; }
        }
    }
}
#endif
