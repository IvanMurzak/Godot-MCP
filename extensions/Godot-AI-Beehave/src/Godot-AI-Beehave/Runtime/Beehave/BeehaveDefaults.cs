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
    /// Pure-managed (no Godot native types, CI-unit-testable) recommended starter SKELETON for a Beehave
    /// behaviour tree — the structured config the <c>beehave-defaults</c> tool returns. It needs no addon and no
    /// scene, so an LLM can call it any time to discover a sane tree shape before scaffolding the real nodes
    /// with the editor tools (<c>beehave-tree-create</c> → <c>-add-composite</c> → <c>-add-decorator</c> →
    /// <c>-add-leaf</c>). This is the Class-B analog of Particles' <c>ParticlesDefaults</c>.
    /// </summary>
    public static class BeehaveDefaults
    {
        /// <summary>The recommended tree root class (<see cref="BeehaveEnums.TreeClass"/>).</summary>
        public const string DefaultRootClass = BeehaveEnums.TreeClass;

        /// <summary>The recommended top-level composite kind for a typical AI: a fallback selector.</summary>
        public const string DefaultCompositeKind = "selector";

        /// <summary>The recommended starter <c>tick_rate</c> (every frame).</summary>
        public const int DefaultTickRate = 1;

        /// <summary>
        /// A recommended starter skeleton as a fully-populated <see cref="BeehaveSkeleton"/>: a
        /// <c>BeehaveTree</c> root, a top-level <c>SelectorComposite</c>, and two leaf placeholders (a condition
        /// and an action) the user fills in with their own scripts. Beehave leaves are abstract, so the
        /// skeleton describes scaffolding only — never synthesised behaviour.
        /// </summary>
        public static BeehaveSkeleton Recommended() => new()
        {
            RootClass = DefaultRootClass,
            TickRate = DefaultTickRate,
            CompositeKind = DefaultCompositeKind,
            CompositeClass = BeehaveNodeKinds.ResolveCompositeClass(DefaultCompositeKind),
            Leaves = new List<string> { BeehaveEnums.ConditionLeafClass, BeehaveEnums.ActionLeafClass },
            Note =
                "Scaffold a tree with beehave-tree-create (root BeehaveTree under your actor), then " +
                "beehave-add-composite (a SelectorComposite picks the first child that does not fail), then " +
                "beehave-add-leaf for each ConditionLeaf/ActionLeaf. Beehave leaves are abstract: attach your " +
                "own GDScript to each leaf to implement tick(actor, blackboard) -> SUCCESS|RUNNING|FAILURE."
        };
    }

    /// <summary>
    /// Pure-managed, serializable recommended behaviour-tree skeleton (the <c>beehave-defaults</c> result).
    /// Primitives + strings + a leaf-class list only, so it serializes cleanly through ReflectorNet and is
    /// CI-unit-testable with no Godot binary.
    /// </summary>
    public sealed class BeehaveSkeleton
    {
        /// <summary>The recommended tree root class (<c>"BeehaveTree"</c>).</summary>
        public string RootClass { get; set; } = string.Empty;

        /// <summary>The recommended root <c>tick_rate</c>.</summary>
        public int TickRate { get; set; }

        /// <summary>The recommended top-level composite kind string (<c>"selector"</c>).</summary>
        public string CompositeKind { get; set; } = string.Empty;

        /// <summary>The Beehave class for <see cref="CompositeKind"/> (<c>"SelectorComposite"</c>).</summary>
        public string CompositeClass { get; set; } = string.Empty;

        /// <summary>The recommended leaf placeholder classes, in order.</summary>
        public List<string> Leaves { get; set; } = new();

        /// <summary>Human/LLM-facing guidance on how to build and fill in the tree.</summary>
        public string Note { get; set; } = string.Empty;
    }
}
