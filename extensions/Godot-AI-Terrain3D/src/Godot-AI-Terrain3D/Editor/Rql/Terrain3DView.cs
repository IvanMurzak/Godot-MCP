/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// One Terrain3D node of the edited scene, as a row of the <c>terrain3d</c> source: friendly read-only members read from the
    /// node's Godot properties, the node itself (<c>node</c>, for undoable writes: <c>get node | set props["…"] = …</c>), and
    /// <c>ChangeRegionSize</c>. Terrain3D is a GDExtension class with no C# type, so an adapter cannot target it; this view is
    /// the module's way in.
    /// </summary>
    public sealed class Terrain3DView
    {
        internal Terrain3DView(Node3D node) { this.node = node; }

        [RqlId]
        [RqlDescription("the node's Godot instance id")]
        public ulong id => node.GetInstanceId();

        [RqlReference]
        [RqlDescription("the Terrain3D node (C# Node3D): write its properties with get node | set props[\"data_directory\"] = …")]
        public Node3D node { get; }

        [RqlDisplay]
        [RqlDescription("node name")]
        public string name => node.Name.ToString();

        [RqlDescription("path below the edited scene root")]
        public string path => Terrain3DApi.PathOf(node);

        [RqlDescription("region edge length (64 … 2048); change it with call ChangeRegionSize(size)")]
        public int regionSize => node.Get(Terrain3DNames.RegionSizeProperty).AsInt32();

        [RqlDescription("res:// folder where Terrain3D keeps its region files (empty = the data lives only in memory)")]
        public string dataDirectory => node.Get(Terrain3DNames.DataDirectoryProperty).AsString();

        [RqlDescription("the Terrain3DMaterial (class \"Terrain3DMaterial\"); replace it with set props[\"material\"] = terrain3d.newMaterial()")]
        public Resource? material => node.Get(Terrain3DNames.MaterialProperty).AsGodotObject() as Resource;

        [RqlDescription("regions holding terrain data")]
        public int regionCount => node.Get(Terrain3DNames.DataProperty).AsGodotObject() is { } data
            ? data.Call(Terrain3DNames.GetRegionCountMethod).AsInt32()
            : 0;

        [RqlDescription("clipmap mesh LOD levels")]
        public int meshLods => node.Get(Terrain3DNames.MeshLodsProperty).AsInt32();

        [RqlDescription("clipmap mesh size")]
        public int meshSize => node.Get(Terrain3DNames.MeshSizeProperty).AsInt32();

        [RqlDescription("world distance between vertices")]
        public double vertexSpacing => node.Get(Terrain3DNames.VertexSpacingProperty).AsDouble();

        [RqlDescription("the Terrain3D addon version")]
        public string version => node.Get(Terrain3DNames.VersionProperty).AsString();

        /// <summary>
        /// Terrain3D's <c>change_region_size</c>: re-slices the existing region data into regions of the new size. Not undoable
        /// (the region size is not a scene property: it lives in the region data), so the scene is marked unsaved here.
        /// </summary>
        [RqlCallable("re-slice the terrain into regions of 64, 128, 256, 512, 1024 or 2048 (not undoable); returns the new size")]
        public int ChangeRegionSize(int size)
        {
            if (Terrain3DRules.RegionSize(size) is { } reason) throw new ArgumentException(reason);
            node.Call(Terrain3DNames.ChangeRegionSizeMethod, size);
            EditorInterface.Singleton.MarkSceneAsUnsaved();
            return regionSize;
        }
    }
}
#endif
