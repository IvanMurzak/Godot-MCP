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
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        [RqlCallable("create a scene file (res://….tscn) with a root node of a Godot class, save it and open it; returns the new root node")]
        public Node NewScene(string path, string rootClass = "Node", string? rootName = null)
        {
            path = NewScenePath(path);
            var root = EditorToolGuards.ValidateClassDbInstantiation<Node>(RootClass(rootClass), "rootClass", requireParentClass: "Node");
            if (!string.IsNullOrEmpty(rootName)) root.Name = rootName;
            try
            {
                var packed = new PackedScene();
                var packError = packed.Pack(root);
                if (packError != Error.Ok) throw new InvalidOperationException($"could not pack the new scene: {packError}");
                EditorToolGuards.EnsureDirectoryExists(ResPathNormalizer.ParentDir(path));
                var saveError = ResourceSaver.Save(packed, path);
                if (saveError != Error.Ok) throw new InvalidOperationException($"could not save the new scene to '{path}': {saveError}");
            }
            finally { root.Free(); }
            Ei.GetResourceFilesystem()?.UpdateFile(path);
            return OpenScene(path);
        }

        [RqlCallable("open a scene file (res://….tscn) and make it the edited scene; returns its root node")]
        public Node OpenScene(string path)
        {
            path = RequireScenePath(path);
            EditorToolGuards.RequireResourceExists(path, nameof(path), $"no scene at '{path}'");
            Ei.OpenSceneFromPath(path);
            return RqlGodot.EditedRoot() ?? throw new InvalidOperationException($"opened '{path}' but the editor has no edited scene");
        }

        [RqlCallable("save the edited scene to its file, or to a new res://….tscn path (save as); returns the saved path")]
        public string SaveScene(string? path = null)
        {
            var root = RqlGodot.EditedRoot() ?? throw new InvalidOperationException("no scene is being edited; nothing to save");
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = RequireScenePath(path!);
                // Save-as onto another file would replace that scene (Godot overwrites without asking here).
                if (path != root.SceneFilePath && FileAccess.FileExists(path))
                    throw new ArgumentException($"a file already exists at '{path}'; save to a new path, or delete that file first with DeleteResource");
                EditorToolGuards.EnsureDirectoryExists(ResPathNormalizer.ParentDir(path));
                Ei.SaveSceneAs(path);
                var saved = RqlGodot.EditedRoot()?.SceneFilePath;
                if (saved != path) throw new InvalidOperationException($"save-as to '{path}' did not take effect (the scene path is '{saved ?? "none"}')");
                return path!;
            }
            var existing = root.SceneFilePath;
            if (string.IsNullOrEmpty(existing)) throw new InvalidOperationException("the edited scene was never saved; pass a res:// path: SaveScene(\"res://scenes/x.tscn\")");
            // Saving back would silently recreate a scene file someone deleted (the editor keeps it open before Godot 4.5).
            if (!FileAccess.FileExists(existing))
                throw new InvalidOperationException($"the scene file '{existing}' was deleted; pass a path to save as: SaveScene(\"res://scenes/x.tscn\")");
            var error = Ei.SaveScene();
            if (error != Error.Ok) throw new InvalidOperationException($"could not save '{existing}': {error}");
            return existing;
        }

        [RqlCallable("save every open scene that has a file")]
        public void SaveAllScenes() => Ei.SaveAllScenes();

        /// <summary>The <c>scenes</c> source: the scenes open in the editor (tabs), the edited one marked active.</summary>
        public static List<RqlScene> OpenScenes()
        {
            var active = RqlGodot.EditedRoot()?.SceneFilePath ?? "";
            var list = new List<RqlScene>();
            foreach (var path in Ei.GetOpenScenes())
                list.Add(new RqlScene(path, path == active));
            if (active.Length == 0 && RqlGodot.EditedRoot() != null) list.Add(new RqlScene("", true));
            return list;
        }

        // Call validator (RQL 0.2, run before the call, on dry runs too): nothing is created or opened when it rejects.
        public string? ValidateNewScene(string path, string rootClass, string? rootName) => RqlGodot.Rejection(() =>
        {
            NewScenePath(path);
            EditorToolGuards.CheckClassDbInstantiation(RootClass(rootClass), "rootClass", requireParentClass: "Node");
        });

        private static string RootClass(string rootClass) => string.IsNullOrWhiteSpace(rootClass) ? "Node" : rootClass;

        private static string NewScenePath(string path)
        {
            path = RequireScenePath(path);
            if (FileAccess.FileExists(path)) throw new ArgumentException($"a file already exists at '{path}' (open it with OpenScene)");
            return path;
        }

        /// <summary>The trimmed path when it names a res:// scene file (.tscn / .scn); throws otherwise.</summary>
        private static string RequireScenePath(string path)
        {
            var p = ResPathNormalizer.RequireResFilePath(path, nameof(path));
            if (!ResPathNormalizer.IsScenePath(p)) throw new ArgumentException($"'{path}' is not a scene path ending in .tscn or .scn");
            return p;
        }
    }

    /// <summary>One scene open in the editor (the <c>scenes</c> source).</summary>
    public sealed class RqlScene
    {
        public RqlScene(string path, bool active) { this.path = path; this.active = active; }

        [RqlDescription("res:// path of the scene file (empty for a scene never saved)")]
        public string path { get; }

        [RqlDescription("file name without extension")]
        public string name => path.Length == 0 ? "[unsaved]" : System.IO.Path.GetFileNameWithoutExtension(path);

        [RqlDescription("true for the scene being edited")]
        public bool active { get; }

        [RqlDescription("root node of the edited scene (null for the other open scenes)")]
        [RqlReference]
        public Node? root => active ? RqlGodot.EditedRoot() : null;

        [RqlCallable("make this the edited scene; returns its root node")]
        public Node Open() => active && RqlGodot.EditedRoot() is { } root ? root : RqlEditorApi.Instance.OpenScene(path);

        [RqlCallable("save this scene (it becomes the edited scene first); returns the saved path")]
        public string Save()
        {
            if (path.Length == 0) throw new InvalidOperationException("this scene was never saved; use editor | call SaveScene(\"res://…tscn\")");
            Open();
            return RqlEditorApi.Instance.SaveScene();
        }
    }
}
#endif
