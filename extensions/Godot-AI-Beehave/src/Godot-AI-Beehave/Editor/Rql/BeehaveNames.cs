/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// The Beehave addon's names, the contract of the module: Beehave classes are GDScript <c>class_name</c>s (no C# type,
    /// not in ClassDB), so they are referenced by string only. Verified against <c>bitbrain/beehave</c> v2.9.2. Pure-managed
    /// (no Godot API), so it is unit-tested.
    /// </summary>
    public static class BeehaveNames
    {
        /// <summary>The class whose presence in the global script-class list means "Beehave is installed".</summary>
        public const string TreeClass = "BeehaveTree";

        // Base classes that decide a node's kind (nodes/beehave_node.gd, composites/composite.gd, decorators/decorator.gd,
        // leaves/leaf.gd, leaves/action.gd, leaves/condition.gd).
        public const string NodeClass = "BeehaveNode";
        public const string CompositeClass = "Composite";
        public const string DecoratorClass = "Decorator";
        public const string LeafClass = "Leaf";
        public const string ActionLeafClass = "ActionLeaf";
        public const string ConditionLeafClass = "ConditionLeaf";

        // BeehaveTree's GDScript properties (snake_case, read / written with props["…"]).
        public const string EnabledProperty = "enabled";
        public const string TickRateProperty = "tick_rate";
        public const string ActorProperty = "actor";
        public const string ProcessThreadProperty = "process_thread";

        public const string InstallHint =
            "Beehave is not installed: install 'Beehave' from the Godot Asset Library (or bitbrain/beehave v2.9.2 into " +
            "res://addons/beehave/), enable it under Project Settings -> Plugins, then rebuild.";

        /// <summary>
        /// The kind of a node from its script-class chain (nearest class first): <c>tree</c>, <c>composite</c>,
        /// <c>decorator</c>, <c>action</c>, <c>condition</c>, <c>leaf</c>, <c>node</c> (another BeehaveNode), or null when
        /// the node is not a Beehave node. The most specific match wins (an ActionLeaf is an <c>action</c>, not a <c>leaf</c>).
        /// </summary>
        public static string? KindOf(IEnumerable<string> classChain)
        {
            foreach (var cls in classChain)
            {
                switch (cls)
                {
                    case TreeClass: return "tree";
                    case CompositeClass: return "composite";
                    case DecoratorClass: return "decorator";
                    case ActionLeafClass: return "action";
                    case ConditionLeafClass: return "condition";
                    case LeafClass: return "leaf";
                    case NodeClass: return "node";
                }
            }
            return null;
        }

        /// <summary>BeehaveTree.ProcessThread (IDLE = 0, PHYSICS = 1, MANUAL = 2) as a label.</summary>
        public static string ProcessThreadLabel(int value) => value switch
        {
            0 => "Idle",
            1 => "Physics",
            2 => "Manual",
            _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
