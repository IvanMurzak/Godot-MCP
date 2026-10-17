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
    /// <c>execute: false</c> because the change is already applied.</item>
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

        // Per running Mutate: Godot property snapshots taken before the writes, the undo operations collected so far (scene
        // history / other history), and the file resources to save afterwards.
        private static readonly Dictionary<GodotObject, Dictionary<string, Variant>> _snapshots = new Dictionary<GodotObject, Dictionary<string, Variant>>();
        private static readonly List<Action<EditorUndoRedoManager>> _sceneUndo = new List<Action<EditorUndoRedoManager>>();
        private static readonly List<Action<EditorUndoRedoManager>> _otherUndo = new List<Action<EditorUndoRedoManager>>();
        private static readonly List<Resource> _toSave = new List<Resource>();

        private static void ResetState()
        {
            _mutating = 0;
            _snapshots.Clear();
            _sceneUndo.Clear();
            _otherUndo.Clear();
            _toSave.Clear();
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
            try { problems = SaveResources(); }
            catch (Exception ex) { problems = new List<string> { $"saving failed: {ex.GetType().Name}: {ex.Message}" }; }
            foreach (var problem in problems)
                result.Diagnostics.Add(Diagnostic.Warning("not-saved", problem));
            return result;
        }

        /// <summary>
        /// Register the do/undo operations of a structural scene change a call already applied; they join the running
        /// mutation's scene-history action (committed with <c>execute: false</c>). Calls only run inside rql-mutate.
        /// </summary>
        internal static void RecordUndo(Action<EditorUndoRedoManager> operations) => _sceneUndo.Add(operations);

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
                if (hasSnapshot)
                {
                    var after = StoredProperties(owner);
                    var changed = new List<(string name, Variant before, Variant after)>();
                    foreach (var kv in after)
                    {
                        var known = snapshot!.TryGetValue(kv.Key, out var old);
                        if (!known || !Same(old, kv.Value)) changed.Add((kv.Key, known ? old : default, kv.Value));
                    }
                    if (changed.Count > 0)
                    {
                        propertiesChanged = true;
                        var target = owner;
                        (IsSceneObject(owner) ? _sceneUndo : _otherUndo).Add(ur =>
                        {
                            foreach (var c in changed)
                            {
                                ur.AddDoProperty(target, c.name, c.after);
                                ur.AddUndoProperty(target, c.name, c.before);
                            }
                        });
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
                        if (propertiesChanged && !_toSave.Contains(resource)) _toSave.Add(resource);
                        break;
                    case Resource _ when propertiesChanged:
                        sceneChanged = true; // built-in (scene-embedded) resource
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
            foreach (var r in records)
                foreach (var candidate in r.Owners)
                    if (candidate is GodotObject o)
                    {
                        if (GodotObject.IsInstanceValid(o) && !owners.Contains(o)) owners.Add(o);
                        break;
                    }
            return owners;
        }

        /// <summary>The values of every stored Godot property (storage usage) of <paramref name="o"/>, by name.</summary>
        internal static Dictionary<string, Variant> StoredProperties(GodotObject o)
        {
            var values = new Dictionary<string, Variant>(StringComparer.Ordinal);
            foreach (var name in GodotVariants.PropertyNames(o, storageOnly: true))
                values[name] = o.Get(name);
            return values;
        }

        /// <summary>
        /// Whether two property values are equal. Variant has no value equality: objects compare by identity, packed arrays
        /// element-wise, Godot arrays / dictionaries recursively, scalars and structs by value — no text conversion of
        /// large values; only the remaining kinds (Callable, Signal …) fall back to the text Godot writes to .tscn / .tres.
        /// </summary>
        private static bool Same(Variant a, Variant b)
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

        private static bool IsSceneObject(GodotObject o) => o is Node || (o is Resource r && !IsFileResource(r));

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
            }
            else
            {
                Commit(undo, _sceneUndo, EditedRoot());
                Commit(undo, _otherUndo, null);
            }
            _snapshots.Clear();
        }

        private static void Commit(EditorUndoRedoManager undo, List<Action<EditorUndoRedoManager>> operations, GodotObject? context)
        {
            if (operations.Count == 0) return;
            try
            {
                undo.CreateAction("RQL mutation", global::Godot.UndoRedo.MergeMode.Disable, context);
                foreach (var op in operations)
                {
                    try { op(undo); }
                    catch (Exception ex) { GodotMcpLog.Warning($"[Godot-MCP] RQL undo step skipped: {ex.Message}"); }
                }
                undo.CommitAction(execute: false);
            }
            finally { operations.Clear(); }
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
