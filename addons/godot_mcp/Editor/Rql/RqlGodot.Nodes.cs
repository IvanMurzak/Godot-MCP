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
            var node = InstantiableScene(path, root).Instantiate() ?? throw new InvalidOperationException($"could not instantiate '{path}'");
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

        public static string? ValidateRemove(Node node) => WasRemoved(node) ? null : Rejection(() =>
        {
            var root = RequireEditedRoot();
            RequireInEditedScene(node, "node");
            if (node == root) throw new InvalidOperationException("the scene root cannot be removed; open or create another scene instead");
        });

        public static string? ValidateDuplicate(Node node) => Rejection(() => DuplicateParent(node, RequireEditedRoot()));

        public static string? ValidateMoveChild(Node node) => Rejection(() => MoveChildParent(node));

        // The parent a Duplicate copies into; throws for the root or a node outside the edited scene.
        private static Node DuplicateParent(Node node, Node root)
        {
            RequireInEditedScene(node, "node");
            if (node == root) throw new InvalidOperationException("the scene root cannot be duplicated");
            return node.GetParent() ?? throw new InvalidOperationException($"'{node.Name}' has no parent");
        }

        // The parent whose children MoveChild reorders; throws for the root or a node outside the edited scene.
        private static Node MoveChildParent(Node node)
        {
            RequireInEditedScene(node, "node");
            if (node == EditedRoot() || !(node.GetParent() is { } parent))
                throw new InvalidOperationException("the scene root has no siblings to reorder among");
            return parent;
        }

        public static string? ValidateReparent(Node node, Node? newParent) => Rejection(() => CheckReparent(node, newParent, RequireEditedRoot()));

        internal static string? Rejection(Action check)
        {
            try { check(); return null; }
            catch (ArgumentException e) { return e.Message; }
            catch (InvalidOperationException e) { return e.Message; }
        }

        private static void CheckReparent(Node node, [System.Diagnostics.CodeAnalysis.NotNull] Node? newParent, Node root)
        {
            if (newParent == null)
                throw new ArgumentException("Reparent needs the new parent node, e.g. call Reparent(godot.find(\"Props\")) or godot.find(\".\") for the scene root");
            RequireInEditedScene(node, "node");
            RequireInEditedScene(newParent, "new parent");
            if (node == root) throw new InvalidOperationException("the scene root cannot be reparented");
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
            var copy = node.Duplicate() as Node ?? throw new InvalidOperationException($"could not duplicate '{node.Name}'");
            Attach(parent, copy, root);
            var index = node.GetIndex(includeInternal: false) + 1;
            parent.MoveChild(copy, index);
            RecordUndo(ur => ur.AddDoMethod(parent, Node.MethodName.MoveChild, copy, index), null);
            return copy;
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
            RequireInEditedScene(node, "node");
            if (node == root) throw new InvalidOperationException("the scene root cannot be removed; open or create another scene instead");
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
            Tool_Node.SetOwnerRecursive(node, root);
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
            var kind = baseClass == "Node" ? "node" : "resource";
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
