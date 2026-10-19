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
    /// TileSet: its sources (atlas textures / scene collections, by source id) and its layers (physics, terrain sets and
    /// terrains, navigation, occlusion, custom data), which Godot keeps in dynamic properties; calls add sources and layers
    /// (undoable). Per-layer settings stay reachable through <c>props["physics_layer_0/collision_layer"]</c> etc.
    /// </summary>
    public sealed class Godot_TileSet_Adapter : RqlTypeAdapter<TileSet>
    {
        public override string Description =>
            "TileSet: TileShape, TileSize, sources (id kind atlas scenes texture tileCount), physicsLayers, terrainSets, customDataLayers, " +
            "navigationLayers, occlusionLayers; calls AddAtlasSource(texture, tileSize?), RemoveSource(id), AddPhysicsLayer(), " +
            "AddTerrainSet(mode?), AddTerrain(set, name, color?), AddCustomDataLayer(name, type), AddNavigationLayer(), AddOcclusionLayer()";

        protected override void Members(MemberTable<TileSet> m)
        {
            m.Add<List<RqlTileSource>>("sources", t => Sources(t), description: "the sources by id: id kind atlas texture tileCount (get sources | get atlas for an atlas source's tiles and calls)", owning: true)
             .Add<int>("physicsLayers", t => t.GetPhysicsLayersCount(), description: "number of physics layers (settings: props[\"physics_layer_<i>/…\"])")
             .Add<List<RqlTerrainSet>>("terrainSets", t => Enumerable.Range(0, t.GetTerrainSetsCount()).Select(i => new RqlTerrainSet(t, i)).ToList(),
                    description: "terrain sets: index mode terrains")
             .Add<List<RqlCustomDataLayer>>("customDataLayers",
                    t => Enumerable.Range(0, t.GetCustomDataLayersCount()).Select(i => new RqlCustomDataLayer(t.GetCustomDataLayerName(i), t.GetCustomDataLayerType(i).ToString(), i)).ToList(),
                    description: "custom data layers: index name type")
             .Add<int>("navigationLayers", t => t.GetNavigationLayersCount(), description: "number of navigation layers")
             .Add<int>("occlusionLayers", t => t.GetOcclusionLayersCount(), description: "number of occlusion layers");

            var u = m.Untyped;
            u.AddMethod("AddAtlasSource",
                    new[] { new CallableParameter("texture", typeof(Texture2D)), new CallableParameter("tileSize", typeof(Vector2I), hasDefault: true, defaultValue: null) },
                    typeof(int), (o, a) => AddAtlasSource((TileSet)o, (Texture2D)a[0]!, a[1] is Vector2I size ? size : (Vector2I?)null),
                    "add an atlas source for a texture (a res:// path works), its tiles sized tileSize (default: the TileSet's TileSize); returns its source id")
             .AddMethod("RemoveSource", new[] { new CallableParameter("id", typeof(int)) }, typeof(void),
                    (o, a) => { RemoveSource((TileSet)o, (int)a[0]!); return null; }, "remove the source with that id")
             .AddMethod("AddPhysicsLayer", Array.Empty<CallableParameter>(), typeof(int),
                    (o, a) => AddLayer((TileSet)o, t => t.GetPhysicsLayersCount(), t => t.AddPhysicsLayer(), TileSet.MethodName.AddPhysicsLayer, TileSet.MethodName.RemovePhysicsLayer),
                    "add a physics layer; returns its index")
             .AddMethod("AddNavigationLayer", Array.Empty<CallableParameter>(), typeof(int),
                    (o, a) => AddLayer((TileSet)o, t => t.GetNavigationLayersCount(), t => t.AddNavigationLayer(), TileSet.MethodName.AddNavigationLayer, TileSet.MethodName.RemoveNavigationLayer),
                    "add a navigation layer; returns its index")
             .AddMethod("AddOcclusionLayer", Array.Empty<CallableParameter>(), typeof(int),
                    (o, a) => AddLayer((TileSet)o, t => t.GetOcclusionLayersCount(), t => t.AddOcclusionLayer(), TileSet.MethodName.AddOcclusionLayer, TileSet.MethodName.RemoveOcclusionLayer),
                    "add an occlusion layer; returns its index")
             .AddMethod("AddTerrainSet", new[] { new CallableParameter("mode", typeof(string), hasDefault: true, defaultValue: "MatchCornersAndSides") }, typeof(int),
                    (o, a) => AddTerrainSet((TileSet)o, (string)a[0]!),
                    "add a terrain set (mode MatchCornersAndSides, MatchCorners or MatchSides); returns its index")
             .AddMethod("AddTerrain",
                    new[] { new CallableParameter("set", typeof(int)), new CallableParameter("name", typeof(string)), new CallableParameter("color", typeof(Color), hasDefault: true, defaultValue: null) },
                    typeof(int), (o, a) => AddTerrain((TileSet)o, (int)a[0]!, (string)a[1]!, a[2] is Color c ? c : (Color?)null),
                    "add a terrain to a terrain set; returns its index in the set")
             .AddMethod("AddCustomDataLayer", new[] { new CallableParameter("name", typeof(string)), new CallableParameter("type", typeof(string)) }, typeof(int),
                    (o, a) => AddCustomDataLayer((TileSet)o, (string)a[0]!, (string)a[1]!),
                    "add a custom data layer of a Godot value type (\"Int\", \"Float\", \"Bool\", \"String\", \"Color\", \"Vector2\" …); returns its index");
        }

        private static List<RqlTileSource> Sources(TileSet t)
        {
            var list = new List<RqlTileSource>(t.GetSourceCount());
            for (int i = 0; i < t.GetSourceCount(); i++)
            {
                var id = t.GetSourceId(i);
                var source = t.GetSource(id);
                RqlGodot.RegisterContainer(source, t);
                list.Add(new RqlTileSource(id, source));
            }
            return list;
        }

        private static int AddAtlasSource(TileSet t, Texture2D texture, Vector2I? tileSize)
        {
            var size = tileSize ?? t.TileSize;
            if (size.X <= 0 || size.Y <= 0) throw new ArgumentException($"tileSize must be positive, got {size}");
            var source = new TileSetAtlasSource { Texture = texture, TextureRegionSize = size };
            var id = t.AddSource(source);
            RqlGodot.RegisterContainer(source, t);
            RqlGodot.RecordUndoFor(t,
                ur => { ur.AddDoMethod(t, TileSet.MethodName.AddSource, source, id); ur.AddDoReference(source); },
                ur => ur.AddUndoMethod(t, TileSet.MethodName.RemoveSource, id));
            return id;
        }

        private static void RemoveSource(TileSet t, int id)
        {
            if (!t.HasSource(id))
                throw new ArgumentException($"the TileSet has no source {id} (its sources: {string.Join(", ", Enumerable.Range(0, t.GetSourceCount()).Select(t.GetSourceId))})");
            var source = t.GetSource(id);
            t.RemoveSource(id);
            RqlGodot.RecordUndoFor(t,
                ur => ur.AddDoMethod(t, TileSet.MethodName.RemoveSource, id),
                ur => { ur.AddUndoMethod(t, TileSet.MethodName.AddSource, source, id); ur.AddUndoReference(source); });
        }

        private static int AddLayer(TileSet t, Func<TileSet, int> count, Action<TileSet> add, StringName addMethod, StringName removeMethod)
        {
            var index = count(t);
            add(t);
            RqlGodot.RecordUndoFor(t, ur => ur.AddDoMethod(t, addMethod, index), ur => ur.AddUndoMethod(t, removeMethod, index));
            return index;
        }

        /// <summary>A terrain mode as the editor names it: MatchCornersAndSides, MatchCorners, MatchSides.</summary>
        internal static string TerrainModeName(TileSet.TerrainMode mode) => "Match" + mode;

        private static int AddTerrainSet(TileSet t, string mode)
        {
            var key = mode.Replace(" ", "").Replace("_", "");
            if (key.StartsWith("match", StringComparison.OrdinalIgnoreCase)) key = key.Substring(5);
            if (!TryParseName<TileSet.TerrainMode>(key, out var parsed))
                throw new ArgumentException($"unknown terrain mode '{mode}' (use {string.Join(", ", Enum.GetValues(typeof(TileSet.TerrainMode)).Cast<TileSet.TerrainMode>().Select(TerrainModeName))})");
            var index = t.GetTerrainSetsCount();
            t.AddTerrainSet();
            t.SetTerrainSetMode(index, parsed);
            RqlGodot.RecordUndoFor(t,
                ur => { ur.AddDoMethod(t, TileSet.MethodName.AddTerrainSet, index); ur.AddDoMethod(t, TileSet.MethodName.SetTerrainSetMode, index, (int)parsed); },
                ur => ur.AddUndoMethod(t, TileSet.MethodName.RemoveTerrainSet, index));
            return index;
        }

        private static int AddTerrain(TileSet t, int set, string name, Color? color)
        {
            if (set < 0 || set >= t.GetTerrainSetsCount())
                throw new ArgumentException($"the TileSet has no terrain set {set} ({t.GetTerrainSetsCount()} terrain set(s); add one with AddTerrainSet())");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a terrain needs a name");
            var index = t.GetTerrainsCount(set);
            t.AddTerrain(set);
            t.SetTerrainName(set, index, name);
            if (color is { } c) t.SetTerrainColor(set, index, c);
            var finalColor = t.GetTerrainColor(set, index);
            RqlGodot.RecordUndoFor(t,
                ur =>
                {
                    ur.AddDoMethod(t, TileSet.MethodName.AddTerrain, set, index);
                    ur.AddDoMethod(t, TileSet.MethodName.SetTerrainName, set, index, name);
                    ur.AddDoMethod(t, TileSet.MethodName.SetTerrainColor, set, index, finalColor);
                },
                ur => ur.AddUndoMethod(t, TileSet.MethodName.RemoveTerrain, set, index));
            return index;
        }

        // An enum value by its name only: Enum.TryParse also takes numbers ("3") and flag lists ("Bool,Int").
        private static bool TryParseName<T>(string text, out T value) where T : struct, Enum
        {
            value = default;
            return Enum.GetNames(typeof(T)).FirstOrDefault(n => string.Equals(n, text.Trim(), StringComparison.OrdinalIgnoreCase)) is { } name
                   && Enum.TryParse(name, out value);
        }

        private static int AddCustomDataLayer(TileSet t, string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a custom data layer needs a name");
            if (t.GetCustomDataLayerByName(name) >= 0) throw new ArgumentException($"the TileSet already has a custom data layer '{name}'");
            if (!TryParseName<Variant.Type>(type, out var parsed) || parsed == Variant.Type.Nil || parsed == Variant.Type.Max)
                throw new ArgumentException($"unknown value type '{type}' (e.g. Int, Float, Bool, String, Color, Vector2, Vector2I, Object)");
            var index = t.GetCustomDataLayersCount();
            t.AddCustomDataLayer();
            t.SetCustomDataLayerName(index, name);
            t.SetCustomDataLayerType(index, parsed);
            RqlGodot.RecordUndoFor(t,
                ur =>
                {
                    ur.AddDoMethod(t, TileSet.MethodName.AddCustomDataLayer, index);
                    ur.AddDoMethod(t, TileSet.MethodName.SetCustomDataLayerName, index, name);
                    ur.AddDoMethod(t, TileSet.MethodName.SetCustomDataLayerType, index, (int)parsed);
                },
                ur => ur.AddUndoMethod(t, TileSet.MethodName.RemoveCustomDataLayer, index));
            return index;
        }
    }

    /// <summary>A source of a TileSet with its id.</summary>
    public sealed class RqlTileSource
    {
        public RqlTileSource(int id, TileSetSource source) { this.id = id; this.source = source; }

        [RqlDescription("source id (what TileMapLayer cells refer to)")]
        public int id { get; }

        [RqlDescription("\"atlas\" (a texture cut into tiles) or \"scenes\" (a scene collection)")]
        public string kind => source is TileSetAtlasSource ? "atlas" : source is TileSetScenesCollectionSource ? "scenes" : source.GetClass();

        private TileSetSource source { get; }

        [RqlDescription("the atlas source (Texture, TextureRegionSize, tiles; calls CreateTile, RemoveTile, CreateTilesFromTexture), or null")]
        public TileSetAtlasSource? atlas => source as TileSetAtlasSource;

        [RqlDescription("the scene collection source, or null")]
        public TileSetScenesCollectionSource? scenes => source as TileSetScenesCollectionSource;

        [RqlDescription("res:// path of an atlas source's texture, or null")]
        public string? texture => source is TileSetAtlasSource atlas && atlas.Texture is { } t && t.ResourcePath.Length > 0 ? t.ResourcePath : null;

        [RqlDescription("number of tiles (atlas) or scenes (scene collection)")]
        public int tileCount => source.GetTilesCount();
    }

    /// <summary>A terrain set of a TileSet.</summary>
    public sealed class RqlTerrainSet
    {
        private readonly TileSet _tileSet;

        public RqlTerrainSet(TileSet tileSet, int index) { _tileSet = tileSet; this.index = index; }

        [RqlDescription("terrain set index")]
        public int index { get; }

        [RqlDescription("MatchCornersAndSides, MatchCorners or MatchSides")]
        public string mode => Godot_TileSet_Adapter.TerrainModeName(_tileSet.GetTerrainSetMode(index));

        [RqlDescription("terrain names, by index")]
        public List<string> terrains => Enumerable.Range(0, _tileSet.GetTerrainsCount(index)).Select(i => _tileSet.GetTerrainName(index, i)).ToList();
    }

    /// <summary>A custom data layer of a TileSet.</summary>
    public sealed class RqlCustomDataLayer
    {
        public RqlCustomDataLayer(string name, string type, int index) { this.name = name; this.type = type; this.index = index; }

        [RqlDescription("layer index")]
        public int index { get; }

        [RqlDescription("layer name (TileData.GetCustomData(name))")]
        public string name { get; }

        [RqlDescription("Godot value type of the layer")]
        public string type { get; }
    }

    /// <summary>
    /// An atlas source of a TileSet: its tiles by atlas coordinates; calls create and remove tiles (undoable). Tile settings
    /// (modulate, z index, terrain, custom data …) are on each tile's <c>data</c> (a TileData object, <c>props[…]</c>).
    /// </summary>
    public sealed class Godot_TileSetAtlasSource_Adapter : RqlTypeAdapter<TileSetAtlasSource>
    {
        public override string Description =>
            "TileSet atlas source: Texture, TextureRegionSize, Margins, Separation, tiles (coords size data); calls CreateTile(coords, size?), " +
            "RemoveTile(coords), CreateTilesFromTexture()";

        protected override void Members(MemberTable<TileSetAtlasSource> m)
        {
            m.Add<List<RqlTile>>("tiles", a => Tiles(a), description: "tiles by atlas coordinates: coords size data (TileData, alternative 0)", owning: true)
             .Add<Vector2I>("gridSize", a => a.GetAtlasGridSize(), description: "number of tile columns and rows the texture holds");
            m.Untyped
                .AddMethod("CreateTile",
                    new[] { new CallableParameter("coords", typeof(Vector2I)), new CallableParameter("size", typeof(Vector2I), hasDefault: true, defaultValue: null) },
                    typeof(RqlTile), (o, a) => CreateTile((TileSetAtlasSource)o, (Vector2I)a[0]!, a[1] is Vector2I s ? s : Vector2I.One),
                    "create a tile at atlas coordinates \"(x, y)\" spanning size cells (default (1, 1)); returns it")
                .AddMethod("RemoveTile", new[] { new CallableParameter("coords", typeof(Vector2I)) }, typeof(void),
                    (o, a) => { RemoveTile((TileSetAtlasSource)o, (Vector2I)a[0]!); return null; }, "remove the tile at atlas coordinates")
                .AddMethod("CreateTilesFromTexture", Array.Empty<CallableParameter>(), typeof(int),
                    (o, a) => CreateTilesFromTexture((TileSetAtlasSource)o),
                    "create a tile in every free grid cell of the texture that is not fully transparent (the editor's \"create tiles in non-transparent regions\"); returns how many");
        }

        private static List<RqlTile> Tiles(TileSetAtlasSource a) =>
            Enumerable.Range(0, a.GetTilesCount()).Select(i => new RqlTile(a, a.GetTileId(i))).ToList();

        private static RqlTile CreateTile(TileSetAtlasSource a, Vector2I coords, Vector2I size)
        {
            if (a.Texture == null) throw new InvalidOperationException("the atlas source has no texture; set Texture first");
            if (size.X <= 0 || size.Y <= 0) throw new ArgumentException($"size must be positive, got {size}");
            if (!a.HasRoomForTile(coords, size, 1, Vector2I.Zero, 1))
                throw new ArgumentException($"no room for a {size} tile at {coords}: outside the texture grid {a.GetAtlasGridSize()} or overlapping a tile");
            a.CreateTile(coords, size);
            RqlGodot.RecordUndoFor(a,
                ur => ur.AddDoMethod(a, TileSetAtlasSource.MethodName.CreateTile, coords, size),
                ur => ur.AddUndoMethod(a, TileSetAtlasSource.MethodName.RemoveTile, coords));
            return new RqlTile(a, coords);
        }

        private static void RemoveTile(TileSetAtlasSource a, Vector2I coords)
        {
            if (!a.HasTile(coords)) throw new ArgumentException($"no tile at {coords}");
            var size = a.GetTileSizeInAtlas(coords);
            // The tile's stored properties ("x:y/…": its alternatives and their data), to restore them on undo.
            var prefix = $"{coords.X}:{coords.Y}/";
            var saved = RqlGodot.SnapshotOf(a).Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(kv => (name: kv.Key, value: kv.Value)).ToList();
            a.RemoveTile(coords);
            RqlGodot.RecordUndoFor(a,
                ur => ur.AddDoMethod(a, TileSetAtlasSource.MethodName.RemoveTile, coords),
                ur =>
                {
                    ur.AddUndoMethod(a, TileSetAtlasSource.MethodName.CreateTile, coords, size);
                    foreach (var (name, value) in saved) ur.AddUndoProperty(a, name, value);
                });
        }

        private static int CreateTilesFromTexture(TileSetAtlasSource a)
        {
            if (a.Texture == null) throw new InvalidOperationException("the atlas source has no texture; set Texture first");
            var image = a.Texture.GetImage() ?? throw new InvalidOperationException("the texture's image cannot be read");
            if (image.IsCompressed()) image.Decompress();
            var grid = a.GetAtlasGridSize();
            var created = new List<Vector2I>();
            for (int y = 0; y < grid.Y; y++)
                for (int x = 0; x < grid.X; x++)
                {
                    var coords = new Vector2I(x, y);
                    if (a.HasTile(coords) || a.GetTileAtCoords(coords) != new Vector2I(-1, -1)) continue;
                    if (!a.HasRoomForTile(coords, Vector2I.One, 1, Vector2I.Zero, 1)) continue;
                    if (IsTransparent(image, CellRegion(a, coords))) continue;
                    a.CreateTile(coords);
                    created.Add(coords);
                }
            if (created.Count > 0)
                RqlGodot.RecordUndoFor(a,
                    ur => { foreach (var c in created) ur.AddDoMethod(a, TileSetAtlasSource.MethodName.CreateTile, c, Vector2I.One); },
                    ur => { foreach (var c in created) ur.AddUndoMethod(a, TileSetAtlasSource.MethodName.RemoveTile, c); });
            return created.Count;
        }

        // The texture region of a grid cell (GetTileTextureRegion needs an existing tile).
        private static Rect2I CellRegion(TileSetAtlasSource a, Vector2I coords)
        {
            var size = a.TextureRegionSize;
            var step = size + a.Separation;
            return new Rect2I(a.Margins + coords * step, size);
        }

        // One native call per cell (GetPixel per pixel would be one per pixel).
        private static bool IsTransparent(Image image, Rect2I region)
        {
            var clipped = region.Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
            return clipped.Area == 0 || image.GetRegion(clipped).IsInvisible();
        }
    }

    /// <summary>A tile of an atlas source.</summary>
    public sealed class RqlTile : Settings.IRqlViewOf
    {
        private readonly TileSetAtlasSource _atlas;

        public RqlTile(TileSetAtlasSource atlas, Vector2I coords) { _atlas = atlas; this.coords = coords; }

        // A write to the tile's data is a change of its atlas source (undo, and the TileSet file is saved).
        GodotObject Settings.IRqlViewOf.Owner => _atlas;

        [RqlDescription("atlas coordinates of the tile")]
        public Vector2I coords { get; }

        [RqlDescription("size in atlas cells")]
        public Vector2I size => _atlas.GetTileSizeInAtlas(coords);

        [RqlDescription("number of alternative tiles (0 = the base tile only)")]
        public int alternatives => Math.Max(0, _atlas.GetAlternativeTilesCount(coords) - 1);

        [RqlDescription("the tile's settings (alternative 0): Modulate, ZIndex, Probability, TerrainSet, Terrain, props[\"custom_data_0\"] …")]
        public TileData data => _atlas.GetTileData(coords, 0);
    }
}
#endif
