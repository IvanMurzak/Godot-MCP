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
using System.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using VariantType = Godot.Variant.Type;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// The pure-managed rules behind <see cref="GodotObjectPatcher"/>: how a patch path is resolved against
    /// a live object, which route (native Godot property vs. ReflectorNet CLR member) a top-level segment
    /// takes, and how a mismatched primitive <c>typeName</c> is aligned. No Godot native call, so every rule
    /// is unit-tested with plain CLR objects.
    /// </summary>
    public static class PropertyPatchPath
    {
        /// <summary>The binding flags ReflectorNet's <c>TryModifyAt</c>/<c>TryPatch</c> navigate with by default.</summary>
        public const BindingFlags ReflectorFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly string VariantTypeId = typeof(global::Godot.Variant).GetTypeId();

        /// <summary>
        /// The resolved CLR target of a path: <see cref="Path"/> with every member segment rewritten to its
        /// declared CLR name, the declared <see cref="MemberType"/> of the terminal segment (<c>null</c> when
        /// the walk could not get there), and the <see cref="TopLevelMember"/> when the path is one member.
        /// </summary>
        public sealed class Resolution
        {
            public string Path { get; init; } = string.Empty;
            public Type? MemberType { get; init; }
            public MemberInfo? TopLevelMember { get; init; }
        }

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
            MemberInfo? topLevel = null;

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (IsBracket(segment))
                {
                    memberType = ElementType(currentType);
                    current = null;
                }
                else
                {
                    var member = FindMember(currentType, segment);
                    if (member == null)
                        return new Resolution { Path = string.Join("/", segments) };

                    segments[i] = member.Name;
                    memberType = MemberType(member);
                    if (i == 0 && segments.Length == 1)
                        topLevel = member;
                    current = current == null ? null : TryGetValue(member, current);
                }

                if (memberType == null)
                    return new Resolution { Path = string.Join("/", segments) };
                currentType = current?.GetType() ?? memberType;
            }

            return new Resolution { Path = string.Join("/", segments), MemberType = memberType, TopLevelMember = topLevel };
        }

        /// <summary>
        /// Find a field or property on <paramref name="type"/>: the exact name first (any visibility, as
        /// ReflectorNet does), then — unless <paramref name="exactOnly"/> — a public member whose name equals
        /// <paramref name="name"/> ignoring case and underscores. The most-derived declaration wins, so a
        /// <c>new</c>-hidden member never throws <see cref="AmbiguousMatchException"/>.
        /// </summary>
        public static MemberInfo? FindMember(Type type, string name, bool exactOnly = false)
        {
            var normalized = Normalize(name);
            MemberInfo? loose = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var member in t.GetMembers(ReflectorFlags | BindingFlags.DeclaredOnly))
                {
                    if (member is not (FieldInfo or PropertyInfo) || member is PropertyInfo { CanRead: false })
                        continue;
                    if (member is PropertyInfo p && p.GetIndexParameters().Length != 0)
                        continue;
                    if (member.Name == name)
                        return member;
                    if (!exactOnly && loose == null && IsPublic(member) && Normalize(member.Name) == normalized)
                        loose = member;
                }
            }
            return loose;
        }

        /// <summary>
        /// The Godot property a top-level segment must be applied to natively, or <c>null</c> to leave it on
        /// the ReflectorNet CLR route. Native when the segment is a Godot property name (what
        /// <c>resource-get-data</c> returns, or a GDScript property) that is not also an exact CLR member, or
        /// when the value is a <c>Godot.Variant</c> envelope aimed at a CLR member that is not itself a
        /// Variant — the CLR route cannot assign a Variant to a <c>float</c>, Godot's own setter can.
        /// </summary>
        public static string? GodotPropertyFor(Type targetType, string segment, bool valueIsVariant, IReadOnlyCollection<string> godotProperties)
        {
            var exact = FindMember(targetType, segment, exactOnly: true);
            if (exact == null && godotProperties.Contains(segment))
                return segment;

            if (!valueIsVariant)
                return null;
            var member = exact ?? FindMember(targetType, segment);
            if (member != null && MemberType(member) == typeof(global::Godot.Variant))
                return null;

            if (godotProperties.Contains(segment))
                return segment;
            var normalized = Normalize(segment);
            return godotProperties.FirstOrDefault(p => Normalize(p) == normalized);
        }

        /// <summary>
        /// Rewrite <paramref name="value"/>'s <c>typeName</c> to the declared primitive/enum type of the target
        /// when the caller sent a different numeric one (e.g. <c>System.Double</c> for a <c>float</c>).
        /// ReflectorNet picks its converter from <c>typeName</c>; a mismatch deserializes to the wrong CLR type
        /// and the setter rejects it. Non-numeric type names are never touched. Returns <c>true</c> when
        /// the type name was changed.
        /// </summary>
        public static bool AlignPrimitiveTypeName(SerializedMember value, Type declaredType)
        {
            var target = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
            if (!IsNumericLike(target))
                return false;

            var expected = target.GetTypeId();
            if (value.typeName == expected)
                return false;

            var given = TypeUtils.GetType(value.typeName);
            if (given == null || !IsNumericLike(Nullable.GetUnderlyingType(given) ?? given))
                return false;

            value.typeName = expected;
            return true;
        }

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
            return Family(value) is { } family && family == Family(property);
        }

        static int? Family(VariantType type) => type switch
        {
            VariantType.Bool or VariantType.Int or VariantType.Float => 1,
            VariantType.String or VariantType.StringName or VariantType.NodePath => 2,
            VariantType.Vector2 or VariantType.Vector2I => 3,
            VariantType.Vector3 or VariantType.Vector3I => 4,
            VariantType.Vector4 or VariantType.Vector4I => 5,
            VariantType.Rect2 or VariantType.Rect2I => 6,
            VariantType.Array or VariantType.PackedByteArray or VariantType.PackedInt32Array or VariantType.PackedInt64Array
                or VariantType.PackedFloat32Array or VariantType.PackedFloat64Array or VariantType.PackedStringArray
                or VariantType.PackedVector2Array or VariantType.PackedVector3Array or VariantType.PackedColorArray
                or VariantType.PackedVector4Array => 7,
            _ => null,
        };

        /// <summary>True when <paramref name="typeName"/> names <c>Godot.Variant</c>.</summary>
        public static bool IsVariantTypeName(string? typeName) => typeName == VariantTypeId;

        static bool IsNumericLike(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(decimal);

        static bool IsPublic(MemberInfo member) => member switch
        {
            FieldInfo f => f.IsPublic,
            PropertyInfo p => p.GetMethod?.IsPublic == true,
            _ => false,
        };

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

        static string Normalize(string name) => name.Replace("_", string.Empty).ToLowerInvariant();

        static bool IsBracket(string segment) => segment.Length > 2 && segment[0] == '[' && segment[segment.Length - 1] == ']';

        static string[] Split(string path)
        {
            if (path.StartsWith("#/", StringComparison.Ordinal))
                path = path.Substring(2);
            else if (path.StartsWith("#", StringComparison.Ordinal))
                path = path.Substring(1);
            return path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
