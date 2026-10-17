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
using System.Globalization;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The RQL engine over the live Godot editor — the Godot counterpart of Unity-MCP's <c>RqlUnity</c>: engine factory,
    /// stable references (<c>Node3D#&lt;instance id&gt;</c>), sources and functions, and the editor integration for writes
    /// (one <see cref="EditorUndoRedoManager"/> action per mutation, the scene marked unsaved, edited resource files saved;
    /// see <c>RqlGodot.Save.cs</c>). Sources, functions and adapters come from modules: <see cref="Modules.GodotCoreModule"/>,
    /// <see cref="Modules.ToolTypesModule"/> and any <see cref="IRqlModule"/> in a loaded assembly (an extension's module in
    /// the consumer project assembly is found the same way). One engine per editor session: <see cref="Reset"/> drops it on
    /// plugin teardown / assembly reload. Every call must run on the main thread.
    /// </summary>
    public static partial class RqlGodot
    {
        /// <summary>Most resources <c>godot.resources(...)</c> loads per call (loading resources is expensive).</summary>
        public const int MaxResources = 200;

        /// <summary>Most entries the <c>files</c> source returns.</summary>
        public const int MaxFiles = 20000;

        private static RqlEngine? _engine;

        public static RqlEngine Engine => _engine ??= Create();

        /// <summary>
        /// Drop the engine and everything cached from it (tool signatures, mutation state). Returns whether an engine had
        /// been created since the last reset — only then can RQL's process-wide caches hold types of this collectible
        /// assembly (see <see cref="RqlStaticCaches"/>). Called on plugin teardown.
        /// </summary>
        public static bool Reset()
        {
            var used = _engine != null;
            _engine = null;
            RqlToolBinding.Instance.Reset();
            ResetState();
            return used;
        }

        public static RqlEngine Create()
        {
            var engine = new RqlEngine(AccessorMode.Compiled) { RuntimeName = "godot" };
            engine.ResolveStableRef = ResolveStableRef;

            engine.Mutations.Enabled = true;
            // One undo action per mutation: BeforeWrite snapshots the Godot properties of every object about to change,
            // Committed turns the differences into do/undo properties (structural calls such as AddChild / Free register
            // their own do/undo methods); Mutate commits the action once the query finished (RqlGodot.Save.cs).
            engine.Mutations.BeforeWrite = BeforeWrite;
            engine.Mutations.Committed = Committed;

            // Every module in the loaded assemblies (an extension's module compiled into the consumer project assembly
            // included); RQL reports a module or assembly that fails to load instead of skipping it silently.
            var reports = engine.AddModulesFrom(GodotAssemblyUtils.AllAssemblies,
                assembly => GodotAssemblyUtils.IsSkippedForDiscovery(assembly, typeof(RqlGodot).Assembly));
            foreach (var failed in reports.Where(r => !r.Ok))
                GodotMcpLog.Warning($"[Godot-MCP] RQL module skipped: {failed}");
            return engine;
        }

        /// <summary><c>Type#id</c> where id is the Godot instance id; the engine checks the type.</summary>
        private static object? ResolveStableRef(string type, string id)
        {
            if (!ulong.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var instanceId)) return null;
            if (!GodotObject.IsInstanceIdValid(instanceId)) return null;
            return GodotObject.InstanceFromId(instanceId);
        }

        // ------------------------------------------------------------ sources

        /// <summary>The root of the scene being edited, or null when no scene is open.</summary>
        public static Node? EditedRoot() => EditorInterface.Singleton.GetEditedSceneRoot();

        /// <summary>The edited scene root as a one-item list (empty when no scene is open).</summary>
        public static List<Node> Roots() => EditedRoot() is { } root ? new List<Node> { root } : new List<Node>();

        /// <summary>Every node of the edited scene, depth-first, root first, internal children excluded.</summary>
        public static List<Node> AllNodes()
        {
            var all = new List<Node>();
            if (EditedRoot() is { } root) Collect(root, all);
            return all;
        }

        private static void Collect(Node node, List<Node> sink)
        {
            sink.Add(node);
            var count = node.GetChildCount(includeInternal: false);
            for (int i = 0; i < count; i++)
                if (node.GetChild(i, includeInternal: false) is { } child) Collect(child, sink);
        }

        /// <summary>The nodes selected in the editor.</summary>
        public static List<Node> SelectedNodes() =>
            EditorInterface.Singleton.GetSelection().GetSelectedNodes().Where(n => n != null && GodotObject.IsInstanceValid(n)).ToList();

        /// <summary>The path of <paramref name="node"/> below the edited scene root ("." for the root itself).</summary>
        public static string PathOf(Node node)
        {
            var root = EditedRoot();
            if (root == null || !GodotObject.IsInstanceValid(root)) return node.GetPath().ToString();
            if (node == root) return ".";
            return root.IsAncestorOf(node) ? root.GetPathTo(node).ToString() : node.GetPath().ToString();
        }

        // ------------------------------------------------------------ functions

        /// <summary>
        /// <c>godot.find("Player")</c> / <c>godot.find("Level/Player")</c>: the node of the edited scene at that path (below
        /// the root, the root's own name allowed as a prefix) or with that unique name; an error when there is none or several.
        /// </summary>
        public static object? FindNode(object?[] args)
        {
            if (args.Length != 1 || !(args[0] is string key) || key.Length == 0)
                return new RqlError("type-mismatch", "godot.find(name) expects a node name or a path below the scene root such as \"Level/Player\"");
            return TryFindNode(key, out var node, out var error) ? node : new RqlError(error!.Value.code, error.Value.message);
        }

        /// <summary>The node <paramref name="key"/> names (path below the edited root, or unique name).</summary>
        internal static bool TryFindNode(string key, out Node? node, out (string code, string message)? error)
        {
            node = null;
            error = null;
            var root = EditedRoot();
            if (root == null)
            {
                error = ("not-found", "no scene is being edited; open one with editor | call OpenScene(\"res://…\")");
                return false;
            }
            if (Tool_Node.FindByPath(root, key) is { } byPath) { node = byPath; return true; }
            if (key.IndexOf('/') < 0)
            {
                // A unique name anywhere in the scene: stop at the second match.
                Node? first = null, second = null;
                FindNamed(root, key, ref first, ref second);
                if (first != null && second == null) { node = first; return true; }
                if (second != null)
                {
                    error = ("ambiguous", $"several nodes are named '{key}': use the path below the scene root, e.g. \"{PathOf(first!)}\" or \"{PathOf(second)}\", or a ref");
                    return false;
                }
            }
            error = ("not-found", $"no node at '{key}' in the edited scene '{root.Name}'");
            return false;
        }

        private static void FindNamed(Node node, string name, ref Node? first, ref Node? second)
        {
            if (node.Name.ToString() == name)
            {
                if (first == null) first = node;
                else { second = node; return; }
            }
            var count = node.GetChildCount(includeInternal: false);
            for (int i = 0; i < count && second == null; i++)
                if (node.GetChild(i, includeInternal: false) is { } child) FindNamed(child, name, ref first, ref second);
        }

        /// <summary>
        /// <c>godot.resources("Texture2D", "res://art")</c>: resources whose type is (or derives from) the given Godot class,
        /// below an optional folder (at most <see cref="MaxResources"/>, loaded as needed). An empty type matches every file
        /// the editor recognises as a resource.
        /// </summary>
        public static object? FindResources(object?[] args)
        {
            if (args.Length < 1 || args.Length > 2 || !(args[0] is string type) || (args.Length == 2 && !(args[1] is string)))
                return new RqlError("type-mismatch", "godot.resources(type, folder?) expects a Godot class such as \"StandardMaterial3D\" and an optional res:// folder");
            var folder = args.Length == 2 ? (string)args[1]! : null;
            var efs = EditorToolGuards.GetResourceFileSystemOrThrow();
            string scope;
            try { scope = ResPathNormalizer.NormalizeDir(folder); }
            catch (ArgumentException ex) { return new RqlError("type-mismatch", ex.Message); }
            var dir = efs.GetFilesystemPath(scope);
            if (dir == null) return new RqlError("not-found", $"folder '{scope}' is not in the project filesystem");
            return LoadResources(ResourceFiles(dir, type)).Take(MaxResources);
        }

        // Lazy: a query that needs only a few resources (… | first 3) loads only those.
        private static IEnumerable<Resource> LoadResources(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                if (ResourceLoader.Load(path) is { } resource) yield return resource;
        }

        // The files of the requested type: the type check runs once per distinct file type (ClassDB lookups), not per file.
        private static IEnumerable<string> ResourceFiles(EditorFileSystemDirectory dir, string type)
        {
            var matches = new Dictionary<string, bool>(StringComparer.Ordinal);
            var typeIsClass = type.Length > 0 && ClassDB.ClassExists(type);
            foreach (var (path, fileType) in WalkFiles(dir))
            {
                if (!matches.TryGetValue(fileType, out var match))
                    matches[fileType] = match = TypeMatches(fileType, type, typeIsClass);
                if (match) yield return path;
            }
        }

        /// <summary>Every file below <paramref name="dir"/> with its importer-assigned type, depth-first, lazily.</summary>
        internal static IEnumerable<(string path, string type)> WalkFiles(EditorFileSystemDirectory dir)
        {
            var fileCount = dir.GetFileCount();
            for (int i = 0; i < fileCount; i++)
                yield return (dir.GetFilePath(i), dir.GetFileType(i).ToString());
            var subCount = dir.GetSubdirCount();
            for (int i = 0; i < subCount; i++)
                if (dir.GetSubdir(i) is { } sub)
                    foreach (var file in WalkFiles(sub)) yield return file;
        }

        /// <summary>A file's importer-assigned type equals or derives from <paramref name="type"/> (empty = any resource).</summary>
        private static bool TypeMatches(string fileType, string type, bool typeIsClass)
        {
            if (string.IsNullOrEmpty(fileType)) return false;
            if (type.Length == 0) return ClassDB.ClassExists(fileType) && ClassDB.IsParentClass(fileType, "Resource");
            if (string.Equals(fileType, type, StringComparison.OrdinalIgnoreCase)) return true;
            return typeIsClass && ClassDB.ClassExists(fileType) && ClassDB.IsParentClass(fileType, type);
        }

        /// <summary><c>godot.resource("res://materials/red.tres")</c> (or a <c>uid://</c>): the resource at a path, or null.</summary>
        public static object? LoadResource(object?[] args)
        {
            if (args.Length != 1 || !(args[0] is string path) || path.Length == 0)
                return new RqlError("type-mismatch", "godot.resource(path) expects a res:// or uid:// path");
            return TryLoadResource(path, out var resource, out _) ? resource : null;
        }

        /// <summary>Load the resource at a res:// or uid:// path; the reason when there is none.</summary>
        internal static bool TryLoadResource(string path, out Resource? resource, out string? reason)
        {
            resource = null;
            reason = null;
            if (!path.StartsWith("res://", StringComparison.Ordinal) && !path.StartsWith("uid://", StringComparison.Ordinal))
            {
                reason = $"'{path}' is not a res:// or uid:// path";
                return false;
            }
            resource = Tool_Resource.LoadByPath(path, out reason);
            return resource != null;
        }
    }
}
#endif
