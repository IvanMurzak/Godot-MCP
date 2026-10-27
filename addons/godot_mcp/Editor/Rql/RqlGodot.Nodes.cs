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
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Structural scene edits behind the Node adapter's calls (the replaced <c>node-create</c> / <c>node-duplicate</c> /
    /// <c>node-delete</c> / <c>node-set-parent</c> / <c>node-reorder</c> tools). Each applies the change at once, keeps the
    /// node owned by the edited scene root (so it is saved with the scene), marks the scene unsaved and registers do/undo
    /// methods with <see cref="RecordUndo"/> — the same operations Godot's Scene dock registers for these edits.
    /// </summary>
    public static partial class RqlGodot
    {
        private static Node RequireEditedRoot() =>
            EditorToolGuards.GetEditedSceneRootOrThrow("no scene is being edited; open one with editor | call OpenScene(\"res://…\")");

        private static void RequireInEditedScene(Node node, string what)
        {
            if (!IsInEditedScene(node))
                throw new InvalidOperationException($"{what} '{node.Name}' is not part of the edited scene");
        }

        /// <summary>A new node of a Godot class (or a GDScript / C# global script class) as the last child of <paramref name="parent"/>.</summary>
        public static Node AddChild(Node parent, string className, string? name)
        {
            var root = RequireEditedRoot();
            RequireInEditedScene(parent, "parent");
            var node = Instantiate(className);
            if (!string.IsNullOrEmpty(name)) node.Name = name;
            return Attach(parent, node, root);
        }

        /// <summary>A new instance of the PackedScene at <paramref name="path"/> as the last child of <paramref name="parent"/>.</summary>
        public static Node InstantiateScene(Node parent, string path, string? name)
        {
            var root = RequireEditedRoot();
            RequireInEditedScene(parent, "parent");
            // Like the Scene dock: the instance keeps its scene's state, so only what is changed here is saved as an override
            // (without it the .tscn pins the instance's type, script and groups, which then no longer follow the scene file).
            var node = InstantiableScene(path, root).Instantiate(PackedScene.GenEditState.Instance)
                       ?? throw new InvalidOperationException($"could not instantiate '{path}'");
            if (!string.IsNullOrEmpty(name)) node.Name = name;
            return Attach(parent, node, root);
        }

        // The PackedScene at path, when the edited scene (root) may instance it.
        private static PackedScene InstantiableScene(string path, Node root)
        {
            if (!TryLoadResource(path, out var resource, out var reason)) throw new ArgumentException(reason);
            if (!(resource is PackedScene packed)) throw new ArgumentException($"'{path}' is a {resource!.GetClass()}, not a scene (PackedScene)");
            // Like the Scene dock: a scene cannot contain itself (Godot would then fail to save it, keeping only the root).
            if (!string.IsNullOrEmpty(root.SceneFilePath) && (SamePath(packed.ResourcePath, root.SceneFilePath) || Contains(packed, root.SceneFilePath, new HashSet<string>(StringComparer.OrdinalIgnoreCase))))
                throw new InvalidOperationException($"cannot instance '{path}' in '{root.SceneFilePath}': the edited scene would contain itself");
            return packed;
        }

        // ---- call validators (RQL 0.2): why a planned call would fail, checked for every target before the first call (dry
        // runs too), so a multi-target call is all-or-nothing. They only read; the calls repeat the checks.

        public static string? ValidateAddChild(Node parent, string className) => Rejection(() =>
        {
            RequireEditedRoot();
            RequireInEditedScene(parent, "parent");
            NodeClass(className, out _);
        });

        public static string? ValidateInstantiateScene(Node parent, string path) => Rejection(() =>
        {
            var root = RequireEditedRoot();
            RequireInEditedScene(parent, "parent");
            InstantiableScene(path, root);
        });

        // A node of an instance is refused by ValidateBatch, which sees every target: freeing an instance together with its
        // descendants (… | recurse children | call Free()) removes them with it.
        public static string? ValidateRemove(Node node) => Rejection(() => CheckRemove(node, RequireEditedRoot()));

        /// <summary>
        /// The batch check of every set / call stage (RQL <see cref="MutationPolicy.ValidateBatch"/>, dry runs too): a
        /// <c>Free()</c> target that is part of an instance (or of an inherited scene) is refused up front unless one of its
        /// ancestors is freed by the same call, which removes it with the ancestor.
        /// </summary>
        internal static string? ValidateBatch(IReadOnlyList<MutationRecord> records)
        {
            var freed = new HashSet<Node>();
            foreach (var record in records)
                if (record.Method?.Name == "Free" && record.Item is Node node) freed.Add(node);
            if (freed.Count == 0 || !(EditedRoot() is { } root)) return null;
            foreach (var node in freed)
            {
                var removedWithAncestor = false;
                for (var parent = node.GetParent(); parent != null && !removedWithAncestor; parent = parent.GetParent())
                    removedWithAncestor = freed.Contains(parent);
                if (!removedWithAncestor && Rejection(() => RequireOwnNode(node, root, "removed")) is { } reason) return reason;
            }
            return null;
        }

        // Throws for a node Remove may not take out: outside the edited scene, or the root.
        private static void CheckRemove(Node node, Node root)
        {
            RequireInEditedScene(node, "node");
            if (node == root) throw new InvalidOperationException("the scene root cannot be removed; open or create another scene instead");
        }

        public static string? ValidateDuplicate(Node node) => Rejection(() => DuplicateParent(node, RequireEditedRoot()));

        public static string? ValidateMoveChild(Node node) => Rejection(() => MoveChildParent(node));

        // The parent a Duplicate copies into; throws for the root or a node outside the edited scene.
        private static Node DuplicateParent(Node node, Node root)
        {
            RequireInEditedScene(node, "node");
            if (node == root) throw new InvalidOperationException("the scene root cannot be duplicated");
            RequireOwnNode(node, root, "duplicated");
            return node.GetParent() ?? throw new InvalidOperationException($"'{node.Name}' has no parent");
        }

        // The parent whose children MoveChild reorders; throws for the root or a node outside the edited scene.
        private static Node MoveChildParent(Node node)
        {
            RequireInEditedScene(node, "node");
            var root = RequireEditedRoot();
            if (node == root || !(node.GetParent() is { } parent))
                throw new InvalidOperationException("the scene root has no siblings to reorder among");
            RequireOwnNode(node, root, "reordered");
            return parent;
        }

        public static string? ValidateReparent(Node node, Node? newParent) => Rejection(() => CheckReparent(node, newParent, RequireEditedRoot()));

        internal static string? Rejection(Action check)
        {
            try { check(); return null; }
            catch (ArgumentException e) { return e.Message; }
            catch (InvalidOperationException e) { return e.Message; }
        }

        /// <summary>
        /// Whether <paramref name="node"/> is part of a scene instanced in the edited scene (owned by the instance, not by the
        /// edited scene root): Godot saves its property overrides, never its removal, renaming, reordering or a new parent.
        /// </summary>
        internal static bool IsForeign(Node node, Node root) => node != root && node.Owner != null && node.Owner != root;

        /// <summary>Why <paramref name="node"/> cannot be renamed, or null (the Node adapter's name validation).</summary>
        internal static string? ForeignRenameProblem(Node node) =>
            EditedRoot() is { } root ? Rejection(() => RequireOwnNode(node, root, "renamed")) : null;

        // Like the Scene dock ("Can't operate on nodes from a foreign scene" / "… the current scene inherits from"): the edit
        // would be lost when the scene is saved.
        private static void RequireOwnNode(Node node, Node root, string verb)
        {
            if (IsForeign(node, root))
            {
                var instance = node.Owner!;
                var scene = string.IsNullOrEmpty(instance.SceneFilePath) ? "an instanced scene" : instance.SceneFilePath;
                throw new InvalidOperationException($"'{PathOf(node)}' is part of the instance '{PathOf(instance)}' of {scene} and cannot " +
                    $"be {verb} here (Godot would not save it); edit {scene} instead, or the whole instance. Its properties can be " +
                    "changed here (saved as overrides)");
            }
            if (InheritedFrom(node, root) is { } baseScene)
                throw new InvalidOperationException($"'{PathOf(node)}' comes from {baseScene}, the scene this one inherits, and cannot be " +
                    $"{verb} here (Godot would not save it); edit {baseScene} instead. Its properties can be changed here (saved as overrides)");
        }

        /// <summary>
        /// The base scene a node of an inherited scene comes from (its path is in the base scene's state, or that scene's own
        /// base), or null. Such nodes are owned by the edited root like its own nodes, but Godot saves only their overrides.
        /// </summary>
        private static string? InheritedFrom(Node node, Node root)
        {
            if (node == root || string.IsNullOrEmpty(root.SceneFilePath) || !ResourceLoader.Exists(root.SceneFilePath)) return null;
            var path = PathOf(node);
            var state = ResourceLoader.Load<PackedScene>(root.SceneFilePath)?.GetState();
            // An inherited scene's first node instances its base scene.
            for (var depth = 0; depth < 32 && state != null && state.GetNodeCount() > 0 && state.GetNodeInstance(0) is { } basePacked; depth++)
            {
                state = basePacked.GetState();
                for (var i = 0; i < state.GetNodeCount(); i++)
                {
                    var p = state.GetNodePath(i).ToString();
                    if ((p.StartsWith("./", StringComparison.Ordinal) ? p.Substring(2) : p) == path) return basePacked.ResourcePath;
                }
            }
            return null;
        }

        /// <summary>
        /// Turn on Editable Children for every instance that owns <paramref name="node"/>, undoably: Godot saves changes to the
        /// nodes inside an instance (property overrides, added children) only for an editable instance, and drops them otherwise.
        /// </summary>
        internal static void EnsureEditable(Node node, Node root)
        {
            // Owners that are not instance roots (a tool script's own wiring) are left alone.
            for (var owner = node.Owner; owner != null && owner != root && !string.IsNullOrEmpty(owner.SceneFilePath); owner = owner.Owner)
            {
                if (root.IsEditableInstance(owner)) continue;
                var instance = owner;
                root.SetEditableInstance(instance, true);
                RecordUndo(
                    ur => ur.AddDoMethod(root, Node.MethodName.SetEditableInstance, instance, true),
                    ur => ur.AddUndoMethod(root, Node.MethodName.SetEditableInstance, instance, false));
            }
        }

        private static void CheckReparent(Node node, [System.Diagnostics.CodeAnalysis.NotNull] Node? newParent, Node root)
        {
            if (newParent == null)
                throw new ArgumentException("Reparent needs the new parent node, e.g. call Reparent(godot.find(\"Props\")) or godot.find(\".\") for the scene root");
            RequireInEditedScene(node, "node");
            RequireInEditedScene(newParent, "new parent");
            if (node == root) throw new InvalidOperationException("the scene root cannot be reparented");
            RequireOwnNode(node, root, "moved");
            if (node == newParent || node.IsAncestorOf(newParent))
                throw new InvalidOperationException($"'{node.Name}' cannot be moved under itself or one of its descendants");
        }


        // Paths compared case-insensitively (Windows / macOS): a false match only refuses an instance, never allows a cycle.
        private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // Whether the scene instances the file at scenePath, directly or through its own sub-scenes (each walked once).
        private static bool Contains(PackedScene scene, string scenePath, HashSet<string> visited)
        {
            var state = scene.GetState();
            for (int i = 0; i < state.GetNodeCount(); i++)
                if (state.GetNodeInstance(i) is { } sub && (SamePath(sub.ResourcePath, scenePath) || (visited.Add(sub.ResourcePath) && Contains(sub, scenePath, visited))))
                    return true;
            return false;
        }

        /// <summary>A copy of <paramref name="node"/> placed right after it.</summary>
        public static Node Duplicate(Node node)
        {
            var root = RequireEditedRoot();
            var parent = DuplicateParent(node, root);
            var copy = DuplicateNode(node, root);
            Attach(parent, copy, root);
            if (root.IsEditableInstance(node))
            {
                root.SetEditableInstance(copy, true);
                RecordUndo(ur => ur.AddDoMethod(root, Node.MethodName.SetEditableInstance, copy, true), null);
            }
            var index = node.GetIndex(includeInternal: false) + 1;
            parent.MoveChild(copy, index);
            RecordUndo(ur => ur.AddDoMethod(parent, Node.MethodName.MoveChild, copy, index), null);
            return copy;
        }

        // A scene instance is instanced again from its scene file (like the Scene dock's Duplicate) and given the original's
        // state: Node.Duplicate() makes a copy whose .tscn entry pins its type, script and groups and every property of its inner
        // nodes, so it no longer follows the scene file.
        private static Node DuplicateNode(Node node, Node root)
        {
            if (!string.IsNullOrEmpty(node.SceneFilePath) && ResourceLoader.Exists(node.SceneFilePath)
                && ResourceLoader.Load<PackedScene>(node.SceneFilePath)?.Instantiate(PackedScene.GenEditState.Instance) is { } copy)
            {
                copy.Name = node.Name; // Attach makes it unique among the siblings ("Enemy2"), like Node.Duplicate's copy
                CopyInstanceState(node, copy, root);
                return copy;
            }
            return node.Duplicate() as Node ?? throw new InvalidOperationException($"could not duplicate '{node.Name}'");
        }

        // The stored properties and persistent groups of `from` onto its fresh twin `to`, then the same for every node of the
        // instance below it; nodes this scene added inside the instance (owned by the root) are copied over.
        private static void CopyInstanceState(Node from, Node to, Node root)
        {
            foreach (var kv in StoredProperties(from))
                if (kv.Key != "name" && kv.Key != "script" && !Same(to.Get(kv.Key), kv.Value)) to.Set(kv.Key, kv.Value);
            foreach (var group in GroupsOf(from))
                if (!to.IsInGroup(group)) to.AddToGroup(group, persistent: true);
            foreach (var child in from.GetChildren(includeInternal: false))
            {
                if (child.Owner != root && to.GetNodeOrNull(child.Name.ToString()) is { } twin) CopyInstanceState(child, twin, root);
                else if (child.Owner == root && child.Duplicate() is Node added) to.AddChild(added, forceReadableName: true);
            }
        }

        /// <summary>
        /// Remove <paramref name="node"/> from the scene, undoably (the node stays alive in the undo history). A node whose
        /// ancestor this mutation already removed went with it: a no-op, so <c>… | call Free()</c> over a node and its
        /// descendants does not fail half-way.
        /// </summary>
        public static void Remove(Node node)
        {
            var root = RequireEditedRoot();
            if (WasRemoved(node)) return;
            CheckRemove(node, root);
            RequireOwnNode(node, root, "removed");
            var parent = node.GetParent()!;
            var index = node.GetIndex(includeInternal: false);
            var owners = new List<(Node node, Node? owner)>();
            SubtreeOwners(node, owners);
            parent.RemoveChild(node);
            _removed.Add(node);
            RecordUndo(
                ur => ur.AddDoMethod(parent, Node.MethodName.RemoveChild, node),
                ur =>
                {
                    ur.AddUndoMethod(parent, Node.MethodName.AddChild, node, true);
                    ur.AddUndoMethod(parent, Node.MethodName.MoveChild, node, index);
                    foreach (var (n, owner) in owners)
                        if (owner != null) ur.AddUndoMethod(n, Node.MethodName.SetOwner, owner);
                    ur.AddUndoReference(node);
                });
            MarkSceneUnsaved();
        }

        // The node, or one of its ancestors, was removed by the running mutation (its subtree left the scene with it).
        private static bool WasRemoved(Node node)
        {
            if (_removed.Count == 0) return false;
            for (Node? n = node; n != null; n = n.GetParent())
                if (_removed.Contains(n)) return true;
            return false;
        }

        /// <summary>Move <paramref name="node"/> under <paramref name="newParent"/> (keeping its global transform by default).</summary>
        public static void Reparent(Node node, Node? newParent, bool keepGlobalTransform)
        {
            var root = RequireEditedRoot();
            CheckReparent(node, newParent, root);
            var oldParent = node.GetParent()!;
            var oldIndex = node.GetIndex(includeInternal: false);
            if (oldParent == newParent) return;
            node.Reparent(newParent, keepGlobalTransform);
            EnsureEditable(newParent, root);
            if (node.Owner == null) node.Owner = root;
            Tool_Node.SetOwnerRecursive(node, root);
            // The mutation's property snapshot also records the node's local transform that keepGlobalTransform changed,
            // and its undo runs first (undo parts of later steps run first): moving back must keep that restored local
            // transform, or the old offset is applied a second time.
            RecordUndo(
                ur => ur.AddDoMethod(node, Node.MethodName.Reparent, newParent, keepGlobalTransform),
                ur =>
                {
                    ur.AddUndoMethod(node, Node.MethodName.Reparent, oldParent, false);
                    ur.AddUndoMethod(oldParent, Node.MethodName.MoveChild, node, oldIndex);
                });
            MarkSceneUnsaved();
        }

        /// <summary>Move <paramref name="node"/> to <paramref name="index"/> among its siblings (negative counts from the end, clamped).</summary>
        public static int MoveChild(Node node, int index)
        {
            var parent = MoveChildParent(node);
            var oldIndex = node.GetIndex(includeInternal: false);
            var target = NodeIndexResolver.Resolve(index, parent.GetChildCount(includeInternal: false));
            parent.MoveChild(node, target);
            RecordUndo(
                ur => ur.AddDoMethod(parent, Node.MethodName.MoveChild, node, target),
                ur => ur.AddUndoMethod(parent, Node.MethodName.MoveChild, node, oldIndex));
            MarkSceneUnsaved();
            return node.GetIndex(includeInternal: false);
        }

        // Add a new node (fresh, instanced or copied) to the scene: owned by the root so it is saved, selected in the editor.
        private static Node Attach(Node parent, Node node, Node root)
        {
            parent.AddChild(node, forceReadableName: true);
            node.Owner = root;
            OwnUnowned(node, root);
            var owners = new List<(Node node, Node? owner)>();
            SubtreeOwners(node, owners);
            RecordUndo(
                ur =>
                {
                    ur.AddDoMethod(parent, Node.MethodName.AddChild, node, true);
                    foreach (var (n, owner) in owners)
                        if (owner != null) ur.AddDoMethod(n, Node.MethodName.SetOwner, owner);
                    ur.AddDoReference(node);
                },
                ur => ur.AddUndoMethod(parent, Node.MethodName.RemoveChild, node));
            MarkSceneUnsaved();
            return node;
        }

        // Give the root every node below `node` that has no owner yet, also below the nodes of an instance (a copied instance's
        // nodes added by this scene); the instance's own nodes keep their owner.
        private static void OwnUnowned(Node node, Node root)
        {
            foreach (var child in node.GetChildren(includeInternal: false))
            {
                if (child.Owner == null) child.Owner = root;
                OwnUnowned(child, root);
            }
        }

        private static void SubtreeOwners(Node node, List<(Node, Node?)> sink)
        {
            sink.Add((node, node.Owner));
            foreach (var child in node.GetChildren(includeInternal: false)) SubtreeOwners(child, sink);
        }

        /// <summary>
        /// A new node of <paramref name="className"/>: a Godot class (ClassDB), or a global script class (GDScript
        /// <c>class_name</c> / a C# [GlobalClass]) — its native base instantiated with the script attached.
        /// </summary>
        private static Node Instantiate(string className) => Instantiate<Node>(className, "Node", "class");

        private static string NodeClass(string className, out Script? script) => ScriptAwareClass(className, "Node", "class", out script);

        /// <summary>
        /// A new object of <paramref name="className"/> deriving from <paramref name="baseClass"/>: a Godot class (ClassDB), or a
        /// global script class (GDScript <c>class_name</c> / a C# [GlobalClass]) — its native base instantiated with the script
        /// attached.
        /// </summary>
        internal static T Instantiate<[MustBeVariant] T>(string className, string baseClass, string paramName) where T : GodotObject
        {
            var native = ScriptAwareClass(className, baseClass, paramName, out var script);
            var o = EditorToolGuards.ValidateClassDbInstantiation<T>(native, paramName);
            if (script == null) return o;
            var id = o.GetInstanceId();
            o.SetScript(script);
            // Attaching a C# script replaces the managed wrapper: look the object up again by its instance id.
            return GodotObject.InstanceFromId(id) as T ?? o;
        }

        /// <summary>
        /// The native class <see cref="Instantiate{T}"/> creates for <paramref name="className"/> (and the global script class to
        /// attach, if any), after every check it makes; creates nothing, so call validators use it too.
        /// </summary>
        internal static string ScriptAwareClass(string className, string baseClass, string paramName, out Script? script)
        {
            script = null;
            var kind = baseClass == "Node" ? "node" : baseClass == "Resource" ? "resource" : "Godot";
            if (string.IsNullOrWhiteSpace(className))
                throw new ArgumentException($"a {kind} class is required, e.g. \"{(baseClass == "Node" ? "Node3D" : "StandardMaterial3D")}\"");
            var native = className;
            if (!ClassDB.ClassExists(className))
            {
                var entry = ProjectSettings.GetGlobalClassList().FirstOrDefault(e => e["class"].AsString() == className)
                            ?? throw new ArgumentException($"unknown {kind} class '{className}'" + Similar(ClassDB.GetClassList().Where(c => ClassDB.IsParentClass(c, baseClass)), className));
                script = ResourceLoader.Load<Script>(entry["path"].AsString())
                         ?? throw new ArgumentException($"the script of '{className}' failed to load");
                native = script.GetInstanceBaseType().ToString();
            }
            if (!ClassDB.IsParentClass(native, baseClass)) throw new ArgumentException($"'{className}' is not a {baseClass} class");
            EditorToolGuards.CheckClassDbInstantiation(native, paramName);
            return native;
        }
    }
}
#endif
