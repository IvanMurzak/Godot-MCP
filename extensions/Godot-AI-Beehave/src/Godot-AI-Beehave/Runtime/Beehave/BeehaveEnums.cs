/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// Pure-managed (no Godot native types, CI-unit-testable) SOURCE OF TRUTH for the third-party
    /// <c>Beehave</c> addon's <c>class_name</c> identifiers, GDScript <c>snake_case</c> member names, and
    /// enum-int values. Because this is a <b>Class-B</b> (addon-dependent) extension, the addon's classes are
    /// NOT in GodotSharp and are referenced ONLY by string name — there are no compile-time enum types to lean
    /// on, so <b>these constants ARE the contract</b>. They are pinned by <c>BeehaveEnumsTests</c> so an
    /// upstream rename can never drift silently. Verified against <c>bitbrain/beehave</c> tag <c>v2.9.2</c>.
    /// </summary>
    public static class BeehaveEnums
    {
        // ── class_name identifiers (GDScript global-class list; NOT in ClassDB) ────────────────────────

        /// <summary>The class the presence gate probes for ("is Beehave installed?"). Behaviour-tree root.</summary>
        public const string PresenceClass = "BeehaveTree";

        /// <summary>The behaviour-tree root node class (<c>nodes/beehave_tree.gd</c>).</summary>
        public const string TreeClass = "BeehaveTree";

        /// <summary>The internal blackboard class (<c>blackboard.gd</c>).</summary>
        public const string BlackboardClass = "Blackboard";

        /// <summary>Composite — children evaluated until one succeeds (<c>composites/selector.gd</c>).</summary>
        public const string SelectorClass = "SelectorComposite";

        /// <summary>Composite — children evaluated until one fails (<c>composites/sequence.gd</c>).</summary>
        public const string SequenceClass = "SequenceComposite";

        /// <summary>Decorator — inverts its single child's result (<c>decorators/inverter.gd</c>).</summary>
        public const string InverterClass = "InverterDecorator";

        /// <summary>Decorator — limits how often its child may run (<c>decorators/limiter.gd</c>).</summary>
        public const string LimiterClass = "LimiterDecorator";

        /// <summary>Leaf — a user-authored action (<c>leaves/action.gd</c>). Abstract → scaffold only.</summary>
        public const string ActionLeafClass = "ActionLeaf";

        /// <summary>Leaf — a user-authored condition (<c>leaves/condition.gd</c>). Abstract → scaffold only.</summary>
        public const string ConditionLeafClass = "ConditionLeaf";

        // ── GDScript snake_case member names driven dynamically via GodotObject.Set/Get ────────────────

        /// <summary>BeehaveTree <c>enabled</c> bool — whether the tree ticks (default true).</summary>
        public const string EnabledMember = "enabled";

        /// <summary>BeehaveTree <c>tick_rate</c> int — ticks every N frames (default 1).</summary>
        public const string TickRateMember = "tick_rate";

        /// <summary>BeehaveTree <c>actor</c> Node — the node the tree drives (defaults to its parent).</summary>
        public const string ActorMember = "actor";

        /// <summary>BeehaveTree <c>actor_node_path</c> NodePath — actor addressed by path.</summary>
        public const string ActorNodePathMember = "actor_node_path";

        /// <summary>BeehaveTree <c>process_thread</c> int — see <see cref="ProcessThreadIdle"/> etc.</summary>
        public const string ProcessThreadMember = "process_thread";

        // ── enum ProcessThread { IDLE, PHYSICS, MANUAL } — plain ints (no compile-time enum) ───────────

        /// <summary>BeehaveTree.ProcessThread.IDLE (ticks in <c>_process</c>).</summary>
        public const int ProcessThreadIdle = 0;

        /// <summary>BeehaveTree.ProcessThread.PHYSICS (ticks in <c>_physics_process</c>; the addon default).</summary>
        public const int ProcessThreadPhysics = 1;

        /// <summary>BeehaveTree.ProcessThread.MANUAL (the host calls <c>tick()</c> itself).</summary>
        public const int ProcessThreadManual = 2;
    }
}
