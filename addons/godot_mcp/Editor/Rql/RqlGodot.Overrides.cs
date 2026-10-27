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
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public static partial class RqlGodot
    {
        /// <summary>
        /// How a scene instance differs from its scene file: for the instance root and every node of the instanced scene, each stored
        /// property whose live value is not the one a fresh instance of the file has (so nested instances, inherited scenes and script
        /// variables compare with their real defaults). A Node3D transform is told as position / rotation_degrees / scale; the
        /// instance root's position (its placement) is left out. Empty for a node that is not a scene instance.
        /// </summary>
        public static List<RqlOverride> OverridesOf(Node instance)
        {
            var list = new List<RqlOverride>();
            if (string.IsNullOrEmpty(instance.SceneFilePath) || instance == EditedRoot()) return list;
            if (ResourceLoader.Load<PackedScene>(instance.SceneFilePath) is not { } scene) return list;
            var fresh = scene.Instantiate(PackedScene.GenEditState.Instance);
            try
            {
                foreach (var template in WithDescendants(fresh))
                {
                    var nodePath = fresh.GetPathTo(template).ToString();
                    if (instance.GetNodeOrNull(nodePath) is not { } live) continue;
                    foreach (var name in GodotVariants.PropertyNames(live, storageOnly: true))
                    {
                        if (name == "script" || name.StartsWith("metadata/", StringComparison.Ordinal)) continue;
                        var current = live.Get(name);
                        var original = template.Get(name);
                        if (name == "transform" && current.VariantType == Variant.Type.Transform3D && original.VariantType == Variant.Type.Transform3D)
                        {
                            list.AddRange(OverrideDiff.Transform(current.AsTransform3D(), original.AsTransform3D())
                                .Where(d => nodePath != "." || d.property != "position")
                                .Select(d => new RqlOverride(nodePath, d.property, d.live, d.template)));
                            continue;
                        }
                        var value = GodotVariants.ToClr(current);
                        var old = GodotVariants.ToClr(original);
                        if ((value != null || old != null) && !(OverrideDiff.ApproxSame(value, old) ?? Same(current, original)))
                            list.Add(new RqlOverride(nodePath, name, value, old));
                    }
                }
            }
            finally
            {
                fresh.Free();
            }
            return list;
        }

        private static IEnumerable<Node> WithDescendants(Node node)
        {
            yield return node;
            foreach (var child in node.GetChildren(includeInternal: false))
                foreach (var n in WithDescendants(child))
                    yield return n;
        }
    }

    /// <summary>One property in which a scene instance differs from its scene file (<c>overrides</c>).</summary>
    public sealed class RqlOverride
    {
        internal RqlOverride(string node, string property, object? value, object? template)
        {
            this.node = node;
            this.property = property;
            this.value = value;
            this.template = template;
        }

        [RqlDescription("the node inside the instance (\".\" = the instance root, \"Body\" = its child Body)")]
        public string node { get; }

        [RqlDescription("Godot property name, snake_case (a Node3D transform is told as position, rotation_degrees and scale)")]
        public string property { get; }

        [RqlDescription("the instance's value")]
        public object? value { get; }

        [RqlDescription("the scene file's value: revert with set props[\"<property>\"] = <template> on that node")]
        public object? template { get; }
    }
}
#endif
