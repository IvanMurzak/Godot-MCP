/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// RQL's literal kinds for Godot (spec §4.5): a Resource is a <b>value</b>, so an object literal builds a new one (an embedded
    /// sub-resource, like the inspector's "New BoxMesh"), GDExtension and script Resources included (the GodotObject adapter
    /// builds those itself); every other Godot object keeps the default <b>engine</b> kind its identity / liveness adapter gives
    /// it and is never built from a literal: Nodes are added with AddChild / editor.CreateNode (one undo step). The refusal ends
    /// with Godot's ways to name an existing object (<see cref="RefusalHint"/>). Pure: unit-tested.
    /// </summary>
    internal static class RqlLiteralPolicy
    {
        /// <summary>The advice after a refused Node literal: how to reference or create a node instead.</summary>
        public const string NodeAdvice =
            "pass an existing node (godot.find(\"Name\"), its path or ref(\"Class#id\")) or create one with AddChild / editor.CreateNode";

        /// <summary>The advice after any other refused Godot object literal.</summary>
        public const string ObjectAdvice = "pass an existing one (ref(\"Class#id\"))";

        public static void Configure(MutationPolicy mutations)
        {
            mutations.LiteralKindOf = KindOf;
            mutations.LiteralRefusalHint = RefusalHint;
        }

        /// <summary>Value for a Godot Resource type; null (RQL's default: engine for other Godot objects) for anything else.</summary>
        public static LiteralKind? KindOf(Type type) => typeof(Resource).IsAssignableFrom(type) ? LiteralKind.Value : (LiteralKind?)null;

        /// <summary>The end of RQL's engine-object refusal for a Godot type; null keeps RQL's default for other types.</summary>
        public static string? RefusalHint(Type type) =>
            typeof(Node).IsAssignableFrom(type) ? NodeAdvice : typeof(GodotObject).IsAssignableFrom(type) ? ObjectAdvice : null;

        /// <summary>
        /// The refusal of a literal naming (or written to) a non-Resource Godot class, for the checks the addon makes before RQL
        /// would: script classes it builds itself and the call validators (dry runs fail like the real call). Same text as RQL's.
        /// </summary>
        public static string Refusal(string className) =>
            $"{className} is an engine object and cannot be built from an object literal; {NodeAdvice}";
    }
}
#endif
