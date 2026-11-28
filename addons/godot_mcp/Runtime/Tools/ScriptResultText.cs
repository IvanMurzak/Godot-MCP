/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// The editor and a game built with the in-game opt-in (GODOT_MCP_CODE_EXEC_IN_GAME, docs/ARCHITECTURE.md).
#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The plain form of a csharp-exec result, made on the main thread before the snippet unloads: values of the
    /// snippet's own types (anonymous objects, records, classes the code declares) become maps of their public members and
    /// lists of their items, so nothing of the snippet outlives it, and the tool binding presents the result with RQL
    /// (<c>Engine.Present</c>) like any tool result. Host and engine values (BCL collections, Godot structs and objects) are
    /// kept as they are. Also the type name shown before the value. Pure managed (unit-tested).
    /// </summary>
    public static class ScriptResultText
    {
        /// <summary>Most items of a snippet-typed collection kept (the rest is counted), as for a lazy sequence.</summary>
        public const int MaxItems = 1_000;

        const int MaxDepth = 4;

        /// <summary>
        /// The type shown before a value: the full name of a scalar or string (<c>System.Int32</c>, as Unity-MCP's
        /// csharp-exec prints it), the C# name of anything else (<c>List&lt;Vector3&gt;</c>, <c>anonymous</c>).
        /// </summary>
        public static string ShownTypeName(Type type) => IsScalar(type) ? type.FullName ?? type.Name : TypeName(type);

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

        /// <summary>
        /// <paramref name="value"/> with every part of a snippet type (<paramref name="isSnippetType"/>) replaced by plain data:
        /// a map → <c>Dictionary&lt;string, object?&gt;</c> of its entries, a sequence → <c>List&lt;object?&gt;</c> of its items,
        /// anything else → <c>Dictionary&lt;string, object?&gt;</c> of its public fields and properties (a member that throws
        /// reads as <c>&lt;ExceptionName&gt;</c>). Host collections holding snippet values are copied the same way; other
        /// values are returned as they are. <paramref name="special"/> may replace a value first (a freed Godot object).
        /// </summary>
        public static object? Plain(object? value, Func<Type, bool> isSnippetType, Func<object, object?>? special = null) =>
            Plainer(isSnippetType, special)(value);

        /// <summary><see cref="Plain"/> as one function for many values (the items of a sequence share one walk and its type facts).</summary>
        public static Func<object?, object?> Plainer(Func<Type, bool> isSnippetType, Func<object, object?>? special = null)
        {
            var walk = new Walk(isSnippetType, special);
            return value => walk.Value(value, 0);
        }

        sealed class Walk
        {
            readonly Func<Type, bool> _isSnippetType;
            readonly Func<object, object?>? _special;
            readonly HashSet<object> _open = new HashSet<object>(ReferenceEqualityComparer.Instance);
            // Public members of each snippet type, read once per walk (never a static cache: it would hold the snippet's types).
            readonly Dictionary<Type, (FieldInfo[] fields, PropertyInfo[] properties)> _members = new Dictionary<Type, (FieldInfo[], PropertyInfo[])>();

            public Walk(Func<Type, bool> isSnippetType, Func<object, object?>? special)
            {
                _isSnippetType = isSnippetType;
                _special = special;
            }

            public object? Value(object? value, int depth)
            {
                if (value == null) return null;
                if (_special?.Invoke(value) is { } replaced) return replaced;
                var type = value.GetType();
                if (IsScalar(type)) return value;
                var snippet = _isSnippetType(type);
                // A host collection is copied only when an item is a snippet value (shallow check: its declared item type).
                if (!snippet && !HoldsSnippetValues(type)) return value;
                if (depth >= MaxDepth || !_open.Add(value)) return Safe(value);
                try
                {
                    if (value is IDictionary map)
                    {
                        var entries = new Dictionary<string, object?>();
                        foreach (DictionaryEntry e in map)
                        {
                            if (entries.Count == MaxItems) { entries["…"] = $"{map.Count - MaxItems} more"; break; }
                            entries[Safe(e.Key)] = Value(e.Value, depth + 1);
                        }
                        return entries;
                    }
                    if (value is IEnumerable sequence && !(value is string))
                    {
                        var items = new List<object?>();
                        foreach (var item in sequence)
                        {
                            if (items.Count == MaxItems) { items.Add("… (more items)"); break; }
                            items.Add(Value(item, depth + 1));
                        }
                        return items;
                    }
                    var members = new Dictionary<string, object?>();
                    var (fields, properties) = MembersOf(type);
                    foreach (var f in fields)
                        members[f.Name] = Read(() => f.GetValue(value), depth);
                    foreach (var p in properties)
                        members[p.Name] = Read(() => p.GetValue(value), depth);
                    return members.Count > 0 ? members : Safe(value);
                }
                finally { _open.Remove(value); }
            }

            (FieldInfo[] fields, PropertyInfo[] properties) MembersOf(Type type)
            {
                if (_members.TryGetValue(type, out var known)) return known;
                var record = IsRecord(type);
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !(record && p.Name == "EqualityContract")).ToArray();
                return _members[type] = (type.GetFields(BindingFlags.Public | BindingFlags.Instance), properties);
            }

            object? Read(Func<object?> get, int depth)
            {
                object? member;
                try { member = get(); }
                catch (Exception e) { return "<" + (e is TargetInvocationException { InnerException: { } inner } ? inner : e).GetType().Name + ">"; }
                return Value(member, depth + 1);
            }

            bool HoldsSnippetValues(Type type)
            {
                if (type.IsArray) return !type.GetElementType()!.IsSealed || _isSnippetType(type.GetElementType()!);
                if (!typeof(IEnumerable).IsAssignableFrom(type) || type == typeof(string) || type.Namespace == "Godot" || type.Namespace == "Godot.Collections") return false;
                // object / interface / open-ended item types may hold snippet values; List<int>, Dictionary<string, Node> cannot.
                var args = type.IsGenericType ? type.GetGenericArguments() : new[] { typeof(object) };
                return args.Any(a => !a.IsSealed && !a.IsValueType && !typeof(global::Godot.GodotObject).IsAssignableFrom(a));
            }
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

        static bool IsRecord(Type t) => t.GetMethod("<Clone>$") != null;

        static string Safe(object value)
        {
            try { return (value is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : value.ToString()) ?? value.GetType().Name; }
            catch (Exception e) { return "<" + e.GetType().Name + ">"; }
        }
    }
}
#endif
