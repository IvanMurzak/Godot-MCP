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
using System.Text.Json.Nodes;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>NodePath: shown and compared as text; text is accepted wherever a NodePath is expected.</summary>
    public sealed class Godot_NodePath_Adapter : RqlTypeAdapter<NodePath>
    {
        public override string Description => "NodePath: write as text, e.g. \"Level/Player\"";

        protected override JsonNode? Encode(NodePath value, EncodeContext context) => JsonValue.Create(value.ToString());

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            reason = null;
            result = value is string text ? new NodePath(text) : null;
            return result != null ? AdapterOutcome.Ok : AdapterOutcome.NotHandled;
        }

        protected override bool? Equal(NodePath value, object? other, RqlKernel kernel) => other switch
        {
            string s => value.ToString() == s,
            NodePath o => value.ToString() == o.ToString(),
            _ => null,
        };
    }
}
#endif
