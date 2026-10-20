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
    /// MeshLibrary (the item palette of a GridMap): its items by id — name, mesh, mesh transform, collision shapes,
    /// navigation mesh, preview — with CreateItem / RemoveItem (undoable, a removed item comes back with all its settings).
    /// Item settings are Godot's stored <c>item/&lt;id&gt;/…</c> properties, so editing a row is a change of the library:
    /// undoable through the property snapshot and saved with its .tres.
    /// </summary>
    public sealed class Godot_MeshLibrary_Adapter : RqlTypeAdapter<MeshLibrary>
    {
        public override string Description =>
            "MeshLibrary: items (id name mesh meshTransform shapes navigationMesh navigationMeshTransform navigationLayers preview); " +
            "calls CreateItem(name, mesh?, id?), RemoveItem(id); item rows: set name / mesh / …, call AddShape(shape, transform?), ClearShapes()";

        protected override void Members(MemberTable<MeshLibrary> m)
        {
            m.Add<List<RqlMeshItem>>("items", lib => lib.GetItemList().OrderBy(id => id).Select(id => new RqlMeshItem(lib, id)).ToList(),
                description: "the items by id: id name mesh meshTransform shapes navigationMesh preview (set them on a row)", owning: true);
            m.Untyped
                .AddMethod("CreateItem",
                    new[]
                    {
                        new CallableParameter("name", typeof(string)), new CallableParameter("mesh", typeof(Mesh), hasDefault: true, defaultValue: null),
                        new CallableParameter("id", typeof(int), hasDefault: true, defaultValue: -1),
                    },
                    typeof(RqlMeshItem), (o, a) => CreateItem((MeshLibrary)o, (string)a[0]!, a[1] as Mesh, a[2] is int id ? id : -1),
                    "add an item with a name and an optional mesh (a res:// path works) at a free id (or the id given); returns it")
                .AddMethod("RemoveItem", new[] { new CallableParameter("id", typeof(int)) }, typeof(void),
                    (o, a) => { RemoveItem((MeshLibrary)o, (int)a[0]!); return null; }, "remove the item with that id");
        }

        private static RqlMeshItem CreateItem(MeshLibrary lib, string name, Mesh? mesh, int id)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("an item needs a name");
            if (id < 0) id = lib.GetLastUnusedItemId();
            else if (lib.GetItemList().Contains(id)) throw new ArgumentException($"the MeshLibrary already has an item {id} ('{lib.GetItemName(id)}')");
            lib.CreateItem(id);
            lib.SetItemName(id, name);
            if (mesh != null) lib.SetItemMesh(id, mesh);
            RqlGodot.RecordUndoFor(lib,
                ur =>
                {
                    ur.AddDoMethod(lib, MeshLibrary.MethodName.CreateItem, id);
                    ur.AddDoMethod(lib, MeshLibrary.MethodName.SetItemName, id, name);
                    if (mesh != null) { ur.AddDoMethod(lib, MeshLibrary.MethodName.SetItemMesh, id, mesh); ur.AddDoReference(mesh); }
                },
                ur => ur.AddUndoMethod(lib, MeshLibrary.MethodName.RemoveItem, id));
            return new RqlMeshItem(lib, id);
        }

        private static void RemoveItem(MeshLibrary lib, int id)
        {
            RqlMeshItem.Require(lib, id);
            // The item's stored properties ("item/<id>/…") come back on undo.
            RqlGodot.RecordRemoval(lib, $"item/{id}/", () => lib.RemoveItem(id),
                ur => ur.AddDoMethod(lib, MeshLibrary.MethodName.RemoveItem, id),
                ur => ur.AddUndoMethod(lib, MeshLibrary.MethodName.CreateItem, id));
        }
    }

    /// <summary>An item of a MeshLibrary; its settings are written on the row (a change of the library).</summary>
    public sealed class RqlMeshItem : Settings.IRqlViewOf
    {
        private readonly MeshLibrary _lib;

        public RqlMeshItem(MeshLibrary lib, int id) { _lib = lib; this.id = id; }

        GodotObject Settings.IRqlViewOf.Owner => _lib;

        internal static void Require(MeshLibrary lib, int id)
        {
            if (!lib.GetItemList().Contains(id))
                throw new ArgumentException($"the MeshLibrary has no item {id} (its items: {string.Join(", ", lib.GetItemList().OrderBy(i => i))})");
        }

        // Writes and calls check the item still exists (rows are listed from the library, so reads need not).
        private int Id { get { Require(_lib, id); return id; } }

        [RqlDescription("item id (what GridMap cells refer to)")]
        public int id { get; }

        [RqlDescription("item name")]
        public string name { get => _lib.GetItemName(id); set => _lib.SetItemName(Id, value ?? ""); }

        [RqlDescription("the mesh (a res:// path works), or null")]
        public Mesh? mesh { get => _lib.GetItemMesh(id); set => _lib.SetItemMesh(Id, value); }

        [RqlDescription("transform of the mesh within the cell")]
        public Transform3D meshTransform { get => _lib.GetItemMeshTransform(id); set => _lib.SetItemMeshTransform(Id, value); }

        [RqlDescription("collision shapes: index shape transform (add with call AddShape(shape, transform?), ClearShapes())")]
        public List<RqlMeshShape> shapes
        {
            get
            {
                var raw = _lib.GetItemShapes(id);
                var list = new List<RqlMeshShape>();
                for (int i = 0; i + 1 < raw.Count; i += 2)
                    list.Add(new RqlMeshShape(list.Count, raw[i].As<Shape3D>(), raw[i + 1].VariantType == Variant.Type.Transform3D ? raw[i + 1].AsTransform3D() : Transform3D.Identity));
                return list;
            }
        }

        [RqlDescription("the navigation mesh (a res:// path works), or null")]
        public NavigationMesh? navigationMesh { get => _lib.GetItemNavigationMesh(id); set => _lib.SetItemNavigationMesh(Id, value); }

        [RqlDescription("transform of the navigation mesh within the cell")]
        public Transform3D navigationMeshTransform { get => _lib.GetItemNavigationMeshTransform(id); set => _lib.SetItemNavigationMeshTransform(Id, value); }

        [RqlDescription("navigation layers bit mask of the navigation mesh (by name: the library's layerNames[\"item/<id>/navigation_layers\"])")]
        public uint navigationLayers { get => _lib.GetItemNavigationLayers(id); set => _lib.SetItemNavigationLayers(Id, value); }

        [RqlDescription("the preview icon (a res:// texture works), or null")]
        public Texture2D? preview { get => _lib.GetItemPreview(id); set => _lib.SetItemPreview(Id, value); }

        [RqlCallable("add a collision shape (a res:// path works) with an optional transform; returns the number of shapes")]
        public int AddShape(Shape3D shape, Transform3D? transform = null)
        {
            if (shape == null) throw new ArgumentException("AddShape needs a Shape3D");
            var raw = _lib.GetItemShapes(Id).Duplicate();
            raw.Add(shape);
            raw.Add(transform ?? Transform3D.Identity);
            _lib.SetItemShapes(Id, raw);
            return raw.Count / 2;
        }

        [RqlCallable("remove every collision shape of the item")]
        public void ClearShapes() => _lib.SetItemShapes(Id, new global::Godot.Collections.Array());
    }

    /// <summary>A collision shape of a MeshLibrary item.</summary>
    public sealed class RqlMeshShape
    {
        public RqlMeshShape(int index, Shape3D? shape, Transform3D transform) { this.index = index; this.shape = shape; this.transform = transform; }

        [RqlDescription("position in the item's shape list")]
        public int index { get; }

        [RqlDescription("the Shape3D")]
        public Shape3D? shape { get; }

        [RqlDescription("its transform within the cell")]
        public Transform3D transform { get; }
    }
}
#endif
