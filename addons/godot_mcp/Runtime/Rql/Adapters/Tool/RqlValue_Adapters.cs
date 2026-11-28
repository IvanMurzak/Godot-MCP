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
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// <see cref="RqlValue"/> tool arguments: any RQL value, kept with the kernel that evaluated it so the tool converts it to
    /// the type it finds at run time (kernels are immutable and reusable, RQL's own contract).
    /// </summary>
    public sealed class RqlValue_Adapter : RqlTypeAdapter<RqlValue>
    {
        public override string? Description =>
            "any RQL value, converted to the type the tool needs: ref(\"Node3D#12\"), godot.find(\"Name\"), a node path, \"res://…\", " +
            "[1, 2, 3], {key: value, …}, text, a number";

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            reason = null;
            result = value as RqlValue ?? new RqlValue(value, kernel);
            return AdapterOutcome.Ok;
        }
    }

    /// <summary><see cref="RqlValues"/> tool arguments: an RQL map of named values whose types the tool decides.</summary>
    public sealed class RqlValues_Adapter : RqlTypeAdapter<RqlValues>
    {
        public override string? Description =>
            "named values {amount: 5, at: [1, 2, 3], target: ref(\"Node3D#12\")}, each converted to the type of the parameter it names";

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            reason = null;
            result = null;
            if (!(value is ObjectValue named)) return AdapterOutcome.NotHandled;
            result = new RqlValues(named.Select(kv => new KeyValuePair<string, RqlValue>(kv.Key, new RqlValue(kv.Value, kernel))).ToList());
            return AdapterOutcome.Ok;
        }
    }
}
