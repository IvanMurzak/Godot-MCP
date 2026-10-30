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
    /// and resources saved on disk that depend on it, the files that name it as a string (<see cref="Mentions"/>), and the
    /// nodes of the edited scene that hold it now, its unsaved changes included (a reference assigned since the last save is
    /// not on disk yet).
    /// </summary>
    internal static class RqlReferences
    {
        private static readonly HashSet<string> DependentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tscn", "scn", "tres", "res" };

        /// <summary>Whether the file at <paramref name="path"/> can reference other files (a scene or resource).</summary>
        internal static bool CanDepend(string path) => DependentExtensions.Contains(System.IO.Path.GetExtension(path).TrimStart('.'));

        // Text files whose contents can name another file as a string: scripts, shaders, text scenes / resources, data and config.
        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "tscn", "tres", "gd", "cs", "gdshader", "gdshaderinc", "json", "cfg", "ini", "xml", "yml", "yaml", "toml" };

        private const long MaxTextBytes = 4 * 1024 * 1024;
        private const string ProjectFile = "res://project.godot";
        private const string OwnAddonFolder = "res://addons/godot_mcp/"; // its sources quote example paths

        private static Dictionary<string, List<string>>? _mentions;
        private static ulong _mentionsFrame = ulong.MaxValue;

        /// <summary>
        /// The files that name another file as a string, keyed by the named file's res:// path: a res:// path or uid:// id in
        /// a script (<c>load</c>, <c>preload</c>, <c>ChangeSceneToFile</c>), a string property of a text scene or resource (an
        /// exported <c>NextLevel = "res://…"</c>), <c>project.godot</c> (main scene, autoloads, icon). Dependencies Godot
        /// records are not strings to it, so these are what <see cref="ResourceLoader.GetDependencies"/> misses. A path built
        /// from parts at run time is not seen. Read once per editor frame (the calls of one query or mutation share it).
        /// </summary>
        internal static Dictionary<string, List<string>> Mentions()
        {
            var frame = Engine.GetProcessFrames();
            if (_mentions != null && _mentionsFrame == frame) return _mentions;
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var files = new List<string> { ProjectFile };
            if (EditorInterface.Singleton.GetResourceFilesystem()?.GetFilesystem() is { } dir)
                files.AddRange(RqlGodot.WalkFiles(dir).Take(RqlGodot.MaxFiles).Select(f => f.path)
                    .Where(f => TextExtensions.Contains(System.IO.Path.GetExtension(f).TrimStart('.'))
                                && !f.StartsWith(OwnAddonFolder, StringComparison.OrdinalIgnoreCase)));
            foreach (var file in files)
            {
                if (ReadText(file) is not { } text) continue;
                foreach (var mention in ResPathNormalizer.MentionedPaths(text))
                {
                    var target = ResolveMention(mention);
                    if (target == null || string.Equals(target, file, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!index.TryGetValue(target, out var users)) index[target] = users = new List<string>();
                    if (!users.Contains(file)) users.Add(file);
                }
            }
            _mentionsFrame = frame;
            return _mentions = index;
        }

        /// <summary>The files that still exist and name <paramref name="resPath"/> as a string (see <see cref="Mentions"/>).</summary>
        internal static IEnumerable<string> MentionsOf(string resPath) =>
            Mentions().TryGetValue(resPath, out var users) ? users.Where(FileAccess.FileExists) : Enumerable.Empty<string>();

        private static string? ReadText(string resPath)
        {
            try
            {
                var file = new System.IO.FileInfo(ProjectSettings.GlobalizePath(resPath));
                return file.Exists && file.Length <= MaxTextBytes ? System.IO.File.ReadAllText(file.FullName) : null;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { return null; }
        }

        // A uid:// id names the file it is registered for now; a res:// path is taken as written.
        private static string? ResolveMention(string mention)
        {
            if (!mention.StartsWith("uid://", StringComparison.Ordinal)) return mention;
            var id = ResourceUid.TextToId(mention);
            return id != ResourceUid.InvalidId && ResourceUid.HasId(id) ? ResourceUid.GetIdPath(id) : null;
        }

        /// <summary>The users of <paramref name="resPath"/>: "res://…" files, then "node 'Path' of the edited scene" entries.</summary>
        public static List<string> UsersOf(string resPath)
        {
            var users = new List<string>();
            var uid = RqlGodot.UidOf(resPath);
            // Saved files first (the edited scene's file too: unsaved changes may still be discarded), then the live nodes.
            if (EditorInterface.Singleton.GetResourceFilesystem()?.GetFilesystem() is { } dir)
                foreach (var (path, _) in RqlGodot.WalkFiles(dir))
                {
                    if (string.Equals(path, resPath, StringComparison.OrdinalIgnoreCase) || !CanDepend(path)) continue;
                    if (ResourceLoader.GetDependencies(path).Any(d => ResPathNormalizer.DependencyNames(d, resPath, uid))) users.Add(path);
                }
            users.AddRange(MentionsOf(resPath).Where(m => !users.Contains(m)).ToList());
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
