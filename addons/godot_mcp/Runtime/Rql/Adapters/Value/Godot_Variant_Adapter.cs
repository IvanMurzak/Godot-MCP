/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Text.Json.Nodes;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Variant (the elements of Godot arrays and dictionaries, Variant-typed C# members and tool parameters): shown as the
    /// value it holds; any RQL value is accepted where a Variant is expected (lists and object literals become Godot arrays
    /// and dictionaries, a whole number an int as in GDScript: <c>42</c> is Int, <c>42.5</c> Float).
    /// </summary>
    public sealed class Godot_Variant_Adapter : RqlTypeAdapter<Variant>
    {
        public override string Description => "Variant: any value (number — a whole one is an int —, text, bool, vector, color, object, list, map)";

        protected override JsonNode? Encode(Variant value, EncodeContext context) => context.EncodeValue(GodotVariants.ToClr(value));

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (target != typeof(Variant)) return AdapterOutcome.NotHandled;
            try
            {
                result = GodotVariants.ToVariant(Input(value));
                return AdapterOutcome.Ok;
            }
            catch (ArgumentException ex)
            {
                reason = ex.Message;
                return AdapterOutcome.Failed;
            }
        }

        /// <summary>
        /// The CLR value a Variant is made from: RQL numbers are doubles, and a whole one becomes a long (Variant Int), as a
        /// GDScript literal 42 would; anything else is unchanged. Pure (unit-tested; making the Variant needs Godot).
        /// </summary>
        internal static object? Input(object? value) =>
            value is double d && Math.Floor(d) == d && Math.Abs(d) < 9e15 ? (long)d : value;

        protected override void Members(MemberTable<Variant> m) => m.HideWhere(member => member.Name != "VariantType" && member.Name != "Obj");
    }
}
