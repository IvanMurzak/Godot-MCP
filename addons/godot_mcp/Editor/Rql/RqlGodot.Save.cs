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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The write side of the engine: undo, dirty marking and saving.
    /// <list type="bullet">
    /// <item><b>Undo.</b> One <see cref="EditorUndoRedoManager"/> action per <see cref="Mutate"/> (two when it touched both
    /// scene nodes and other objects: Godot keeps one undo history per scene plus a global one). <c>set</c>: the Godot
    /// properties (storage usage) of every changed object are snapshotted before the write and the differences become
    /// do/undo properties. Structural calls (AddChild, InstantiateScene, Duplicate, Free, Reparent, MoveChild) apply their
    /// change immediately and register do/undo methods through <see cref="RecordUndo"/>. Actions are committed with
    /// <c>execute: false</c> because the change is already applied. The steps' do parts are registered in the order the
    /// changes happened and their undo parts in reverse, so undoing a mutation of several dependent steps (two MoveChild
    /// calls, several siblings freed) restores the original state.</item>
    /// <item><b>Saving</b> — the same policy as the replaced <c>node-modify</c> / <c>resource-modify</c> tools: a change to a
    /// node of the edited scene marks the scene unsaved (it is written by <c>editor | call SaveScene()</c>, like Ctrl+S); a
    /// change to a resource stored in its own file (<c>res://….tres</c>) is saved to that file after the mutation; a built-in
    /// (scene-embedded) resource marks the scene unsaved. Nothing is saved after a dry run or a mutation that applied
    /// nothing.</item>
    /// </list>
    /// </summary>
    public static partial class RqlGodot
    {
        private static int _mutating;

        /// <summary>One undoable step: the operations that redo it and the ones that undo it (either may be null).</summary>
        private readonly struct UndoStep
        {
            public UndoStep(Action<EditorUndoRedoManager>? redo, Action<EditorUndoRedoManager>? undo) { Redo = redo; Undo = undo; }
            public Action<EditorUndoRedoManager>? Redo { get; }
            public Action<EditorUndoRedoManager>? Undo { get; }
        }

        // Per running Mutate: Godot property snapshots taken before the writes, the undo steps collected so far (scene
        // history / other history), the file resources to save afterwards and the nodes removed (kept alive by the undo
        // history; freed when there is none).
        private static readonly Dictionary<GodotObject, Dictionary<string, Variant>> _snapshots = new Dictionary<GodotObject, Dictionary<string, Variant>>();
        private static readonly List<UndoStep> _sceneUndo = new List<UndoStep>();
        private static readonly List<UndoStep> _otherUndo = new List<UndoStep>();
        private static readonly List<Resource> _toSave = new List<Resource>();
        private static readonly HashSet<Resource> _toSaveSet = new HashSet<Resource>();
        private static readonly HashSet<Node> _removed = new HashSet<Node>();
        private static bool _settingsChanged;
        // Objects whose change registered its own do/undo (structural edits such as adding a TileSet source or a Theme
        // item): their property snapshot is not turned into undo properties (setting a removed entry to null does not
        // remove it again), they are only marked changed / saved.
        private static readonly HashSet<GodotObject> _explicitUndo = new HashSet<GodotObject>();

        // Sub-resources an adapter reached through their container (a TileSet's atlas sources): a new sub-resource has no
        // "file::id" path until its file is loaded again, so this is how its changes find the file to save. The value is the
        // container's instance id, not the container: a strong value would keep the container alive, which keeps the
        // sub-resource (the key) alive natively, so neither would ever be freed.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Resource, System.Runtime.CompilerServices.StrongBox<ulong>> _containers =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Resource, System.Runtime.CompilerServices.StrongBox<ulong>>();

        /// <summary>
        /// The stored properties of <paramref name="o"/> as the running mutation saw them before its first write (its undo
        /// snapshot), or read now when there is none.
        /// </summary>
        internal static Dictionary<string, Variant> SnapshotOf(GodotObject o) =>
            _snapshots.TryGetValue(o, out var snapshot) ? snapshot : StoredProperties(o);

        /// <summary>Record that <paramref name="sub"/> is stored inside <paramref name="container"/> (saved with it).</summary>
        internal static void RegisterContainer(Resource sub, Resource container) =>
            _containers.AddOrUpdate(sub, new System.Runtime.CompilerServices.StrongBox<ulong>(container.GetInstanceId()));

        private static void ResetState()
        {
            _mutating = 0;
            _snapshots.Clear();
            _sceneUndo.Clear();
            _otherUndo.Clear();
            _toSave.Clear();
            _toSaveSet.Clear();
            _removed.Clear();
            _settingsChanged = false;
            _explicitUndo.Clear();
        }

        /// <summary>
        /// Run a set / call query, then commit its undo action and save the edited resource files (see the class summary).
        /// Saving problems become <c>not-saved</c> warnings on the receipt, never a tool error: the change is applied.
        /// </summary>
        public static QueryResult Mutate(string query, bool dryRun = false)
        {
            if (_mutating == 0) ResetState();
            _mutating++;
            QueryResult result;
            try
            {
                result = Engine.Execute(query, null, new QueryOptions { AllowMutation = true, DryRun = dryRun });
            }
            finally
            {
                _mutating--;
                if (_mutating == 0) CommitUndo();
            }
            if (_mutating > 0 || dryRun) return result;

            List<string> problems;
            try { problems = SaveResources(); SaveProjectSettings(problems); }
            catch (Exception ex) { problems = new List<string> { $"saving failed: {ex.GetType().Name}: {ex.Message}" }; }
            foreach (var problem in problems)
                result.Diagnostics.Add(Diagnostic.Warning("not-saved", problem));
            return result;
        }

        /// <summary>
        /// Register the do and the undo operations of a structural scene change a call already applied; they join the
        /// running mutation's scene-history action (committed with <c>execute: false</c>; undo parts of later steps run
        /// first). Calls only run inside rql-mutate.
        /// </summary>
        internal static void RecordUndo(Action<EditorUndoRedoManager>? redo, Action<EditorUndoRedoManager>? undo) =>
            _sceneUndo.Add(new UndoStep(redo, undo));

        /// <summary>
        /// Register the do / undo operations of a structural change of <paramref name="owner"/> (a resource such as a TileSet or
        /// Theme) that a call or setter already applied. The owner's property snapshot is then not diffed into undo properties;
        /// it is still saved (file resource) or marks the scene unsaved (built-in resource).
        /// </summary>
        internal static void RecordUndoFor(GodotObject owner, Action<EditorUndoRedoManager>? redo, Action<EditorUndoRedoManager>? undo)
        {
            (IsSceneObject(owner) ? _sceneUndo : _otherUndo).Add(new UndoStep(redo, undo));
            _explicitUndo.Add(owner);
        }

        /// <summary>Mark the edited scene as having unsaved changes.</summary>
        internal static void MarkSceneUnsaved()
        {
            if (EditedRoot() != null) EditorInterface.Singleton.MarkSceneAsUnsaved();
        }

        // ------------------------------------------------------------ engine hooks

        private static void BeforeWrite(IReadOnlyList<MutationRecord> records)
        {
            foreach (var owner in MutatedObjects(records))
                if (!_snapshots.ContainsKey(owner))
                    _snapshots[owner] = StoredProperties(owner);
        }

        // Committed gets exactly the applied records (every set after all writes succeeded, the completed calls when a later
        // call threw); a set the engine rolled back never gets here.
        private static void Committed(IReadOnlyList<MutationRecord> records)
        {
            var sceneChanged = false;
            foreach (var owner in MutatedObjects(records))
            {
                var hasSnapshot = _snapshots.TryGetValue(owner, out var snapshot);
                var propertiesChanged = !hasSnapshot; // no snapshot (never in practice): assume it changed
                // A structural change registered its own undo for the entries it added or removed (setting a removed
                // entry to null does not remove it again); values that changed in place are still diffed.
                var explicitUndo = _explicitUndo.Contains(owner);
                if (explicitUndo) propertiesChanged = true;
                if (hasSnapshot)
                {
                    var after = StoredProperties(owner);
                    var changed = new List<(string name, Variant before, Variant after)>();
                    foreach (var kv in after)
                    {
                        var known = snapshot!.TryGetValue(kv.Key, out var old);
                        if (explicitUndo && !known) continue;
                        if (!known || !Same(old, kv.Value)) changed.Add((kv.Key, known ? old : default, kv.Value));
                    }
                    // A removed project setting (custom setting, input action, autoload) is restored by its undo.
                    if (!explicitUndo && Settings.RqlProject.Is(owner))
                        foreach (var kv in snapshot!)
                            if (!after.ContainsKey(kv.Key)) changed.Add((kv.Key, kv.Value, default));
                    if (changed.Count > 0)
                    {
                        propertiesChanged = true;
                        var target = owner;
                        // Undoing / redoing a project setting also writes project.godot, like the Project Settings dialog.
                        var save = Settings.RqlProject.Is(owner);
                        (IsSceneObject(owner) ? _sceneUndo : _otherUndo).Add(new UndoStep(
                            ur =>
                            {
                                foreach (var c in changed) ur.AddDoProperty(target, c.name, c.after);
                                if (save) ur.AddDoMethod(target, ProjectSettings.MethodName.Save);
                            },
                            ur =>
                            {
                                foreach (var c in changed) ur.AddUndoProperty(target, c.name, c.before);
                                if (save) ur.AddUndoMethod(target, ProjectSettings.MethodName.Save);
                            }));
                    }
                }

                switch (owner)
                {
                    case Node node when IsInEditedScene(node):
                        sceneChanged = true;
                        break;
                    case Resource resource when IsFileResource(resource):
                        // Saved only when a Godot property actually changed: writing the same value again does not
                        // rewrite the file (the replaced resource-modify's "no modifications, not re-saved" rule).
                        if (propertiesChanged && _toSaveSet.Add(resource)) _toSave.Add(resource);
                        break;
                    case Resource resource when propertiesChanged:
                        // A sub-resource built into a resource file (a TileSet's atlas source, a mesh's material): saved
                        // with that file. One built into a scene marks the scene unsaved.
                        if (ContainerFile(resource) is { } container) { if (_toSaveSet.Add(container)) _toSave.Add(container); }
                        else sceneChanged = true;
                        break;
                    case GodotObject settings when propertiesChanged && Settings.RqlProject.Is(settings):
                        _settingsChanged = true;
                        break;
                }
            }
            if (sceneChanged) MarkSceneUnsaved();
        }

        // The nearest Godot object enclosing each change (a write into a struct member is recorded on the node / resource
        // that holds it); view objects such as the editor source are not Godot objects and need no undo.
        private static List<GodotObject> MutatedObjects(IReadOnlyList<MutationRecord> records)
        {
            var owners = new List<GodotObject>();
            var seen = new HashSet<GodotObject>();
            foreach (var r in records)
            {
                // The nearest node, resource or project-settings object; a plain Object on the way (a TileSet tile's
                // TileData) is stored by the resource that holds it, so it counts only when nothing encloses it.
                GodotObject? plain = null, owner = null;
                foreach (var candidate in r.Owners)
                {
                    var o = candidate as GodotObject ?? (candidate as Settings.IRqlViewOf)?.Owner;
                    if (o == null || !GodotObject.IsInstanceValid(o)) continue;
                    if (o is Node || o is Resource || Settings.RqlProject.Is(o)) { owner = o; break; }
                    plain ??= o;
                }
                owner ??= plain;
                if (owner != null && seen.Add(owner)) owners.Add(owner);
            }
            return owners;
        }

        /// <summary>
        /// The values of every stored Godot property (storage usage) of <paramref name="o"/>, by name, plus a node's
        /// <c>name</c> (its property has no storage usage: the .tscn writes it in the node header), so a rename is undoable.
        /// </summary>
        internal static Dictionary<string, Variant> StoredProperties(GodotObject o)
        {
            var values = new Dictionary<string, Variant>(StringComparer.Ordinal);
            // Project settings at their default value have no storage usage: snapshot every listed setting.
            foreach (var name in GodotVariants.PropertyNames(o, storageOnly: !Settings.RqlProject.Is(o)))
                values[name] = o.Get(name);
            if (o is Node node) values["name"] = node.Name;
            return values;
        }

        /// <summary>
        /// Whether two property values are equal. Variant has no value equality: objects compare by identity, packed arrays
        /// element-wise, Godot arrays / dictionaries recursively, scalars and structs by value — no text conversion of
        /// large values; only the remaining kinds (Callable, Signal …) fall back to the text Godot writes to .tscn / .tres.
        /// </summary>
        internal static bool Same(Variant a, Variant b)
        {
            if (a.VariantType != b.VariantType) return false;
            switch (a.VariantType)
            {
                case Variant.Type.Nil: return true;
                case Variant.Type.Object: return ReferenceEquals(a.AsGodotObject(), b.AsGodotObject());
                case Variant.Type.Array: return a.AsGodotArray().RecursiveEqual(b.AsGodotArray());
                case Variant.Type.Dictionary: return a.AsGodotDictionary().RecursiveEqual(b.AsGodotDictionary());
                case Variant.Type.StringName:
                case Variant.Type.NodePath:
                    return a.Obj?.ToString() == b.Obj?.ToString();
                case Variant.Type.Callable:
                case Variant.Type.Signal:
                case Variant.Type.Rid:
                    return GD.VarToStr(a) == GD.VarToStr(b);
            }
            var x = a.Obj;
            var y = b.Obj;
            return x is System.Array xa && y is System.Array ya
                ? System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals(xa, ya)
                : Equals(x, y);
        }

        // A node, or a resource built into the scene (not a file resource, nor a sub-resource of one).
        private static bool IsSceneObject(GodotObject o) => o is Node || (o is Resource r && !IsFileResource(r) && ContainerFile(r) == null);

        /// <summary>A node of the scene being edited (the root or one of its descendants).</summary>
        internal static bool IsInEditedScene(Node node)
        {
            var root = EditedRoot();
            return root != null && (node == root || root.IsAncestorOf(node));
        }

        /// <summary>A resource stored in its own file (not built into a scene or another resource).</summary>
        internal static bool IsFileResource(Resource resource)
        {
            var path = resource.ResourcePath;
            return !string.IsNullOrEmpty(path) && path.StartsWith("res://", StringComparison.Ordinal) && path.IndexOf("::", StringComparison.Ordinal) < 0;
        }

        // ------------------------------------------------------------ undo + save

        /// <summary>
        /// The editor's undo manager. Godot 4.3's C# API exposes it only on the EditorPlugin (EditorPlugin.GetUndoRedo), so
        /// the plugin assigns it at install (RqlInstaller); null = changes are applied without undo entries.
        /// </summary>
        internal static Func<EditorUndoRedoManager?>? UndoRedo { get; set; }

        private static void CommitUndo()
        {
            var undo = UndoRedo?.Invoke();
            if (undo == null || !GodotObject.IsInstanceValid(undo))
            {
                _sceneUndo.Clear();
                _otherUndo.Clear();
                // No undo history keeps the removed nodes alive: free them instead of leaking them as orphans.
                foreach (var node in _removed)
                    if (GodotObject.IsInstanceValid(node) && !node.IsInsideTree()) node.Free();
            }
            else
            {
                Commit(undo, _sceneUndo, EditedRoot());
                Commit(undo, _otherUndo, GlobalHistoryContext);
            }
            _removed.Clear();
            _snapshots.Clear();
        }

        // The context that files an action under the global history, whatever objects its steps touch (Godot would file
        // one on a path-less resource, such as a TileSet source created in this session, under the edited scene): any
        // object that is neither a node nor a resource does; the ProjectSettings singleton is always there.
        private static GodotObject GlobalHistoryContext => Settings.RqlProject.Instance;

        private static void Commit(EditorUndoRedoManager undo, List<UndoStep> steps, GodotObject? context)
        {
            if (steps.Count == 0) return;
            try
            {
                undo.CreateAction("RQL mutation", global::Godot.UndoRedo.MergeMode.Disable, context);
                // Every step joins the context's history: a built-in or path-less resource would otherwise be guessed into
                // another one ("UndoRedo history mismatch").
                undo.ForceFixedHistory();
                // Undo operations run in the order they are registered: the do parts go in the order the changes
                // happened, the undo parts in reverse, so the last change is undone first.
                foreach (var step in steps) Register(undo, step.Redo);
                for (int i = steps.Count - 1; i >= 0; i--) Register(undo, steps[i].Undo);
                undo.CommitAction(execute: false);
            }
            finally { steps.Clear(); }
        }

        private static void Register(EditorUndoRedoManager undo, Action<EditorUndoRedoManager>? operations)
        {
            if (operations == null) return;
            try { operations(undo); }
            catch (Exception ex) { GodotMcpLog.Warning($"[Godot-MCP] RQL undo step skipped: {ex.Message}"); }
        }

        /// <summary>Save every file resource the mutation changed; returns what could not be saved.</summary>
        private static List<string> SaveResources()
        {
            var problems = new List<string>();
            foreach (var resource in _toSave)
                if (GodotObject.IsInstanceValid(resource) && IsFileResource(resource) && SaveFile(resource) is { } problem)
                    problems.Add(problem);
            _toSave.Clear();
            return problems;
        }

        /// <summary>
        /// The resource file a built-in sub-resource is stored in (path "res://tiles.tres::TileSetAtlasSource_x"), when that
        /// file is a resource and not a scene (a scene's sub-resources are saved with the scene); null otherwise.
        /// </summary>
        private static Resource? ContainerFile(Resource resource, int depth = 0)
        {
            if (depth < 8 && _containers.TryGetValue(resource, out var id) && GodotObject.InstanceFromId(id.Value) is Resource container)
                return IsFileResource(container) ? container : ContainerFile(container, depth + 1);
            var path = resource.ResourcePath ?? "";
            var cut = path.IndexOf("::", StringComparison.Ordinal);
            if (cut <= 0 || !ResPathNormalizer.IsResPath(path)) return null;
            var file = path.Substring(0, cut);
            if (ResPathNormalizer.IsScenePath(file)) return null;
            // The loaded (cached) container: the one the sub-resource belongs to.
            return ResourceLoader.HasCached(file) ? ResourceLoader.Load(file) : null;
        }

        /// <summary>Save project.godot after a mutation that changed a project setting.</summary>
        private static void SaveProjectSettings(List<string> problems)
        {
            if (!_settingsChanged) return;
            _settingsChanged = false;
            var error = ProjectSettings.Save();
            if (error != Error.Ok) problems.Add($"project.godot could not be saved: {error}");
        }

        /// <summary>Write a file resource to its res:// file and refresh the editor filesystem; the problem, or null when saved.</summary>
        internal static string? SaveFile(Resource resource)
        {
            var path = resource.ResourcePath;
            var error = ResourceSaver.Save(resource, path);
            if (error != Error.Ok) return $"resource '{path}' could not be saved: {error}";
            EditorInterface.Singleton.GetResourceFilesystem()?.UpdateFile(path);
            return null;
        }

        /// <summary>The uid:// text of the file at <paramref name="path"/>, or null when it has none.</summary>
        internal static string? UidOf(string path)
        {
            var id = ResourceLoader.GetResourceUid(path);
            return id == ResourceUid.InvalidId ? null : ResourceUid.IdToText(id);
        }
    }
}
#endif
