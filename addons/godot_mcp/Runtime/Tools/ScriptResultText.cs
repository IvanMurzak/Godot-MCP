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

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The compact text of a script-execute result in the RQL tool format, the vocabulary rql-query answers in:
    /// <c>Dictionary&lt;string, int&gt;: {a: 1, b: 2}</c>, <c>List&lt;Vector3&gt;: [(1, 2, 3)]</c>,
    /// <c>anonymous: {name: x, count: 3}</c>, a node as <c>Node3D#123(Geometry/Floor)</c>. ReflectorNet's form printed
    /// a dictionary's internals instead of its entries, a list as name/typeName/value tables and a node with the editor's
    /// own absolute path. Engine values (nodes, resources, Godot structs) are rendered by the caller's
    /// <c>special</c> function. Pure managed: no Godot types (unit-tested).
    /// </summary>
    public static class ScriptResultText
    {
        /// <summary>Most items of a collection that are listed (the rest is counted).</summary>
        public const int MaxItems = 100;

        const int MaxDepth = 4;

        /// <summary>
        /// "Type: value" for a value that is not a plain scalar, or null for a scalar / string (those keep the existing
        /// "System.Int32: 42" form). <paramref name="special"/> returns the text of an engine value, or null when it does not
        /// handle it.
        /// </summary>
        public static string? Format(object? value, Func<object, string?> special)
        {
            if (value == null || IsScalar(value.GetType())) return null;
            return TypeName(value.GetType()) + ": " + Value(value, special, 0, quoteStrings: true);
        }

        /// <summary>The value's text: collections as [..], maps and objects as {k: v}, tuples as (a, b).</summary>
        public static string Value(object? value, Func<object, string?> special, int depth, bool quoteStrings)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return quoteStrings ? Quote(s) : s;
                case bool b: return b ? "true" : "false";
                case char c: return quoteStrings ? Quote(c.ToString()) : c.ToString();
                case Enum e: return e.ToString();
                case IFormattable f when IsNumber(value.GetType()): return f.ToString(null, CultureInfo.InvariantCulture);
            }
            if (special(value) is { } text) return text;
            if (depth >= MaxDepth) return "…";
            var type = value.GetType();
            if (value is IDictionary map)
                return "{" + Join(Entries(map).Select(kv => Key(kv.Key, special) + ": " + Value(kv.Value, special, depth + 1, true)), map.Count) + "}";
            if (value is ITuple tuple)
                return "(" + string.Join(", ", Enumerable.Range(0, tuple.Length).Select(i => Value(tuple[i], special, depth + 1, true))) + ")";
            if (value is IEnumerable items)
            {
                // A generic-only map (IDictionary<K, V>, e.g. Godot.Collections.Dictionary) enumerates KeyValuePairs.
                var isMap = type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                                                                             || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
                var list = new List<string>();
                var count = 0;
                foreach (var item in items)
                {
                    if (count++ < MaxItems)
                        list.Add(isMap && item != null && KeyValue(item) is { } kv
                            ? Key(kv.key ?? "null", special) + ": " + Value(kv.value, special, depth + 1, true)
                            : Value(item, special, depth + 1, true));
                    if (count > MaxItems * 100) break; // a huge or endless sequence: stop counting
                }
                return isMap ? "{" + Join(list, count) + "}" : "[" + Join(list, count) + "]";
            }
            var members = Members(type).ToList();
            if (members.Count == 0) return value.ToString() ?? type.Name;
            return "{" + string.Join(", ", members.Select(m => m.Name + ": " + Value(Read(m, value), special, depth + 1, true))) + "}";
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

        static readonly Dictionary<Type, string> Aliases = new Dictionary<Type, string>
        {
            [typeof(object)] = "object", [typeof(string)] = "string", [typeof(bool)] = "bool", [typeof(int)] = "int",
            [typeof(long)] = "long", [typeof(float)] = "float", [typeof(double)] = "double", [typeof(decimal)] = "decimal",
            [typeof(byte)] = "byte", [typeof(short)] = "short", [typeof(uint)] = "uint", [typeof(ulong)] = "ulong", [typeof(char)] = "char",
        };

        static bool IsScalar(Type t) => t == typeof(string) || t == typeof(bool) || t == typeof(char) || t.IsEnum || IsNumber(t);

        static bool IsNumber(Type t) => t.IsPrimitive && t != typeof(bool) && t != typeof(char) || t == typeof(decimal);

        static bool IsAnonymous(Type t) =>
            t.Name.Contains("AnonymousType", StringComparison.Ordinal) && t.IsDefined(typeof(CompilerGeneratedAttribute), false);

        static IEnumerable<KeyValuePair<object, object?>> Entries(IDictionary map)
        {
            var i = 0;
            foreach (DictionaryEntry e in map)
            {
                if (i++ >= MaxItems) yield break;
                yield return new KeyValuePair<object, object?>(e.Key, e.Value);
            }
        }

        static string Join(IEnumerable<string> shown, int total)
        {
            var list = shown.ToList();
            var text = string.Join(", ", list);
            return total > list.Count ? text + $", … ({total} items)" : text;
        }

        // A key reads bare unless it could be confused with the separators.
        static string Key(object key, Func<object, string?> special)
        {
            var text = Value(key, special, MaxDepth - 1, quoteStrings: false);
            return text.Length == 0 || text.IndexOfAny(new[] { ',', '{', '}', '[', ']', '"' }) >= 0 || text.Contains(": ", StringComparison.Ordinal)
                ? Quote(text) : text;
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

        // Public instance properties without parameters, then public fields (records, anonymous types, plain classes).
        static IEnumerable<MemberInfo> Members(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                                                                                    && p.Name != "EqualityContract")
                .Cast<MemberInfo>()
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance));

        // The Key and Value of a KeyValuePair<K, V> (any K, V), or null for another item.
        static (object? key, object? value)? KeyValue(object item)
        {
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
