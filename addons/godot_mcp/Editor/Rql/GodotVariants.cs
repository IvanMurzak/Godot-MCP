/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Variant ⇄ CLR value conversion for the <c>props</c> map and <c>CallMethod</c>: Godot properties (including GDScript
    /// script variables and GDExtension properties, which have no C# member) are read with <see cref="GodotObject.Get"/>
    /// and written with <see cref="GodotObject.Set"/>, so RQL sees them as typed CLR values.
    /// </summary>
    internal static class GodotVariants
    {
        /// <summary>The CLR value of a Variant (null for Nil).</summary>
        public static object? ToClr(Variant v) => v.VariantType == Variant.Type.Nil ? null : v.Obj;

        /// <summary>A Variant holding <paramref name="value"/>; RQL lists / object literals become Godot arrays / dictionaries.</summary>
        public static Variant ToVariant(object? value)
        {
            switch (value)
            {
                case null: return default;
                case Variant v: return v;
                case bool b: return b;
                case string s: return s;
                case int i: return i;
                case long l: return l;
                case double d: return d;
                case float f: return f;
                case byte or sbyte or short or ushort or uint: return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case ulong ul: return unchecked((long)ul);
                case decimal m: return (double)m;
                case Enum e: return Convert.ToInt64(e, CultureInfo.InvariantCulture);
                case Vector2 x: return x;
                case Vector2I x: return x;
                case Vector3 x: return x;
                case Vector3I x: return x;
                case Vector4 x: return x;
                case Vector4I x: return x;
                case Rect2 x: return x;
                case Rect2I x: return x;
                case Transform2D x: return x;
                case Plane x: return x;
                case Quaternion x: return x;
                case Aabb x: return x;
                case Basis x: return x;
                case Transform3D x: return x;
                case Projection x: return x;
                case Color x: return x;
                case Rid x: return x;
                case StringName x: return x;
                case NodePath x: return x;
                case GodotObject x: return x;
                case global::Godot.Collections.Array x: return x;
                case global::Godot.Collections.Dictionary x: return x;
                case byte[] x: return x;
                case int[] x: return x;
                case long[] x: return x;
                case float[] x: return x;
                case double[] x: return x;
                case string[] x: return x;
                case Vector2[] x: return x;
                case Vector3[] x: return x;
                case Color[] x: return x;
                case IDictionary map:
                {
                    var dictionary = new global::Godot.Collections.Dictionary();
                    foreach (DictionaryEntry entry in map) dictionary[ToVariant(entry.Key)] = ToVariant(entry.Value);
                    return dictionary;
                }
                case IEnumerable list:
                {
                    var array = new global::Godot.Collections.Array();
                    foreach (var item in list) array.Add(ToVariant(item));
                    return array;
                }
                default:
                    throw new ArgumentException($"a {value.GetType().Name} cannot be stored in a Godot property");
            }
        }

        /// <summary>
        /// The CLR type RQL coerces a value to before it is written to a Godot property of <paramref name="type"/>
        /// (<paramref name="hint"/> / <paramref name="hintString"/> narrow object properties to Resource / Node).
        /// </summary>
        public static Type ClrType(Variant.Type type, PropertyHint hint = PropertyHint.None, string? hintString = null) => type switch
        {
            Variant.Type.Bool => typeof(bool),
            Variant.Type.Int => typeof(long),
            Variant.Type.Float => typeof(double),
            Variant.Type.String => typeof(string),
            Variant.Type.Vector2 => typeof(Vector2),
            Variant.Type.Vector2I => typeof(Vector2I),
            Variant.Type.Rect2 => typeof(Rect2),
            Variant.Type.Rect2I => typeof(Rect2I),
            Variant.Type.Vector3 => typeof(Vector3),
            Variant.Type.Vector3I => typeof(Vector3I),
            Variant.Type.Transform2D => typeof(Transform2D),
            Variant.Type.Vector4 => typeof(Vector4),
            Variant.Type.Vector4I => typeof(Vector4I),
            Variant.Type.Plane => typeof(Plane),
            Variant.Type.Quaternion => typeof(Quaternion),
            Variant.Type.Aabb => typeof(Aabb),
            Variant.Type.Basis => typeof(Basis),
            Variant.Type.Transform3D => typeof(Transform3D),
            Variant.Type.Projection => typeof(Projection),
            Variant.Type.Color => typeof(Color),
            Variant.Type.StringName => typeof(StringName),
            Variant.Type.NodePath => typeof(NodePath),
            Variant.Type.Rid => typeof(Rid),
            Variant.Type.Object => hint == PropertyHint.ResourceType ? typeof(Resource)
                : hint == PropertyHint.NodeType ? typeof(Node) : typeof(GodotObject),
            Variant.Type.Dictionary => typeof(global::Godot.Collections.Dictionary),
            Variant.Type.Array => typeof(global::Godot.Collections.Array),
            Variant.Type.PackedByteArray => typeof(byte[]),
            Variant.Type.PackedInt32Array => typeof(int[]),
            Variant.Type.PackedInt64Array => typeof(long[]),
            Variant.Type.PackedFloat32Array => typeof(float[]),
            Variant.Type.PackedFloat64Array => typeof(double[]),
            Variant.Type.PackedStringArray => typeof(string[]),
            Variant.Type.PackedVector2Array => typeof(Vector2[]),
            Variant.Type.PackedVector3Array => typeof(Vector3[]),
            Variant.Type.PackedColorArray => typeof(Color[]),
            _ => typeof(object), // Nil (a Variant property), Callable, Signal, Packed types newer than 4.3
        };

        /// <summary>The Godot properties of an object that the <c>props</c> map exposes: stored or shown in the inspector.</summary>
        public static IEnumerable<global::Godot.Collections.Dictionary> Properties(GodotObject o)
        {
            foreach (var info in o.GetPropertyList())
            {
                var usage = (PropertyUsageFlags)info["usage"].AsInt64();
                if ((usage & (PropertyUsageFlags.Category | PropertyUsageFlags.Group | PropertyUsageFlags.Subgroup)) != 0) continue;
                if ((usage & (PropertyUsageFlags.Storage | PropertyUsageFlags.Editor)) == 0) continue;
                if (info["name"].AsString().Length == 0) continue;
                yield return info;
            }
        }

        /// <summary>The property description of <paramref name="name"/> on <paramref name="o"/>, or null.</summary>
        public static global::Godot.Collections.Dictionary? Property(GodotObject o, string name)
        {
            foreach (var info in Properties(o))
                if (info["name"].AsString() == name) return info;
            return null;
        }
    }
}
#endif
