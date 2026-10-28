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
    /// Which Godot types an object literal may build (<see cref="MutationPolicy.CanConstructFromLiteral"/>). RQL refuses to
    /// build a type with an identity or liveness adapter, and the GodotObject adapter gives every Godot object both. A
    /// Resource literal is an embedded sub-resource (like the inspector's "New BoxMesh"), so Resources stay constructible;
    /// Nodes keep the refusal (AddChild / editor.CreateNode add them to the scene with an undo step). Pure: unit-tested.
    /// </summary>
    internal static class RqlLiteralPolicy
    {
        public static void Configure(MutationPolicy mutations) => mutations.CanConstructFromLiteral = CanConstruct;

        /// <summary>true for a Godot Resource type; null (RQL's default) for anything else.</summary>
        public static bool? CanConstruct(Type type) => typeof(Resource).IsAssignableFrom(type) ? true : (bool?)null;

        /// <summary>
        /// The refusal of a literal naming (or written to) a non-Resource Godot class, for the checks the addon makes before RQL
        /// would: script classes it builds itself and the call validators (dry runs fail like the real call).
        /// </summary>
        public static string Refusal(string className) =>
            $"{className} is an engine object and cannot be built from an object literal; pass an existing one " +
            $"(e.g. ref(\"{className}#…\") or a node path) or create it with AddChild / editor.CreateNode";
    }
}
#endif
