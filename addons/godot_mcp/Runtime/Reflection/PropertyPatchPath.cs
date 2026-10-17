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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using VariantType = Godot.Variant.Type;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// The pure-managed rules behind <see cref="GodotObjectPatcher"/>: how a patch path is resolved against
    /// a live object and which route (native Godot property vs. ReflectorNet CLR member) a top-level segment
    /// takes. No Godot native call, so every rule is unit-tested with plain CLR objects.
    /// </summary>
    public static class PropertyPatchPath
    {
        // The members ReflectorNet's TryModifyAt/TryPatch navigate by default.
        const BindingFlags ReflectorFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        static readonly string VariantTypeId = typeof(global::Godot.Variant).GetTypeId();

        /// <summary>
        /// The resolved CLR target of a path: <see cref="Path"/> with every member segment rewritten to its
        /// declared CLR name, and the declared <see cref="MemberType"/> of the terminal segment (<c>null</c>
        /// when the walk could not get there).
        /// </summary>
        public readonly record struct Resolution(string Path, Type? MemberType);

        /// <summary>
        /// The single member name a path addresses, or <c>null</c> for a nested (<c>a/b</c>) or bracket
        /// (<c>[i]</c>) path. A leading <c>#/</c> is stripped, as ReflectorNet does.
        /// </summary>
        public static string? SingleMemberSegment(string path)
        {
            var segments = Split(path);
            return segments.Length == 1 && !IsBracket(segments[0]) ? segments[0] : null;
        }

        /// <summary>
        /// Walk <paramref name="path"/> over the live <paramref name="root"/>, resolving each member segment
        /// exactly or — failing that — by its Godot snake_case spelling (<c>albedo_color</c> → <c>AlbedoColor</c>).
        /// The walk follows runtime values, so a member declared as a base type (e.g. <c>Material</c>) still
        /// resolves members of the instance it holds. Stops at the first segment it cannot resolve and
        /// returns the rest of the path unchanged, leaving ReflectorNet to report it.
        /// </summary>
        public static Resolution Resolve(object root, string path)
        {
            var segments = Split(path);
            object? current = root;
            var currentType = root.GetType();
            Type? memberType = null;

            for (var i = 0; i < segments.Length; i++)
            {
                // Only intermediate values are needed to find the next member's runtime type; the terminal
                // getter may be a native call returning a large copy.
                var last = i == segments.Length - 1;
                if (!TryStep(currentType, current, segments[i], needValue: !last, out segments[i], out memberType, out current))
                    return new Resolution(string.Join("/", segments), null);

                if (memberType == null)
                    return new Resolution(string.Join("/", segments), null);
                currentType = current?.GetType() ?? memberType;
            }

            return new Resolution(string.Join("/", segments), memberType);
        }

        /// <summary>
        /// Find a field or property on <paramref name="type"/>: the exact name (any visibility, as ReflectorNet
        /// does), else a public member whose name equals <paramref name="name"/> ignoring case and underscores
        /// (<c>albedo_color</c> → <c>AlbedoColor</c>). An exact match always wins; the most-derived declaration
        /// wins, so a <c>new</c>-hidden member never throws <see cref="AmbiguousMatchException"/>.
        /// </summary>
        public static MemberInfo? FindMember(Type type, string name)
        {
            if (MemberCache.TryGetValue((type, name), out var cached))
                return cached;
            var member = FindMemberUncached(type, name);
            // Only hits are cached: misses are caller-supplied names and would grow the cache without bound.
            if (member != null)
                MemberCache[(type, name)] = member;
            return member;
        }

        static readonly ConcurrentDictionary<(Type Type, string Name), MemberInfo> MemberCache = new();

        static MemberInfo? FindMemberUncached(Type type, string name)
        {
            MemberInfo? loose = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var field in t.GetFields(ReflectorFlags))
                {
                    if (field.Name == name)
                        return field;
                    if (loose == null && field.IsPublic && SameName(field.Name, name))
                        loose = field;
                }
                foreach (var property in t.GetProperties(ReflectorFlags))
                {
                    var exact = property.Name == name;
                    if (!exact && (loose != null || !SameName(property.Name, name) || property.GetMethod?.IsPublic != true))
                        continue;
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;
                    if (exact)
                        return property;
                    loose = property;
                }
            }
            return loose;
        }

        /// <summary>
        /// The entry of <paramref name="godotProperties"/> named <paramref name="name"/>, exactly or ignoring
        /// case and underscores (<c>LightEnergy</c> → <c>light_energy</c>); <c>null</c> when there is none.
        /// </summary>
        public static string? MatchGodotProperty(string name, IReadOnlyCollection<string> godotProperties)
            => godotProperties.Contains(name) ? name : godotProperties.FirstOrDefault(p => SameName(p, name));

        /// <summary>
        /// The Godot property backing the top-level C# member <paramref name="clrName"/> of
        /// <paramref name="targetType"/>, used to read a ReflectorNet write back: the exact Godot name, else the
        /// snake_case spelling of a public C# property (<c>LightEnergy</c> → <c>light_energy</c>). A field or
        /// non-public member never matches loosely, so a script's <c>_name</c> backing field is not read back
        /// through the unrelated <c>name</c> property.
        /// </summary>
        public static string? GodotBackingProperty(Type targetType, string clrName, IReadOnlyCollection<string> godotProperties)
        {
            if (godotProperties.Contains(clrName))
                return clrName;
            return FindMember(targetType, clrName) is PropertyInfo { GetMethod.IsPublic: true }
                ? MatchGodotProperty(clrName, godotProperties)
                : null;
        }

        /// <summary>
        /// The Godot property a top-level segment must be applied to natively, or <c>null</c> to leave it on
        /// the ReflectorNet route: a Godot property name (as get-data returns it, or a GDScript property) that
        /// is not also an exact C# member, or any member named with a <c>Godot.Variant</c> value that is not
        /// itself a Variant — ReflectorNet cannot assign a Variant to a <c>float</c>, Godot's setter can.
        /// </summary>
        public static string? GodotPropertyFor(Type targetType, string segment, bool valueIsVariant, IReadOnlyCollection<string> godotProperties)
        {
            var member = FindMember(targetType, segment);
            if (member?.Name != segment && godotProperties.Contains(segment))
                return segment;
            if (!valueIsVariant || (member != null && MemberType(member) == typeof(global::Godot.Variant)))
                return null;
            return MatchGodotProperty(segment, godotProperties);
        }

        /// <summary>
        /// Rewrite the keys of a JSON Merge Patch to the C# members they name on the live <paramref name="target"/>
        /// (snake_case → PascalCase, e.g. <c>{"position":{"x":1}}</c> → <c>{"Position":{"X":1}}</c>), at every
        /// depth, following runtime values. It descends only where the reflector merges a JSON object into the
        /// existing value — not into a <c>Resource</c>/<c>Node</c>/<c>Variant</c>, whose JSON object is the whole
        /// (reference) value. Unknown keys are left for ReflectorNet to report.
        /// </summary>
        public static void CanonicalizeMergePatch(Reflector reflector, object? target, Type type, JsonObject patch)
        {
            var entries = patch.ToList();
            var sent = new HashSet<string>(entries.Select(e => e.Key), StringComparer.Ordinal);
            patch.Clear(); // detaches the nodes so they can be re-added under their canonical names
            // Only a bracket segment addresses a collection's element; a bare key is never one of its own
            // members (Count, Keys, …), so it is left for ReflectorNet to report as it was sent.
            var isCollection = ElementType(type) != null;
            foreach (var (key, node) in entries)
            {
                var name = key;
                if (key != "$type" && (!isCollection || IsBracket(key))
                    && TryStep(type, null, key, needValue: false, out var canonical, out var childType, out _))
                {
                    // Never fold two sent keys into one: the later would silently overwrite the earlier.
                    if (canonical != key && !sent.Contains(canonical) && !patch.ContainsKey(canonical))
                        name = canonical;
                    if (node is JsonObject inner && childType != null && IsMergeTarget(reflector, childType))
                    {
                        // A nested "$type" replaces the value, so its keys name members of that type; otherwise
                        // read the live value only now: its runtime type may expose members the declared one lacks.
                        var replaced = inner["$type"] is JsonValue typeNode && typeNode.TryGetValue<string>(out var typeName)
                            ? TypeUtils.GetType(typeName)
                            : null;
                        object? child = null;
                        if (replaced == null)
                            TryStep(type, target, key, needValue: true, out _, out _, out child);
                        var nested = replaced ?? child?.GetType() ?? childType;
                        if (IsMergeTarget(reflector, nested))
                            CanonicalizeMergePatch(reflector, child, nested, inner);
                    }
                }
                patch[name] = node;
            }
        }

        /// <summary>
        /// One path segment against <paramref name="type"/>: its canonical spelling, the declared type below it,
        /// and — when <paramref name="needValue"/> — the live value. <c>false</c> for an unknown member.
        /// </summary>
        static bool TryStep(Type type, object? current, string segment, bool needValue, out string canonical, out Type? next, out object? value)
        {
            value = null;
            canonical = segment;
            if (IsBracket(segment))
            {
                next = ElementType(type);
                return true;
            }
            var member = FindMember(type, segment);
            if (member == null)
            {
                next = null;
                return false;
            }
            canonical = member.Name;
            next = MemberType(member);
            if (needValue && current != null)
                value = TryGetValue(member, current);
            return true;
        }

        /// <summary>
        /// Whether ReflectorNet merges a JSON object into a value of <paramref name="type"/> (rather than reading
        /// it as the whole value): the same converter decision TryPatch makes.
        /// </summary>
        static bool IsMergeTarget(Reflector reflector, Type type)
            => !IsNumericLike(type) && type != typeof(string)
                && reflector.Converters.GetConverter(type)?.TreatJsonObjectAsAtomicValue(type) != true;

        /// <summary>
        /// Whether Godot can store a <paramref name="value"/>-typed Variant in a <paramref name="property"/>-typed
        /// property without inventing data. Godot's own setter is looser — it turns the string <c>"abc"</c> into
        /// <c>0.0</c> for a <c>float</c> — so this is checked before writing. Untyped (<c>Nil</c>) properties take
        /// anything; numbers convert among Bool/Int/Float, text among String/StringName/NodePath, integer and
        /// float vectors/rects into each other, arrays into packed arrays, and <c>null</c> clears an object.
        /// </summary>
        public static bool IsAssignable(VariantType value, VariantType property)
        {
            if (property == VariantType.Nil || value == property)
                return true;
            if (value == VariantType.Nil)
                return property == VariantType.Object;
            return GodotVariantPayload.Family(value) is { } family && family == GodotVariantPayload.Family(property);
        }

        /// <summary>True when <paramref name="typeName"/> names <c>Godot.Variant</c>.</summary>
        public static bool IsVariantTypeName(string? typeName) => typeName == VariantTypeId;

        static bool IsNumericLike(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(decimal);

        static Type? MemberType(MemberInfo member) => member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => null,
        };

        static object? TryGetValue(MemberInfo member, object target)
        {
            try
            {
                return member switch
                {
                    FieldInfo f => f.GetValue(target),
                    PropertyInfo p => p.GetValue(target),
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }

        static Type? ElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();
            if (TypeUtils.IsDictionary(type))
                return TypeUtils.GetDictionaryGenericArguments(type)?[1];
            return typeof(IEnumerable).IsAssignableFrom(type) ? TypeUtils.GetEnumerableItemType(type) : null;
        }

        static bool SameName(string a, string b)
            => string.Equals(a.Replace("_", string.Empty), b.Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase);

        // Mirrors ReflectorNet's Reflector.TryParseBracketSegment (5.4.3): '[]' is a valid (empty) key.
        static bool IsBracket(string segment) => segment.Length >= 2 && segment[0] == '[' && segment[segment.Length - 1] == ']';

        // Mirrors ReflectorNet's private Reflector.ParsePath (5.4.3, Reflector.ModifyAt.cs): a '/' inside a bracket
        // segment ('[a/b]') belongs to the key, and '\]' / '\\' escapes stay intact until the key is decoded.
        static string[] Split(string path)
        {
            if (path.StartsWith("#/", StringComparison.Ordinal))
                path = path.Substring(2);
            else if (path.StartsWith("#", StringComparison.Ordinal))
                path = path.Substring(1);

            var segments = new List<string>();
            var start = 0;
            var bracketed = false;
            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                if (bracketed && c == '\\' && i + 1 < path.Length && (path[i + 1] == ']' || path[i + 1] == '\\'))
                {
                    i++;
                    continue;
                }
                if (i == start && c == '[')
                    bracketed = true;
                else if (bracketed && c == ']')
                    bracketed = false;
                else if (!bracketed && c == '/')
                {
                    if (i > start)
                        segments.Add(path.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < path.Length)
                segments.Add(path.Substring(start));
            return segments.ToArray();
        }
    }
}
