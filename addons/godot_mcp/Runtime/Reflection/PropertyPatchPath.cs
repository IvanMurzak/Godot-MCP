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
                var segment = segments[i];
                var last = i == segments.Length - 1;
                if (IsBracket(segment))
                {
                    memberType = ElementType(currentType);
                    current = null;
                }
                else
                {
                    var member = FindMember(currentType, segment);
                    if (member == null)
                        return new Resolution(string.Join("/", segments), null);

                    segments[i] = member.Name;
                    memberType = MemberType(member);
                    // Only intermediate values are needed to find the next member's runtime type; the terminal
                    // getter may be a native call returning a large copy.
                    current = last || current == null ? null : TryGetValue(member, current);
                }

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

        static bool IsBracket(string segment) => segment.Length > 2 && segment[0] == '[' && segment[segment.Length - 1] == ']';

        // Mirrors ReflectorNet's private Reflector.ParsePath / TryParseBracketSegment (Reflector.ModifyAt.cs).
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
