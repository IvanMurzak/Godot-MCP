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
        /// property whose live value is not the template's (the value in the scene file, else the class default). Empty for a node
        /// that is not a scene instance. Transforms are told as position / rotation_degrees / scale.
        /// </summary>
        public static List<RqlOverride> OverridesOf(Node instance)
        {
            var list = new List<RqlOverride>();
            if (string.IsNullOrEmpty(instance.SceneFilePath) || instance == EditedRoot()) return list;
            if (ResourceLoader.Load<PackedScene>(instance.SceneFilePath) is not { } scene) return list;
            var state = scene.GetState();
            for (int i = 0; i < state.GetNodeCount(); i++)
            {
                var nodePath = state.GetNodePath(i).ToString();
                if (instance.GetNodeOrNull(nodePath) is not { } live) continue;
                var stored = new Dictionary<string, Variant>();
                for (int j = 0; j < state.GetNodePropertyCount(i); j++) stored[state.GetNodePropertyName(i, j)] = state.GetNodePropertyValue(i, j);
                var cls = live.GetClass();
                var shown = nodePath == "." ? "." : nodePath.StartsWith("./", StringComparison.Ordinal) ? nodePath.Substring(2) : nodePath;
                foreach (var name in GodotVariants.PropertyNames(live, storageOnly: true))
                {
                    if (name == "script" || name.StartsWith("metadata/", StringComparison.Ordinal)) continue;
                    var template = stored.TryGetValue(name, out var s) ? s : ClassDB.ClassGetPropertyDefaultValue(cls, name);
                    var current = live.Get(name);
                    var parts = name != "transform" ? null : (current.VariantType, template.VariantType) switch
                    {
                        (Variant.Type.Transform3D, Variant.Type.Transform3D) => OverrideDiff.Transform(current.AsTransform3D(), template.AsTransform3D()),
                        (Variant.Type.Transform2D, Variant.Type.Transform2D) => OverrideDiff.Transform(current.AsTransform2D(), template.AsTransform2D()),
                        _ => null,
                    };
                    if (parts != null)
                    {
                        list.AddRange(parts.Select(d => new RqlOverride(shown, d.property, d.live, d.template)));
                        continue;
                    }
                    var value = GodotVariants.ToClr(current);
                    var original = GodotVariants.ToClr(template);
                    if (!(OverrideDiff.ApproxSame(value, original) ?? Same(current, template)))
                        list.Add(new RqlOverride(shown, name, value, original));
                }
            }
            return list;
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

        [RqlDescription("Godot property name (a transform is told as position, rotation_degrees and scale)")]
        public string property { get; }

        [RqlDescription("the instance's value")]
        public object? value { get; }

        [RqlDescription("the scene file's value: set the property to it to revert the override")]
        public object? template { get; }
    }
}
#endif
