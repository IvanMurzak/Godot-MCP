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
using System.Globalization;
using System.Text.Json.Nodes;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// RID: an opaque handle of a server object (a World3D's space, a texture on the RenderingDevice …), shown as
    /// <c>"RID(1234)"</c> like Godot prints it and compared by id. Read-only: a handle only means something to the server
    /// that issued it, so no value is written as one.
    /// </summary>
    public sealed class Godot_Rid_Adapter : RqlTypeAdapter<Rid>
    {
        public override string Description => "RID: an opaque server handle, shown as \"RID(1234)\" (Id, IsValid); read-only";

        /// <summary>The text of a RID: <c>RID(id)</c>.</summary>
        public static string Text(Rid rid) => "RID(" + rid.Id.ToString(CultureInfo.InvariantCulture) + ")";

        protected override JsonNode? Encode(Rid value, EncodeContext context) => JsonValue.Create(Text(value));

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (target != typeof(Rid)) return AdapterOutcome.NotHandled;
            if (value is Rid rid) { result = rid; return AdapterOutcome.Ok; }
            reason = "a RID is a runtime handle of a server object and cannot be written";
            return AdapterOutcome.Failed;
        }

        protected override bool? Equal(Rid value, object? other, RqlKernel kernel) => other switch
        {
            Rid o => value.Id == o.Id,
            string s => string.Equals(Text(value), s.Replace(" ", ""), StringComparison.OrdinalIgnoreCase),
            double d => value.Id == d,
            long l => l >= 0 && value.Id == (ulong)l,
            _ => null,
        };

        protected override void Members(MemberTable<Rid> m) => m.HideWhere(member => member.Name != "Id" && member.Name != "IsValid");
    }
}
#endif
