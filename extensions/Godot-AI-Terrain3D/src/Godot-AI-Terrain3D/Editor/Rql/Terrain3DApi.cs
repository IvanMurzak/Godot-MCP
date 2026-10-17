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
using System.Collections.Generic;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// The module's sources and function: <c>terrain3d</c> (one <see cref="Terrain3DView"/> per Terrain3D node of the edited
    /// scene), <c>terrain3d.addon</c> (whether the GDExtension is loaded) and <c>terrain3d.newMaterial()</c> (a new
    /// Terrain3DMaterial, made through ClassDB: the class has no C# type, so an object literal cannot build it). Every RQL query
    /// runs on the editor main thread, so the Godot API is safe here.
    /// </summary>
    public static class Terrain3DApi
    {
        public static bool Installed => ClassDB.ClassExists(Terrain3DNames.TerrainClass);

        /// <summary>The Terrain3D nodes of the edited scene, depth-first, root first (none when the addon is missing).</summary>
        public static List<Terrain3DView> Terrains()
        {
            var views = new List<Terrain3DView>();
            if (Installed && EditorInterface.Singleton.GetEditedSceneRoot() is { } root) Collect(root, views);
            return views;
        }

        private static void Collect(Node node, List<Terrain3DView> views)
        {
            if (node is Node3D node3D && node.IsClass(Terrain3DNames.TerrainClass)) views.Add(new Terrain3DView(node3D));
            foreach (var child in node.GetChildren(includeInternal: false)) Collect(child, views);
        }

        /// <summary><c>terrain3d.newMaterial()</c>: a new Terrain3DMaterial to assign to a terrain's <c>material</c>.</summary>
        public static object? NewMaterial(object?[] args)
        {
            if (args.Length > 0) throw new ArgumentException("terrain3d.newMaterial() takes no arguments");
            if (Terrain3DRules.Installed(ClassDB.ClassExists(Terrain3DNames.MaterialClass)) is { } reason) throw new InvalidOperationException(reason);
            return ClassDB.Instantiate(Terrain3DNames.MaterialClass).AsGodotObject() as Resource
                   ?? throw new InvalidOperationException($"could not create a {Terrain3DNames.MaterialClass}");
        }

        internal static string PathOf(Node node)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            return root == null ? node.Name.ToString() : root.GetPathTo(node).ToString();
        }

        public static readonly Terrain3DAddon Addon = new Terrain3DAddon();
    }

    /// <summary>The <c>terrain3d.addon</c> source: is the Terrain3D GDExtension loaded, and how to install it.</summary>
    public sealed class Terrain3DAddon
    {
        [RqlDescription("true when the Terrain3D GDExtension is loaded (its classes are in ClassDB)")]
        public bool installed => Terrain3DApi.Installed;

        [RqlDescription("the addon this module drives")]
        public string addon => Terrain3DNames.AddonName;

        [RqlDescription("how to install the addon (empty when it is installed)")]
        public string hint => Terrain3DApi.Installed ? "" : Terrain3DNames.InstallHint;
    }
}
#endif
