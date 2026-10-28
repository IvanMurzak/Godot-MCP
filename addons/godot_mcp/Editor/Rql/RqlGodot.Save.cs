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

        // The editor's autoload tab when the running mutation changed an autoload: refreshed after saving (held for one mutation only).
        private static Node? _changedAutoloads;
        // Receipt warnings of the running mutation (code, text): a radians rotation written with degree-sized angles, a scene
        // opened with missing dependencies.
        private static readonly List<(string code, string text)> _warnings = new List<(string code, string text)>();

        /// <summary>Add a warning to the receipt of the running mutation (once per text).</summary>
        internal static void Warn(string code, string text)
        {
            if (!_warnings.Contains((code, text))) _warnings.Add((code, text));
        }
        // Objects whose change registered its own do/undo (structural edits such as adding a TileSet source or a Theme
        // item): their property snapshot is not turned into undo properties (setting a removed entry to null does not
        // remove it again), they are only marked changed / saved.
        private static readonly HashSet<GodotObject> _explicitUndo = new HashSet<GodotObject>();
        // The undo steps setters register during a set stage, held until the stage commits (dropped when it is rolled back).
        private static readonly RqlSetStageBuffer _setStages = new RqlSetStageBuffer();

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
            _changedAutoloads = null;
            _warnings.Clear();
            _explicitUndo.Clear();
            _setStages.Clear();
            _propertyTables.Clear();
        }

        /// <summary>
        /// Run a set / call query, then commit its undo action and save the edited resource files (see the class summary).
        /// Saving problems become <c>not-saved</c> warnings on the receipt, never a tool error: the change is applied.
        /// </summary>
        public static QueryResult Mutate(string query, bool dryRun = false, int? maxTargets = null)
        {
            if (_mutating == 0) ResetState();
            _mutating++;
            QueryResult result;
            try
            {
                result = RunQuery(() => Engine.Execute(query, RqlTargetLimit.BudgetsFor(maxTargets), new QueryOptions { AllowMutation = true, DryRun = dryRun }));
            }
            finally
            {
                _mutating--;
                // A set stage of this query that never committed was rolled back: its setters' undo steps go with it.
                _setStages.DiscardAbove(_mutating);
                if (_mutating == 0) CommitUndo(query);
            }
            if (_mutating == 0)
                foreach (var (code, text) in _warnings)
                    result.Diagnostics.Add(Diagnostic.Warning(code, text));
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
        /// first). Calls only run inside rql-mutate. A step registered by a setter joins only when its set stage commits.
        /// </summary>
        internal static void RecordUndo(Action<EditorUndoRedoManager>? redo, Action<EditorUndoRedoManager>? undo)
        {
            var step = new UndoStep(redo, undo);
            _setStages.Record(() => _sceneUndo.Add(step));
        }

        /// <summary>
        /// Register the do / undo operations of a structural change of <paramref name="owner"/> (a resource such as a TileSet or
        /// Theme) that a call or setter already applied. The owner's property snapshot is then not diffed into undo properties;
        /// it is still saved (file resource) or marks the scene unsaved (built-in resource).
        /// </summary>
        internal static void RecordUndoFor(GodotObject owner, Action<EditorUndoRedoManager>? redo, Action<EditorUndoRedoManager>? undo)
        {
            var step = new UndoStep(redo, undo);
            _setStages.Record(() =>
            {
                (IsSceneObject(owner) ? _sceneUndo : _otherUndo).Add(step);
                _explicitUndo.Add(owner);
            });
        }

        /// <summary>
        /// Remove an entry of <paramref name="owner"/> whose stored properties start with <paramref name="prefix"/> (a TileSet tile
        /// "x:y/", a MeshLibrary item "item/&lt;id&gt;/") and register its undo: <paramref name="recreate"/>, then every saved
        /// property, so the entry comes back with all its settings.
        /// </summary>
        internal static void RecordRemoval(GodotObject owner, string prefix, Action remove, Action<EditorUndoRedoManager> redo,
            Action<EditorUndoRedoManager> recreate)
        {
            var saved = SnapshotOf(owner).Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            remove();
            RecordUndoFor(owner, redo, ur =>
            {
                recreate(ur);
                foreach (var kv in saved) ur.AddUndoProperty(owner, kv.Key, kv.Value);
            });
        }

        /// <summary>Mark the edited scene as having unsaved changes.</summary>
        internal static void MarkSceneUnsaved()
        {
            if (EditedRoot() != null) EditorInterface.Singleton.MarkSceneAsUnsaved();
        }

        // ------------------------------------------------------------ engine hooks

        private static void BeforeWrite(IReadOnlyList<MutationRecord> records)
        {
            _setStages.BeforeWrite(records, _mutating);
            foreach (var r in records)
                if (r.Method == null && r.Owner is Node && RotationUnits.Warning(r.Member, Components(r.New)) is { } warning)
                    Warn("radians", warning);
            foreach (var owner in MutatedObjects(records))
                if (!_snapshots.ContainsKey(owner))
                    _snapshots[owner] = StoredProperties(owner);
        }

        // Committed gets exactly the applied records (every set after all writes succeeded, the completed calls when a later
        // call threw); a set the engine rolled back never gets here.
        private static void Committed(IReadOnlyList<MutationRecord> records)
        {
            // First the undo steps the stage's setters registered (they also mark their owners as explicitly undone).
            _setStages.Committed(records, _mutating);
            var sceneChanged = false;
            var root = EditedRoot();
            var isCall = records.Count > 0 && records[0].Method != null; // set records carry no method
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
                        // An autoload change also refreshes the editor's autoload registry (scripts then resolve the singleton).
                        var autoloads = save && changed.Any(c => c.name.StartsWith(Settings.RqlProject.AutoloadPrefix, StringComparison.Ordinal))
                            ? Settings.RqlProject.EditorAutoloads() : null;
                        _changedAutoloads ??= autoloads;
                        (IsSceneObject(owner) ? _sceneUndo : _otherUndo).Add(new UndoStep(
                            ur =>
                            {
                                foreach (var c in changed) ur.AddDoProperty(target, c.name, c.after);
                                if (save) ur.AddDoMethod(target, ProjectSettings.MethodName.Save);
                                if (autoloads != null) ur.AddDoMethod(autoloads, Settings.RqlProject.UpdateAutoloadMethod);
                            },
                            ur =>
                            {
                                foreach (var c in changed) ur.AddUndoProperty(target, c.name, c.before);
                                if (save) ur.AddUndoMethod(target, ProjectSettings.MethodName.Save);
                                if (autoloads != null) ur.AddUndoMethod(autoloads, Settings.RqlProject.UpdateAutoloadMethod);
                            }));
                    }
                }

                switch (owner)
                {
                    case Node node when root != null && (node == root || root.IsAncestorOf(node)):
                        sceneChanged = true;
                        // A node inside an instance (a property override, a call such as AddChild on it) is saved only
                        // when the instance has Editable Children (a no-op for the scene's own nodes). A set that changed no
                        // stored property leaves the instance closed.
                        if (propertiesChanged || isCall) EnsureEditable(node, root);
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

        // The numbers of a written rotation: a Vector3 / Vector2 or one angle.
        private static double[] Components(object? value) => value switch
        {
            Vector3 v => new double[] { v.X, v.Y, v.Z },
            Vector2 v => new double[] { v.X, v.Y },
            float f => new double[] { f },
            double d => new[] { d },
            _ => Array.Empty<double>(),
        };

        // The Godot object whose snapshot covers each change (see RqlMutationOwner.Nearest).
        private static List<GodotObject> MutatedObjects(IReadOnlyList<MutationRecord> records)
        {
            var owners = new List<GodotObject>();
            var seen = new HashSet<GodotObject>();
            foreach (var r in records)
                if (RqlMutationOwner.Nearest<GodotObject>(r.Owners, ClassifyOwner) is { } owner && seen.Add(owner)) owners.Add(owner);
            return owners;
        }

        private static (GodotObject?, RqlOwnerKind) ClassifyOwner(object candidate)
        {
            var (o, view) = candidate is Settings.IRqlViewOf v ? (v.Owner, true) : (candidate as GodotObject, false);
            if (o == null || !GodotObject.IsInstanceValid(o)) return (null, RqlOwnerKind.None);
            if (view) return (o, RqlOwnerKind.View);
            return (o, o is Node || o is Resource || Settings.RqlProject.Is(o) ? RqlOwnerKind.Stored : RqlOwnerKind.Plain);
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

        /// <summary>
        /// The global undo history, or the edited scene's (null without an undo manager, or without a scene for the scene
        /// history): what an rql-mutate commits to, Undo / Redo step and the test hooks drive.
        /// </summary>
        internal static global::Godot.UndoRedo? UndoHistory(bool global)
        {
            var manager = UndoRedo?.Invoke();
            if (manager == null || !GodotObject.IsInstanceValid(manager)) return null;
            if (global) return manager.GetHistoryUndoRedo(0); // EditorUndoRedoManager.SpecialHistory.Global
            return EditedRoot() is { } root ? manager.GetHistoryUndoRedo(manager.GetObjectHistoryId(root)) : null;
        }

        // The undo action is named after the mutation's query ("RQL: nodes | … | set …").
        private static void CommitUndo(string query)
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
                var name = HistoryStepText.ActionName(query);
                AddResaveSteps(_otherUndo, _toSave);
                // Split over the two histories: the parts are named so editor | call Undo() / Redo() step both.
                var split = _sceneUndo.Count > 0 && _otherUndo.Count > 0;
                Commit(undo, _sceneUndo, EditedRoot(), split ? name + HistoryStepText.ScenePart : name);
                Commit(undo, _otherUndo, GlobalHistoryContext, split ? name + HistoryStepText.GlobalPart : name);
            }
            _removed.Clear();
            _snapshots.Clear();
        }

        // The context that files an action under the global history, whatever objects its steps touch (Godot would file
        // one on a path-less resource, such as a TileSet source created in this session, under the edited scene): any
        // object that is neither a node nor a resource does; the ProjectSettings singleton is always there.
        private static GodotObject GlobalHistoryContext => Settings.RqlProject.Instance;

        /// <summary>
        /// The mutation saves the resource files it changed, so undo and redo save them again: the file never keeps a value
        /// the editor has undone. The saves are registered last on both sides (after every property of the action).
        /// </summary>
        private static void AddResaveSteps(List<UndoStep> steps, List<Resource> changedFiles)
        {
            var files = changedFiles.Where(r => GodotObject.IsInstanceValid(r) && IsFileResource(r)).ToList();
            if (steps.Count == 0 || files.Count == 0) return;
            var saver = global::Godot.Engine.GetSingleton("ResourceSaver");
            // An empty path saves to the resource's path at undo / redo time: a file moved since goes to its new place, and a
            // deleted one (its path cleared) is not written back.
            void Save(Action<GodotObject, StringName, Variant[]> add)
            {
                foreach (var r in files) add(saver, "save", new Variant[] { r, "" });
            }
            steps.Insert(0, new UndoStep(null, ur => Save(ur.AddUndoMethod)));
            steps.Add(new UndoStep(ur => Save(ur.AddDoMethod), null));
        }

        private static void Commit(EditorUndoRedoManager undo, List<UndoStep> steps, GodotObject? context, string name)
        {
            if (steps.Count == 0) return;
            try
            {
                undo.CreateAction(name, global::Godot.UndoRedo.MergeMode.Disable, context);
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
            if (_changedAutoloads != null && GodotObject.IsInstanceValid(_changedAutoloads))
                _changedAutoloads.Call(Settings.RqlProject.UpdateAutoloadMethod);
            _changedAutoloads = null;
        }

        /// <summary>Write a file resource to its res:// file and refresh the editor filesystem; the problem, or null when saved.</summary>
        internal static string? SaveFile(Resource resource)
        {
            var path = resource.ResourcePath;
            var error = ResourceSaver.Save(resource, path);
            if (error != Error.Ok) return $"resource '{path}' could not be saved: {error}";
            RqlReimport.Register(path);
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
