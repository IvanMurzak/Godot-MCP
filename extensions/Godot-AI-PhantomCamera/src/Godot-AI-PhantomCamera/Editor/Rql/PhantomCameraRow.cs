/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// One PhantomCamera3D / PhantomCamera2D of the edited scene (a row of <c>phantomcamera.cameras</c>): its settings read
    /// live from the node, the modes by name. Read-only; plain settings change through <c>node</c>
    /// (<c>get node | set props["priority"] = 50</c>, undoable). <c>SetFollow</c> / <c>SetLookAt</c> set a mode by name and
    /// its target in the order the addon needs, validated by <see cref="PhantomCameraRules"/>; they are calls on this row,
    /// not on the node, so they are NOT undoable: they mark the scene unsaved themselves.
    /// </summary>
    [RqlDescription("PhantomCamera3D / 2D (read-only row): name path scriptClass priority followMode followTarget lookAtMode lookAtTarget followDamping lookAtDamping node; calls SetFollow(mode, target?), SetLookAt(mode, target?)")]
    public sealed class PhantomCameraRow
    {
        private readonly bool _is2D;
        private readonly Node _root;

        internal PhantomCameraRow(Node node, Node root, string scriptClass, bool is2D)
        {
            this.node = node;
            _root = root;
            path = root.GetPathTo(node).ToString();
            this.scriptClass = scriptClass;
            _is2D = is2D;
        }

        [RqlDisplay, RqlDescription("node name")]
        public string name => node.Name.ToString();

        [RqlDescription("path below the edited scene root")]
        public string path { get; }

        [RqlDescription("PhantomCamera3D or PhantomCamera2D (or your class_name extending one)")]
        public string scriptClass { get; }

        [RqlDescription("priority (props[\"priority\"]): the highest-priority camera drives the host's Camera3D / Camera2D; at least 0")]
        public int priority => node.Get(PhantomCameraNames.Priority).AsInt32();

        [RqlDescription("None, Glued, Simple, Group, Path, Framed or ThirdPerson (3D only); set it with SetFollow")]
        public string followMode => PhantomCameraModes.Label<PhantomFollowMode>(node.Get(PhantomCameraNames.FollowMode).AsInt32());

        [RqlDescription("path of the followed node (props[\"follow_target\"]), or null")]
        public string? followTarget => PhantomCameraApi.PathInScene(_root, node.Get(PhantomCameraNames.FollowTarget).As<Node>());

        [RqlDescription("None, Mimic, Simple or Group (null for a PhantomCamera2D); set it with SetLookAt")]
        public string? lookAtMode => _is2D ? null : PhantomCameraModes.Label<PhantomLookAtMode>(node.Get(PhantomCameraNames.LookAtMode).AsInt32());

        [RqlDescription("path of the looked-at node (props[\"look_at_target\"]), or null")]
        public string? lookAtTarget => _is2D ? null : PhantomCameraApi.PathInScene(_root, node.Get(PhantomCameraNames.LookAtTarget).As<Node>());

        [RqlDescription("whether following is damped (props[\"follow_damping\"])")]
        public bool followDamping => node.Get(PhantomCameraNames.FollowDamping).AsBool();

        [RqlDescription("whether looking at is damped (props[\"look_at_damping\"]; null for a PhantomCamera2D)")]
        public bool? lookAtDamping => _is2D ? null : node.Get(PhantomCameraNames.LookAtDamping).AsBool();

        [RqlReference, RqlDescription("the camera node: write plain settings through it (get node | set props[\"priority\"] = 50)")]
        public Node node { get; }

        [RqlCallable("set the follow mode by name (None, Glued, Simple, Group, Path, Framed, ThirdPerson) and, optionally, the node to follow (a Node3D / Node2D; set after the mode, which the addon requires); validated; NOT undoable, marks the scene unsaved; returns the follow mode")]
        public string SetFollow(string mode, Node? target = null)
        {
            Check(PhantomCameraRules.FollowMode(mode, _is2D, out var followMode));
            Apply(PhantomCameraNames.FollowMode, (int)followMode, PhantomCameraNames.FollowTarget, target, () => PhantomCameraRules.FollowTarget(followMode));
            return followMode.ToString();
        }

        [RqlCallable("set the look-at mode by name (None, Mimic, Simple, Group; PhantomCamera3D only) and, optionally, the Node3D to look at (set after the mode, which the addon requires); validated; NOT undoable, marks the scene unsaved; returns the look-at mode")]
        public string SetLookAt(string mode, Node? target = null)
        {
            Check(PhantomCameraRules.LookAtMode(mode, _is2D, out var lookAtMode));
            Apply(PhantomCameraNames.LookAtMode, (int)lookAtMode, PhantomCameraNames.LookAtTarget, target, () => PhantomCameraRules.LookAtTarget(lookAtMode));
            return lookAtMode.ToString();
        }

        // Validate the target first, then write the mode and the target in the order the addon requires. When the addon
        // refuses the target, the previous mode and target are restored so a failed call changes nothing.
        private void Apply(string modeProperty, int mode, string targetProperty, Node? target, Func<string?> targetRule)
        {
            if (target != null)
            {
                Check(targetRule());
                CheckTarget(target);
            }
            var previousMode = node.Get(modeProperty);
            var previousTarget = node.Get(targetProperty);
            node.Set(modeProperty, mode);
            if (target != null)
            {
                try { SetTarget(targetProperty, target); }
                catch
                {
                    node.Set(modeProperty, previousMode);
                    node.Set(targetProperty, previousTarget);
                    throw;
                }
            }
            EditorInterface.Singleton.MarkSceneAsUnsaved();
        }

        private void CheckTarget(Node target) =>
            Check(PhantomCameraRules.TargetType(_is2D, _is2D ? target is Node2D : target is Node3D, target.Name.ToString(), target.GetClass()));

        private void SetTarget(string property, Node target)
        {
            node.Set(property, target);
            if (node.Get(property).As<Node>() != target)
                throw new InvalidOperationException($"Phantom Camera did not accept '{target.Name}' as {property}");
        }

        private static void Check(string? reason)
        {
            if (reason != null) throw new ArgumentException(reason);
        }
    }
}
#endif
