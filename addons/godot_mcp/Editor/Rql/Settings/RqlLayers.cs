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

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Settings
{
    /// <summary>
    /// Layer names (Project Settings → Layer Names) and the <c>layerNames["…"]</c> map every Godot object with layer
    /// properties gets: <c>collision_layer</c> / <c>collision_mask</c> of physics objects, <c>visibility_layer</c> /
    /// <c>light_mask</c> of canvas items, <c>layers</c> / <c>cull_mask</c> of 3D visuals and cameras, <c>navigation_layers</c>,
    /// <c>avoidance_layers</c> … — any property carrying one of Godot's layer hints (<see cref="LayerNames"/>). The raw int
    /// properties stay as they are; the map reads and writes the same bits by name.
    /// </summary>
    public static class RqlLayers
    {
        /// <summary>Every layer of every kind (the <c>layers</c> source).</summary>
        public static List<RqlLayer> All() =>
            LayerNames.Kinds.SelectMany(k => Enumerable.Range(1, k.Count).Select(i => new RqlLayer(k, i))).ToList();

        /// <summary>The name of a layer (empty when unnamed).</summary>
        public static string NameOf(LayerNames.Kind kind, int layer) =>
            ProjectSettings.GetSetting(LayerNames.SettingName(kind.Name, layer), "").AsString();

        // The names of a kind's layers, each read once for one read or write.
        private static Func<int, string?> Namer(LayerNames.Kind kind)
        {
            var names = new string?[kind.Count + 1];
            return layer => names[layer] ??= NameOf(kind, layer);
        }

        /// <summary>The layer properties of <paramref name="o"/> (layer-hinted ints), by name.</summary>
        public static IEnumerable<string> Properties(GodotObject o) =>
            GodotVariants.Properties(o).Where(kv => IsLayer(kv.Value, out _)).Select(kv => kv.Key);

        private static bool IsLayer(GodotVariants.GodotProperty info, out LayerNames.Kind kind)
        {
            kind = default;
            return info.Type == Variant.Type.Int && LayerNames.TryKindOf(info.Hint, out kind);
        }

        private static bool TryKind(GodotObject o, string property, out LayerNames.Kind kind)
        {
            kind = default;
            return GodotVariants.Properties(o).TryGetValue(property, out var info) && IsLayer(info, out kind);
        }

        /// <summary>The layers set in a layer property, by name (number when unnamed); null for any other key.</summary>
        public static List<string>? Read(GodotObject o, string property) =>
            TryKind(o, property, out var kind) ? LayerNames.Names(o.Get(property).AsInt64(), kind, Namer(kind)) : null;

        /// <summary>Why <paramref name="value"/> cannot be written to <paramref name="property"/>, or null.</summary>
        public static string? Validate(GodotObject o, string property, object? value)
        {
            if (!TryKind(o, property, out var kind))
            {
                var keys = Properties(o).ToList();
                return keys.Count == 0
                    ? $"{o.GetClass()} has no layer properties"
                    : $"'{property}' is not a layer property of {o.GetClass()} (its layer properties: {string.Join(", ", keys)})";
            }
            try { LayerNames.Mask(value, 0, kind, Namer(kind)); return null; }
            catch (ArgumentException ex) { return ex.Message; }
        }

        /// <summary>Set a layer property to the layers <paramref name="value"/> lists (names or numbers).</summary>
        public static void Write(GodotObject o, string property, object? value)
        {
            if (!TryKind(o, property, out var kind)) throw new ArgumentException(Validate(o, property, value));
            o.Set(property, LayerNames.Mask(value, o.Get(property).AsInt64(), kind, Namer(kind)));
        }
    }

    /// <summary>One layer of the project: its kind, number and name (writable: renames it in project.godot).</summary>
    public sealed class RqlLayer : IRqlViewOf
    {
        private readonly LayerNames.Kind _kind;

        public RqlLayer(LayerNames.Kind kind, int index) { _kind = kind; this.index = index; }

        GodotObject IRqlViewOf.Owner => RqlProject.Instance;

        [RqlDescription("2d_render, 2d_physics, 2d_navigation, 3d_render, 3d_physics, 3d_navigation or avoidance")]
        public string kind => _kind.Name;

        [RqlDescription("layer number (1-based, like the inspector)")]
        public int index { get; }

        [RqlDescription("its bit in a layer mask (1 << (index - 1))")]
        public long bit => LayerNames.Bit(index);

        [RqlDescription("the layer's name (\"\" when unnamed); set it to rename the layer")]
        public string name
        {
            get => RqlLayers.NameOf(_kind, index);
            set => RqlProject.Set(setting, value ?? "");
        }

        [RqlDescription("the project setting holding the name, e.g. layer_names/3d_physics/layer_2")]
        public string setting => LayerNames.SettingName(_kind.Name, index);
    }
}
#endif
