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
    /// Which Godot types an object literal may build (RQL spec §4.5, <see cref="MutationPolicy.CanConstructFromLiteral"/>).
    /// Since RQL 2877579 a type with an identity or liveness adapter is an engine object and is never built from a literal; the
    /// GodotObject adapter gives every Godot object both, which would refuse <c>set Mesh = {$type: "BoxMesh", …}</c>. A
    /// Resource built from a literal is what the editor's inspector does for "New BoxMesh": an embedded sub-resource with no
    /// side effect until it is assigned, so Resources stay constructible. Nodes (and other objects) keep RQL's default refusal:
    /// they are created through AddChild / editor.CreateNode, which add them to the scene with an undo step.
    /// Pure (no Godot API call): unit-tested.
    /// </summary>
    internal static class RqlLiteralPolicy
    {
        public static void Configure(MutationPolicy mutations) => mutations.CanConstructFromLiteral = CanConstruct;

        /// <summary>true for a Godot Resource type; null (RQL's default) for anything else.</summary>
        public static bool? CanConstruct(Type type) => typeof(Resource).IsAssignableFrom(type) ? true : (bool?)null;
    }
}
#endif
