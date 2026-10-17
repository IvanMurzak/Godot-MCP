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
    /// <summary>StringName: shown and compared as text; text is accepted wherever a StringName is expected.</summary>
    public sealed class Godot_StringName_Adapter : RqlTypeAdapter<StringName>
    {
        public override string Description => "StringName: write as text, e.g. \"my_name\"";

        protected override JsonNode? Encode(StringName value, EncodeContext context) => JsonValue.Create(value.ToString());

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            reason = null;
            result = value is string text ? new StringName(text) : null;
            return result != null ? AdapterOutcome.Ok : AdapterOutcome.NotHandled;
        }

        protected override bool? Equal(StringName value, object? other, RqlKernel kernel) => other switch
        {
            string s => value.ToString() == s,
            StringName o => value.ToString() == o.ToString(),
            _ => null,
        };
    }
}
#endif
