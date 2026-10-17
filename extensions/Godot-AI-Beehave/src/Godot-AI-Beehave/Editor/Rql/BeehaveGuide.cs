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
    /// The agent guide of the Beehave RQL module, rendered into the <c>godot-rql</c> skill (<see cref="BeehaveModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e
    /// manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D.
    /// </summary>
    public static class BeehaveGuide
    {
        public const string Topic = "Beehave";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "beehave | select installed hint   is the addon installed (hint: how to install it)\n" +
            "root | call AddChild(\"BeehaveTree\", \"BT\")   a behaviour-tree root; its actor defaults to its parent\n" +
            "godot.find(\"BT\") | call AddChild(\"SelectorComposite\", \"Sel\")   a composite: runs children until one does not fail\n" +
            "godot.find(\"BT/Sel\") | call AddChild(\"InverterDecorator\", \"Inv\")   a decorator wraps one child\n" +
            "godot.find(\"BT/Sel/Inv\") | call AddChild(\"ActionLeaf\", \"Act\")   a leaf placeholder (attach a script that extends ActionLeaf)\n" +
            "beehave.trees | where name == \"BT\" | get node | set props[\"tick_rate\"] = 2, props[\"enabled\"] = true   tree settings, through the node (undoable)\n" +
            "beehave.trees | select name enabled tickRate processThread actor nodeCount   the trees and their settings\n" +
            "beehave.nodes | where tree == \"BT\" | select name scriptClass kind depth path   the structure of a tree";

        public const string Markdown =
            "Beehave (the bitbrain/beehave addon) builds behaviour trees from nodes whose classes are GDScript class_names, so " +
            "they have no C# type and `class` shows their native base (\"Node\"). Create them with the core `AddChild` by class " +
            "name; the `beehave` source lists them as read-only rows that name the Beehave class (`scriptClass`) and hold the " +
            "node itself in `node`. Read through the rows, write through `get node` (`set props[\"…\"]`, `call AddChild`, " +
            "`Free()` …): those writes are undoable and mark the scene unsaved.\n\n```\n" + Examples + "\n```\n\n" +
            "Classes: the root `BeehaveTree`; composites `SelectorComposite`, `SequenceComposite`, `SelectorReactiveComposite`, " +
            "`SequenceReactiveComposite`, `SequenceStarComposite`, `SelectorRandomComposite`, `SequenceRandomComposite`, " +
            "`SimpleParallelComposite`; decorators `InverterDecorator`, `LimiterDecorator` (props[\"max_count\"]), " +
            "`RepeaterDecorator`, `CooldownDecorator`, `DelayDecorator`, `TimeLimiterDecorator`, `AlwaysSucceedDecorator`, " +
            "`AlwaysFailDecorator`, `UntilFailDecorator`; leaves `ActionLeaf` and `ConditionLeaf` (abstract: attach your own " +
            "script that extends them and implements `tick(actor, blackboard)` returning SUCCESS, RUNNING or FAILURE; " +
            "`set props[\"script\"] = \"res://…gd\"`), plus `BlackboardSetAction`, `BlackboardEraseAction`, `BlackboardHasCondition`, " +
            "`BlackboardCompareCondition`. A typical tree: a BeehaveTree under the actor, one SelectorComposite, condition and " +
            "action leaves below it. BeehaveTree properties: `enabled` (true), `tick_rate` (ticks every N frames, keep it at " +
            "least 1; 0 divides by zero at run time), `process_thread` (0 Idle, 1 Physics = default, 2 Manual), `actor` (a node " +
            "path; defaults to the parent), `blackboard`. `kind` of a row is tree, composite, decorator, action, condition, leaf " +
            "or node. Without the addon `beehave | select installed hint` says so and the lists are empty.";
    }
}
