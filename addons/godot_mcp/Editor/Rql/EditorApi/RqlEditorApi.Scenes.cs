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
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        [RqlCallable("create a scene file (res://….tscn) with a root node of a Godot class, save it and open it; returns the new root node. " +
            "A scene path as rootClass (\"res://enemies/enemy.tscn\") makes an inherited scene of it (Scene → New Inherited Scene)")]
        public Node NewScene(string path, string rootClass = "Node", string? rootName = null)
        {
            path = NewScenePath(path);
            if (IsBaseScene(rootClass)) return NewInheritedScene(path, rootClass, rootName);
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
            RqlReimport.Register(path);
            EditorToolGuards.ReloadReplacedOpenScene(path);
            return OpenScene(path);
        }

        // An inherited scene is a scene whose root instances the base scene; the file is what the editor writes for one.
        private Node NewInheritedScene(string path, string basePath, string? rootName)
        {
            basePath = BaseScenePath(basePath, path);
            var name = string.IsNullOrWhiteSpace(rootName) ? BaseRootName(basePath) : rootName!;
            if (NodeNameRules.Problem(name, _ => false) is { } problem) throw new ArgumentException(problem, nameof(rootName));
            if (!path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"an inherited scene is written as text: give it a .tscn path, not '{path}'", nameof(path));
            var uid = RqlGodot.UidOf(basePath);
            var text = "[gd_scene load_steps=2 format=3]\n\n" +
                       $"[ext_resource type=\"PackedScene\"{(uid != null ? $" uid=\"{uid}\"" : "")} path=\"{basePath.CEscape()}\" id=\"1_base\"]\n\n" +
                       $"[node name=\"{name.CEscape()}\" instance=ExtResource(\"1_base\")]\n";
            EditorToolGuards.EnsureDirectoryExists(ResPathNormalizer.ParentDir(path));
            using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write)
                ?? throw new InvalidOperationException($"could not write '{path}': {FileAccess.GetOpenError()}"))
                file.StoreString(text);
            RqlReimport.Register(path);
            EditorToolGuards.ReloadReplacedOpenScene(path);
            return OpenScene(path);
        }

        private static bool IsBaseScene(string rootClass) =>
            !string.IsNullOrWhiteSpace(rootClass) && (rootClass.Contains("://") || ResPathNormalizer.IsScenePath(rootClass.Trim()));

        private static string BaseScenePath(string basePath, string path)
        {
            basePath = RequireScenePath(basePath);
            EditorToolGuards.RequireResourceExists(basePath, "rootClass", $"no scene at '{basePath}' to inherit from");
            if (basePath == path) throw new ArgumentException("a scene cannot inherit from itself", "rootClass");
            return basePath;
        }

        // The base scene's root name (what the editor names an inherited scene's root).
        private static string BaseRootName(string basePath)
        {
            var state = ResourceLoader.Load<PackedScene>(basePath)?.GetState();
            return state != null && state.GetNodeCount() > 0 ? state.GetNodeName(0).ToString() : System.IO.Path.GetFileNameWithoutExtension(basePath);
        }

        [RqlCallable("reload a scene from its file, discarding its unsaved changes (Scene → Reload Saved Scene): the edited scene, or the " +
            "scene at path (opened when it is not); returns its root node")]
        public Node ReloadScene(string? path = null)
        {
            string target;
            if (!string.IsNullOrWhiteSpace(path)) target = RequireScenePath(path!);
            else
            {
                target = RqlGodot.EditedRoot()?.SceneFilePath ?? "";
                if (target.Length == 0) throw new InvalidOperationException("no saved scene is being edited; pass a res:// scene path");
            }
            EditorToolGuards.RequireResourceExists(target, nameof(path), $"no scene at '{target}'");
            if (Array.IndexOf(Ei.GetOpenScenes(), target) < 0) return OpenScene(target);
            if (RqlGodot.EditedRoot()?.SceneFilePath != target) OpenScene(target);
            EditorToolGuards.ReloadReplacedOpenScene(target); // replaces the cached PackedScene, then reloads the tab
            var root = RqlGodot.EditedRoot() ?? throw new InvalidOperationException($"reloaded '{target}' but the editor has no edited scene");
            if (root.SceneFilePath != target)
                throw new InvalidOperationException($"reloaded '{target}' but the editor shows '{root.SceneFilePath}'");
            return root;
        }

        [RqlCallable("open a scene file (res://….tscn) and make it the edited scene; returns its root node")]
        public Node OpenScene(string path)
        {
            path = RequireScenePath(path);
            EditorToolGuards.RequireResourceExists(path, nameof(path), $"no scene at '{path}'");
            Ei.OpenSceneFromPath(path);
            var root = RqlGodot.EditedRoot() ?? throw new InvalidOperationException($"opened '{path}' but the editor has no edited scene");
            if (root.SceneFilePath != path)
                throw new InvalidOperationException($"opened '{path}' but the editor shows '{root.SceneFilePath}'");
            return root;
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
                RqlReimport.Register(path!);
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

        public string? ValidateReloadScene(string? path) => RqlGodot.Rejection(() =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (string.IsNullOrEmpty(RqlGodot.EditedRoot()?.SceneFilePath)) throw new InvalidOperationException("no saved scene is being edited; pass a res:// scene path");
                return;
            }
            var target = RequireScenePath(path!);
            EditorToolGuards.RequireResourceExists(target, nameof(path), $"no scene at '{target}'");
        });

        // Call validator (RQL 0.2, run before the call, on dry runs too): nothing is created or opened when it rejects.
        public string? ValidateNewScene(string path, string rootClass, string? rootName) => RqlGodot.Rejection(() =>
        {
            path = NewScenePath(path);
            if (IsBaseScene(rootClass))
            {
                BaseScenePath(rootClass, path);
                if (!path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"an inherited scene is written as text: give it a .tscn path, not '{path}'", nameof(path));
            }
            else EditorToolGuards.CheckClassDbInstantiation(RootClass(rootClass), "rootClass", requireParentClass: "Node");
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
            return p.SimplifyPath(); // the form Godot stores as scene_file_path ('res://a/./b.tscn' -> 'res://a/b.tscn')
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
