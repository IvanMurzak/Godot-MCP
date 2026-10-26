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
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Who still uses a file (the check behind <c>DeleteResource</c>, like the FileSystem dock's "Remove" dialog): the scenes
    /// and resources saved on disk that depend on it, and the nodes of the edited scene that hold it now, its unsaved
    /// changes included (a reference assigned since the last save is not on disk yet).
    /// </summary>
    internal static class RqlReferences
    {
        private static readonly HashSet<string> DependentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tscn", "scn", "tres", "res" };

        /// <summary>The users of <paramref name="resPath"/>: "res://…" files, then "node 'Path' of the edited scene" entries.</summary>
        public static List<string> UsersOf(string resPath)
        {
            var users = new List<string>();
            var uid = RqlGodot.UidOf(resPath);
            // Saved files first (the edited scene's file too: unsaved changes may still be discarded), then the live nodes.
            if (EditorInterface.Singleton.GetResourceFilesystem()?.GetFilesystem() is { } dir)
                foreach (var (path, _) in RqlGodot.WalkFiles(dir))
                {
                    if (string.Equals(path, resPath, StringComparison.OrdinalIgnoreCase) || !DependentExtensions.Contains(System.IO.Path.GetExtension(path).TrimStart('.'))) continue;
                    if (ResourceLoader.GetDependencies(path).Any(d => ResPathNormalizer.DependencyNames(d, resPath, uid))) users.Add(path);
                }
            if (RqlGodot.EditedRoot() is { } root)
                foreach (var node in RqlGodot.AllNodes())
                    // An instance of the scene file, or a property holding the resource.
                    if ((node != root && string.Equals(node.SceneFilePath, resPath, StringComparison.OrdinalIgnoreCase)) || Holds(node, resPath, depth: 0))
                        users.Add($"node '{RqlGodot.PathOf(node)}' of the edited scene {root.SceneFilePath}".TrimEnd());
            return users;
        }

        /// <summary>At most five users, then how many more.</summary>
        public static string Describe(List<string> users) =>
            string.Join(", ", users.Take(5)) + (users.Count > 5 ? $" and {users.Count - 5} more" : "");

        // A stored property of the object holds the file's resource, directly or through built-in sub-resources (a mesh's material).
        private static bool Holds(GodotObject o, string resPath, int depth)
        {
            if (depth > 3) return false;
            foreach (var value in RqlGodot.StoredProperties(o).Values)
            {
                if (value.VariantType != Variant.Type.Object || !(value.AsGodotObject() is Resource r) || !GodotObject.IsInstanceValid(r)) continue;
                if (string.Equals(r.ResourcePath, resPath, StringComparison.OrdinalIgnoreCase)) return true;
                // Built-in sub-resources only: another file resource is checked through its own dependencies above.
                if (!RqlGodot.IsFileResource(r) && Holds(r, resPath, depth + 1)) return true;
            }
            return false;
        }
    }
}
#endif
