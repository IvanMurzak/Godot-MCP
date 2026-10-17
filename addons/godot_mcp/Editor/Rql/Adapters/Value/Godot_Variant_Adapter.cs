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
    /// <summary>
    /// Variant (the elements of Godot arrays and dictionaries, Variant-typed C# members): shown as the value it holds;
    /// any RQL value is accepted where a Variant is expected (lists and object literals become Godot arrays and
    /// dictionaries).
    /// </summary>
    public sealed class Godot_Variant_Adapter : RqlTypeAdapter<Variant>
    {
        public override string Description => "Variant: any value (number, text, bool, vector, color, object, list, map)";

        protected override JsonNode? Encode(Variant value, EncodeContext context) => context.EncodeValue(GodotVariants.ToClr(value));

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (target != typeof(Variant)) return AdapterOutcome.NotHandled;
            try
            {
                result = GodotVariants.ToVariant(value);
                return AdapterOutcome.Ok;
            }
            catch (ArgumentException ex)
            {
                reason = ex.Message;
                return AdapterOutcome.Failed;
            }
        }

        protected override void Members(MemberTable<Variant> m) => m.HideWhere(member => member.Name != "VariantType" && member.Name != "Obj");
    }
}
#endif
