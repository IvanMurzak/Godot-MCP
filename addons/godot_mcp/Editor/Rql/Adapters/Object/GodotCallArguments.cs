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
using System.Linq;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// The argument list of <c>CallMethod(name, [args])</c>: the RQL values as written, plus the kernel that coerced the list,
    /// so each value can then be converted like a <c>set</c> would to the type the Godot method declares for it
    /// (<c>"(0, 5)"</c> → Vector2i, <c>"res://…"</c> → the resource, an enum name → its value). Passing the raw values made
    /// Godot reject or silently zero them ("Cannot convert argument … from String to Object") while the call read as success.
    /// </summary>
    public sealed class ArgumentList
    {
        internal ArgumentList(IReadOnlyList<object?> values, RqlKernel kernel) { Values = values; Kernel = kernel; }
        internal IReadOnlyList<object?> Values { get; }
        internal RqlKernel Kernel { get; }
    }

    /// <summary>Takes any RQL list as an <see cref="ArgumentList"/>.</summary>
    public sealed class Godot_ArgumentList_Adapter : RqlTypeAdapter<ArgumentList>
    {
        public override string Description => "a list of method arguments, e.g. [\"(0, 5)\", 0, \"res://icon.svg\"]";

        protected override void Members(MemberTable<ArgumentList> m) { }

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            reason = null;
            result = null;
            if (value is string || !(value is IEnumerable items)) { reason = "expected a list of arguments, e.g. [1, \"(0, 5)\"]"; return AdapterOutcome.Failed; }
            result = new ArgumentList(items.Cast<object?>().ToList(), kernel);
            return AdapterOutcome.Ok;
        }
    }

    /// <summary>Converting <see cref="ArgumentList"/> values to the argument types of a Godot method.</summary>
    internal static class GodotCallArguments
    {
        /// <summary>
        /// The Variants to call <paramref name="method"/> of <paramref name="o"/> with, each converted to the type the method
        /// declares; throws <see cref="ArgumentException"/> naming the argument that does not convert. Vararg extras and
        /// untyped (Variant) arguments pass as given.
        /// </summary>
        public static Variant[] Convert(GodotObject o, string method, ArgumentList? args)
        {
            var values = args?.Values ?? Array.Empty<object?>();
            var declared = Declared(o, method);
            var result = new Variant[values.Count];
            for (var i = 0; i < values.Count; i++)
                result[i] = declared != null && i < declared.Count
                    ? ConvertOne(values[i], declared[i], args!.Kernel, method, i)
                    : GodotVariants.ToVariant(values[i]);
            return result;
        }

        private static global::Godot.Collections.Array<global::Godot.Collections.Dictionary>? Declared(GodotObject o, string method)
        {
            foreach (var entry in o.GetMethodList())
                if (entry["name"].AsString() == method)
                    return entry["args"].AsGodotArray<global::Godot.Collections.Dictionary>();
            return null;
        }

        private static Variant ConvertOne(object? value, global::Godot.Collections.Dictionary arg, RqlKernel kernel, string method, int index)
        {
            var type = (Variant.Type)arg["type"].AsInt32();
            var hint = (PropertyHint)arg["hint"].AsInt32();
            var hintString = arg["hint_string"].AsString();
            var className = arg["class_name"].AsString();
            var label = $"{method} argument {index + 1} ({arg["name"].AsString()}: {(className.Length > 0 ? className : type.ToString())})";
            if (type == Variant.Type.Nil) return GodotVariants.ToVariant(value);
            if (type == Variant.Type.Int && value is string text && EnumOptions(className, hint, hintString) is { Count: > 0 } options)
                return GodotEnumNames.TryMatch(options, text, out var enumValue)
                    ? Variant.From(enumValue)
                    : throw new ArgumentException($"{label}: \"{text}\" is not {GodotEnumNames.Describe(options)}");
            var target = type == Variant.Type.Object ? ObjectType(className) : GodotVariants.ClrType(type, hint, hintString);
            if (!kernel.TryCoerce(value, target, out var converted, out var why))
            {
                // Godot arrays / dictionaries take any list / object literal (the elements are checked by Godot).
                if (type == Variant.Type.Array || type == Variant.Type.Dictionary) return GodotVariants.ToVariant(value);
                throw new ArgumentException($"{label}: {why}");
            }
            if (converted is GodotObject go && className.Length > 0 && ClassDB.ClassExists(className) && !go.IsClass(className))
                throw new ArgumentException($"{label}: got a {go.GetClass()}");
            return GodotVariants.ToVariant(converted);
        }

        /// <summary>
        /// The names an enum-hinted int property takes: its hint labels ("Top,Bottom,Both") and, when its setter's argument is a
        /// class enum, that enum's constants (GROW_DIRECTION_BEGIN …), which can differ from the labels.
        /// </summary>
        public static List<(string name, long value)> PropertyEnumOptions(GodotObject o, string property, string hintString)
        {
            var options = GodotEnumNames.ParseHint(hintString);
            // ClassDB.class_get_property_setter is Godot 4.4+ (the addon's floor is 4.3): called by name when it exists.
            var classDb = Engine.GetSingleton("ClassDB");
            if (classDb == null || !classDb.HasMethod("class_get_property_setter")) return options;
            var setter = classDb.Call("class_get_property_setter", o.GetClass(), property).AsString();
            if (setter.Length == 0) return options;
            foreach (var entry in o.GetMethodList())
            {
                if (entry["name"].AsString() != setter) continue;
                var args = entry["args"].AsGodotArray<global::Godot.Collections.Dictionary>();
                if (args.Count > 0 && EnumOptions(args[0]["class_name"].AsString(), PropertyHint.None, "") is { } constants) options.AddRange(constants);
                break;
            }
            return options;
        }

        // A class enum ("Control.LayoutPreset") or an enum hint ("Begin,End,Both"): its names and values.
        private static List<(string name, long value)>? EnumOptions(string className, PropertyHint hint, string hintString)
        {
            var dot = className.LastIndexOf('.');
            if (dot > 0 && ClassDB.ClassExists(className.Substring(0, dot)))
            {
                var owner = className.Substring(0, dot);
                return ClassDB.ClassGetEnumConstants(owner, className.Substring(dot + 1))
                    .Select(c => (c, ClassDB.ClassGetIntegerConstant(owner, c))).ToList();
            }
            return hint == PropertyHint.Enum ? GodotEnumNames.ParseHint(hintString) : null;
        }

        // The C# type of a Godot class argument (Texture2D, Node …), so a res:// path or node path converts; any object otherwise.
        private static Type ObjectType(string className) =>
            className.Length == 0 ? typeof(GodotObject)
            : GodotTypes.Candidates(className).FirstOrDefault(t => typeof(GodotObject).IsAssignableFrom(t)) ?? typeof(GodotObject);
    }
}
#endif
