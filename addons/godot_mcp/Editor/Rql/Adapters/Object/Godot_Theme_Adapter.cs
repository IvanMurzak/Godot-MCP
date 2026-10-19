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
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Theme: its items as six maps keyed "ControlType/item" — <c>colors["Button/font_color"]</c>, <c>constants</c>,
    /// <c>fonts</c>, <c>fontSizes</c>, <c>icons</c>, <c>styleboxes</c> — read, written and cleared (assign null to an
    /// object item, or call ClearItem) with undo, plus <c>types</c> and <c>items</c> listings.
    /// </summary>
    public sealed class Godot_Theme_Adapter : RqlTypeAdapter<Theme>
    {
        public override string Description =>
            "Theme: DefaultFont, DefaultFontSize, DefaultBaseScale, types, items (type kind name value), maps keyed \"ControlType/item\": " +
            "colors, constants, fonts, fontSizes, icons, styleboxes (assign null to clear a font / icon / stylebox); calls ClearItem(kind, key), " +
            "ClearType(type)";

        // Every item kind with the name ClearItem / items use (aliases: the plural and the name without '_').
        private static readonly (Theme.DataType kind, string name)[] AllKinds =
        {
            (Theme.DataType.Color, "color"), (Theme.DataType.Constant, "constant"), (Theme.DataType.Font, "font"),
            (Theme.DataType.FontSize, "font_size"), (Theme.DataType.Icon, "icon"), (Theme.DataType.Stylebox, "stylebox"),
        };

        protected override void Members(MemberTable<Theme> m)
        {
            m.Add<List<string>>("types", t => t.GetTypeList().Where(type => HasItems(t, type)).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                    description: "control types the theme has items for (\"Button\", \"Label\" …)")
             .Add<List<RqlThemeItem>>("items", t => Items(t), description: "every item: type kind name value");
            Map<Color?>(m, "colors", Theme.DataType.Color, "colors, e.g. colors[\"Button/font_color\"] = \"#ffcc00\" (null clears)");
            Map<int?>(m, "constants", Theme.DataType.Constant, "integer constants, e.g. constants[\"HBoxContainer/separation\"] = 8 (null clears)");
            Map<int?>(m, "fontSizes", Theme.DataType.FontSize, "font sizes, e.g. fontSizes[\"Label/font_size\"] = 24 (null clears)");
            Map<Font?>(m, "fonts", Theme.DataType.Font, "fonts (a res:// path works; null clears)");
            Map<Texture2D?>(m, "icons", Theme.DataType.Icon, "icons (a res:// path works; null clears)");
            Map<StyleBox?>(m, "styleboxes", Theme.DataType.Stylebox, "style boxes (a res:// path or a StyleBox resource; null clears)");
            m.Untyped
                .AddMethod("ClearItem", new[] { new CallableParameter("kind", typeof(string)), new CallableParameter("key", typeof(string)) }, typeof(bool),
                    (o, a) => ClearItem((Theme)o, Kind((string)a[0]!), (string)a[1]!),
                    "remove one item: kind color, constant, font, font_size, icon or stylebox, key \"ControlType/item\"; returns false when there was none")
                .AddMethod("ClearType", new[] { new CallableParameter("type", typeof(string)) }, typeof(int),
                    (o, a) => ClearType((Theme)o, (string)a[0]!), "remove every item of a control type; returns how many");
        }

        // Every "ControlType/item" key is writable (a new key adds the item); a missing item reads as null and assigning null
        // removes one. The listed keys are the existing items.
        private static void Map<T>(MemberTable<Theme> m, string name, Theme.DataType kind, string description)
        {
            m.Map<T>(name,
                t => Keys(t, kind),
                (t, key) => Split(key, out var parts) == null ? As<T>(Get(t, kind, parts.item, parts.type)) : default!,
                (t, key, value) => Set(t, kind, key, value),
                (t, key, value) => Split(key, out _),
                description,
                has: (t, key) => true);
        }

        // Variant ints read as long: the int maps convert them; a missing item reads as null.
        private static T As<T>(object? value)
        {
            if (value is T typed) return typed;
            if (value != null && typeof(T) == typeof(int?)) return (T)(object)Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return default!;
        }

        // A type whose items were all cleared stays in the theme's type list; it is not shown.
        private static bool HasItems(Theme t, string type) => AllKinds.Any(k => t.GetThemeItemList(k.kind, type).Length > 0);

        private static IEnumerable<string> Keys(Theme t, Theme.DataType kind) =>
            t.GetThemeItemTypeList(kind).SelectMany(type => t.GetThemeItemList(kind, type).Select(item => $"{type}/{item}"));

        private static List<RqlThemeItem> Items(Theme t)
        {
            var list = new List<RqlThemeItem>();
            foreach (var (kind, kindName) in AllKinds)
                foreach (var type in t.GetThemeItemTypeList(kind))
                    foreach (var item in t.GetThemeItemList(kind, type))
                        list.Add(new RqlThemeItem(type, kindName, item, GodotVariants.ToClr(t.GetThemeItem(kind, item, type))));
            return list.OrderBy(i => i.type, StringComparer.Ordinal).ThenBy(i => i.kind, StringComparer.Ordinal).ThenBy(i => i.name, StringComparer.Ordinal).ToList();
        }

        private static object? Get(Theme t, Theme.DataType kind, string item, string type) =>
            t.HasThemeItem(kind, item, type) ? GodotVariants.ToClr(t.GetThemeItem(kind, item, type)) : null;

        private static void Set(Theme t, Theme.DataType kind, string key, object? value)
        {
            var (type, item) = Split(key);
            var had = t.HasThemeItem(kind, item, type);
            var before = had ? t.GetThemeItem(kind, item, type) : default;
            var after = GodotVariants.ToVariant(value);
            if (after.VariantType == Variant.Type.Nil)
            {
                if (!had) return;
                t.ClearThemeItem(kind, item, type);
            }
            else t.SetThemeItem(kind, item, type, after);
            RqlGodot.RecordUndoFor(t,
                ur =>
                {
                    if (after.VariantType == Variant.Type.Nil) ur.AddDoMethod(t, Theme.MethodName.ClearThemeItem, (int)kind, item, type);
                    else ur.AddDoMethod(t, Theme.MethodName.SetThemeItem, (int)kind, item, type, after);
                },
                ur =>
                {
                    if (had) ur.AddUndoMethod(t, Theme.MethodName.SetThemeItem, (int)kind, item, type, before);
                    else ur.AddUndoMethod(t, Theme.MethodName.ClearThemeItem, (int)kind, item, type);
                });
        }

        private static bool ClearItem(Theme t, Theme.DataType kind, string key)
        {
            var (type, item) = Split(key);
            if (!t.HasThemeItem(kind, item, type)) return false;
            Set(t, kind, key, null);
            return true;
        }

        private static int ClearType(Theme t, string type)
        {
            var removed = 0;
            foreach (var (kind, _) in AllKinds)
                foreach (var item in t.GetThemeItemList(kind, type))
                {
                    Set(t, kind, $"{type}/{item}", null);
                    removed++;
                }
            return removed;
        }

        private static Theme.DataType Kind(string kind)
        {
            var key = kind.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            foreach (var (k, name) in AllKinds)
            {
                var plain = name.Replace("_", "");
                if (key == plain || key == plain + "s" || key == plain + "es") return k;
            }
            throw new ArgumentException($"unknown theme item kind '{kind}' (use {string.Join(", ", AllKinds.Select(k => k.name))})");
        }

        private static (string type, string item) Split(string key)
        {
            if (Split(key, out var parts) is { } error) throw new ArgumentException(error);
            return parts;
        }

        private static string? Split(string key, out (string type, string item) parts)
        {
            parts = default;
            var slash = key.IndexOf('/');
            if (slash <= 0 || slash == key.Length - 1 || key.IndexOf('/', slash + 1) >= 0)
                return $"a theme item key is \"ControlType/item\", e.g. \"Button/font_color\", not '{key}'";
            parts = (key.Substring(0, slash), key.Substring(slash + 1));
            return null;
        }
    }

    /// <summary>One item of a Theme.</summary>
    public sealed class RqlThemeItem
    {
        public RqlThemeItem(string type, string kind, string name, object? value) { this.type = type; this.kind = kind; this.name = name; this.value = value; }

        [RqlDescription("control type, e.g. \"Button\"")]
        public string type { get; }

        [RqlDescription("color, constant, font, font_size, icon or stylebox")]
        public string kind { get; }

        [RqlDescription("item name, e.g. \"font_color\"")]
        public string name { get; }

        [RqlDescription("the value (write it through the colors / constants / fonts / fontSizes / icons / styleboxes maps)")]
        public object? value { get; }
    }
}
#endif
