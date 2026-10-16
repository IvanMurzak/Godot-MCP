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
    /// Pure-managed, serializable result every editor <c>beehave-*</c> tool returns. Holds ONLY primitives +
    /// strings + a child list (no Godot native types), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate, serializes cleanly through ReflectorNet, and the
    /// presence-gate (<see cref="AddonGate"/>) can produce one with no Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// The first four fields are the <b>presence gate</b> (Option A from the Class-B guide): when Beehave is
    /// absent every editor tool returns <c>Installed = false</c> + a hint instead of crashing. ReflectorNet
    /// serializes the C# <b>PascalCase</b> names, so the live e2e fixture asserts <c>"Installed":true</c>.
    /// </para>
    ///
    /// <para>
    /// <see cref="ClassName"/> carries the Beehave <c>class_name</c> (e.g. <c>"BeehaveTree"</c>,
    /// <c>"SelectorComposite"</c>). It is deliberately NOT <c>node.GetClass()</c> — a GDScript <c>class_name</c>
    /// node reports its native BASE (<c>"Node"</c>) from <c>GetClass()</c>, so the editor helpers resolve the
    /// script class name from the global-class list instead, keeping this field deterministic.
    /// </para>
    /// </summary>
    public sealed class BeehaveNodeInfo
    {
        /// <summary>Whether the Beehave addon was detected (the presence gate). False ⇒ the rest is unset.</summary>
        public bool Installed { get; set; }

        /// <summary>The wrapped addon's display name (<c>"Beehave"</c>).</summary>
        public string Addon { get; set; } = string.Empty;

        /// <summary>The class the gate probed for when <see cref="Installed"/> is false (else null).</summary>
        public string? MissingClass { get; set; }

        /// <summary>Actionable install hint when <see cref="Installed"/> is false (else empty).</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>Scene path of the node this result describes (empty for a gate-failure result).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>The Beehave <c>class_name</c> of the node (e.g. <c>"BeehaveTree"</c>); empty when gated.</summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>Scene path of the parent the node was added under (empty for the tree root / gate).</summary>
        public string ParentPath { get; set; } = string.Empty;

        /// <summary>BeehaveTree <c>enabled</c> flag (tree results only).</summary>
        public bool Enabled { get; set; }

        /// <summary>BeehaveTree <c>tick_rate</c> (tree results only; ticks every N frames).</summary>
        public int TickRate { get; set; }

        /// <summary>Scene path of the tree's <c>actor</c> node (tree results only; empty when unset).</summary>
        public string ActorPath { get; set; } = string.Empty;

        /// <summary>Number of authored (non-internal) child nodes.</summary>
        public int ChildCount { get; set; }

        /// <summary>The descendant Beehave structure (populated by <c>beehave-get</c>; else empty).</summary>
        public List<BeehaveChildInfo> Children { get; set; } = new();

        /// <summary>Build a gate-failure result for a missing Beehave class.</summary>
        public static BeehaveNodeInfo NotInstalled(string missingClass)
        {
            var gate = AddonGate.NotInstalled(missingClass);
            return new BeehaveNodeInfo
            {
                Installed = gate.Installed,
                Addon = gate.Addon,
                MissingClass = gate.MissingClass,
                Hint = gate.Hint
            };
        }
    }

    /// <summary>
    /// Pure-managed summary of one node in a Beehave tree dump (returned by <c>beehave-get</c>). Primitives +
    /// strings only, so it serializes cleanly and is CI-unit-testable.
    /// </summary>
    public sealed class BeehaveChildInfo
    {
        /// <summary>The node's name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The node's Beehave <c>class_name</c> (or its native class when not a Beehave node).</summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>Scene path of the node relative to the edited scene root.</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>Depth below the queried tree root (the root's direct children are depth 1).</summary>
        public int Depth { get; set; }
    }
}
