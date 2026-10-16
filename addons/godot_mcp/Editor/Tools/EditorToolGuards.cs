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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Shared guard helpers for the editor-only (<c>#if TOOLS</c>) tool families. These centralize the
    /// small, repeated precondition checks several <c>Tool_*</c> handlers previously performed inline:
    /// resolving the edited scene root / editor resource filesystem (throwing a consistent error when the
    /// editor is in a state that makes the operation impossible), asserting a resource exists, validating a
    /// ClassDB class name before instantiating it, and creating a target directory on demand. Behavior is
    /// identical to the inlined checks they replace — this is a pure de-duplication.
    ///
    /// <para>
    /// Every helper touches a Godot editor/native API (<see cref="EditorInterface"/>, <see cref="ClassDB"/>,
    /// <see cref="DirAccess"/>, <see cref="ResourceLoader"/>), so the whole class lives behind
    /// <c>#if TOOLS</c> and is exercised via the headless Godot smoke (test.md Suite 3), not the plain-xUnit
    /// host. Callers must already be on the editor main thread (the <c>Tool_*</c> handlers invoke these from
    /// inside their <c>MainThread.Instance.Run(...)</c> body).
    /// </para>
    /// </summary>
    internal static class EditorToolGuards
    {
        /// <summary>
        /// The currently-edited scene root, or throw when no scene is open. Pass <paramref name="message"/>
        /// to override the default "no edited scene" text for a call site with a more specific intent.
        /// Main-thread only.
        /// </summary>
        internal static Node GetEditedSceneRootOrThrow(string? message = null)
            => EditorInterface.Singleton.GetEditedSceneRoot()
                ?? throw new Exception(message ?? "No scene is currently being edited.");

        /// <summary>
        /// The editor's resource filesystem, or throw when it is unavailable. Main-thread only.
        /// </summary>
        internal static EditorFileSystem GetResourceFileSystemOrThrow()
            => EditorInterface.Singleton.GetResourceFilesystem()
                ?? throw new Exception("Editor resource filesystem is not available.");

        /// <summary>
        /// Throw an <see cref="ArgumentException"/> (attributed to <paramref name="paramName"/>) when no
        /// resource exists at <paramref name="resPath"/>. Pass <paramref name="message"/> to override the
        /// default text for a call site that phrases the miss differently. Main-thread only.
        /// </summary>
        internal static void RequireResourceExists(string resPath, string paramName, string? message = null)
        {
            if (!ResourceLoader.Exists(resPath))
                throw new ArgumentException(message ?? $"No resource exists at '{resPath}'.", paramName);
        }

        /// <summary>
        /// Validate a Godot <paramref name="className"/> and instantiate it as <typeparamref name="T"/>,
        /// throwing an <see cref="ArgumentException"/> (attributed to <paramref name="paramName"/>) when the
        /// class is unknown, cannot be instantiated, does not derive from
        /// <paramref name="requireParentClass"/> (when supplied), or does not instantiate to
        /// <typeparamref name="T"/>. Returns the validated, freshly-instantiated instance. Main-thread only.
        /// </summary>
        internal static T ValidateClassDbInstantiation<[MustBeVariant] T>(
            string className, string paramName, string? requireParentClass = null)
            where T : GodotObject
        {
            if (!ClassDB.ClassExists(className))
                throw new ArgumentException($"Unknown Godot class '{className}'.", paramName);
            if (!ClassDB.CanInstantiate(className))
                throw new ArgumentException($"Godot class '{className}' cannot be instantiated (abstract/virtual).", paramName);
            if (requireParentClass != null && !ClassDB.IsParentClass(className, requireParentClass))
                throw new ArgumentException($"Godot class '{className}' does not derive from {requireParentClass}.", paramName);

            return ClassDB.Instantiate(className).As<T>()
                ?? throw new ArgumentException($"Godot class '{className}' did not instantiate to a {typeof(T).Name}.", paramName);
        }

        /// <summary>
        /// Keep Godot's resource cache in step with a file that was moved (<paramref name="newPath"/>) or deleted
        /// (<c>null</c>); otherwise a loaded instance stays bound to the old path. Main-thread only.
        /// </summary>
        internal static void RebindCachedResource(string oldPath, string? newPath)
        {
            // CacheMode.Reuse returns the cached instance without touching the (moved or deleted) file.
            if (!ResourceLoader.HasCached(oldPath)
                || ResourceLoader.Load(oldPath, cacheMode: ResourceLoader.CacheMode.Reuse) is not { } cached)
                return;
            Rebind(cached, newPath);

            // Built-in sub-resources are cached as '<old>::<id>' too; carry them along (or unbind them).
            var prefix = oldPath + "::";
            var seen = new HashSet<ulong>();
            var pending = new Stack<Resource>();
            pending.Push(cached);
            while (pending.Count > 0)
            {
                var owner = pending.Pop();
                if (!seen.Add(owner.GetInstanceId()))
                    continue;
                foreach (var info in owner.GetPropertyList())
                {
                    if ((info["usage"].As<PropertyUsageFlags>() & PropertyUsageFlags.Storage) != 0)
                        CollectSubResources(owner.Get(info["name"].AsStringName()), prefix, newPath, pending);
                }
            }
        }

        static void Rebind(Resource resource, string? newPath)
        {
            if (newPath == null)
                resource.ResourcePath = string.Empty;
            else
                resource.TakeOverPath(newPath);
        }

        static void CollectSubResources(Variant value, string oldPrefix, string? newPath, Stack<Resource> pending)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Object when value.AsGodotObject() is Resource sub && sub.ResourcePath.StartsWith(oldPrefix, StringComparison.Ordinal):
                    Rebind(sub, newPath == null ? null : newPath + "::" + sub.ResourcePath.Substring(oldPrefix.Length));
                    pending.Push(sub);
                    break;
                case Variant.Type.Array:
                    foreach (var item in value.AsGodotArray())
                        CollectSubResources(item, oldPrefix, newPath, pending);
                    break;
                case Variant.Type.Dictionary:
                    foreach (var entry in value.AsGodotDictionary())
                    {
                        CollectSubResources(entry.Key, oldPrefix, newPath, pending);
                        CollectSubResources(entry.Value, oldPrefix, newPath, pending);
                    }
                    break;
            }
        }

        /// <summary>The UID file Godot 4.4+ keeps next to scripts, shaders and other non-imported resources.</summary>
        internal const string UidSidecarSuffix = ".uid";

        /// <summary>Metadata files Godot keeps next to a resource file: import settings and (4.4+) its UID.</summary>
        internal static readonly string[] Sidecars = { ReimportClassifier.ImportSidecarSuffix, UidSidecarSuffix };

        /// <summary>
        /// Point open scenes at a moved scene file: the edited scene (and, on Godot 4.4+, every open scene tab)
        /// whose root — or an instance inside it — still names <paramref name="oldPath"/>. The editor saves a
        /// scene to its root's <c>scene_file_path</c>, so without this the next save recreated the old file.
        /// </summary>
        internal static void RetargetOpenScenes(string oldPath, string newPath)
        {
            // scene_file_path is stored simplified; a caller path like 'res://a/./b.tscn' must still match.
            oldPath = oldPath.SimplifyPath();
            newPath = newPath.SimplifyPath();
            var editor = EditorInterface.Singleton;
            var pending = new Stack<Node>();
            // get_open_scene_roots (Godot 4.4+, which includes the edited scene) is called by name because the
            // addon compiles against 4.3; there, only the edited scene is reachable.
            if (editor.HasMethod("get_open_scene_roots"))
            {
                foreach (var root in editor.Call("get_open_scene_roots").AsGodotArray())
                {
                    if (root.AsGodotObject() is Node node)
                        pending.Push(node);
                }
            }
            else if (editor.GetEditedSceneRoot() is { } edited)
            {
                pending.Push(edited);
            }

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node.SceneFilePath == oldPath)
                    node.SceneFilePath = newPath;
                foreach (var child in node.GetChildren())
                    pending.Push(child);
            }
        }

        /// <summary>
        /// Close the edited scene when its file was just deleted, so the next save cannot recreate it (the editor
        /// keeps the open scene's path). Uses <c>close_scene</c> (Godot 4.4+, called by name: the addon compiles
        /// against 4.3); on 4.3, and for scenes open in background tabs, nothing can be done from a tool.
        /// </summary>
        internal static void CloseDeletedScene(string deletedPath)
        {
            var editor = EditorInterface.Singleton;
            if (editor.GetEditedSceneRoot()?.SceneFilePath == deletedPath.SimplifyPath() && editor.HasMethod("close_scene"))
                editor.Call("close_scene");
        }

        /// <summary>
        /// Ensure <paramref name="absDir"/> exists, creating it recursively when missing and throwing on
        /// failure. Mirrors the "ResourceSaver/FileAccess does not create parent dirs" guard the create-style
        /// tools share. Main-thread only.
        /// </summary>
        internal static void EnsureDirectoryExists(string absDir)
        {
            if (!DirAccess.DirExistsAbsolute(absDir))
            {
                var mkErr = DirAccess.MakeDirRecursiveAbsolute(absDir);
                if (mkErr != Error.Ok)
                    throw new Exception($"Failed to create target directory '{absDir}': {mkErr}.");
            }
        }
    }
}
#endif
