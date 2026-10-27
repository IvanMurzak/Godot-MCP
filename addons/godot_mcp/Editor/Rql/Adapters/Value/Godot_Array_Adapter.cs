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
using System.Collections;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Godot arrays are read as their values (<see cref="GodotVariants.Elements"/>); any list is accepted where a Godot array is
    /// expected, so <c>set props["a"] = godot.find("B").props["a"]</c> copies one array property into another.
    /// </summary>
    public sealed class Godot_Array_Adapter : RqlTypeAdapter<global::Godot.Collections.Array>
    {
        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (target != typeof(global::Godot.Collections.Array) || value is not IEnumerable || value is string) return AdapterOutcome.NotHandled;
            try
            {
                result = GodotVariants.ToVariant(value).AsGodotArray();
                return AdapterOutcome.Ok;
            }
            catch (ArgumentException ex)
            {
                reason = ex.Message;
                return AdapterOutcome.Failed;
            }
        }
    }
}
#endif
