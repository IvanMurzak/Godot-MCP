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
using System.Collections.Generic;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// A typed array of resources or nodes (<see cref="GodotVariants.ClrType"/> maps it to <c>List&lt;Resource&gt;</c> /
    /// <c>List&lt;Node&gt;</c>) also takes another array property's value (read as a Godot array's elements), so
    /// <c>set props["a"] = props["b"]</c> keeps working: RQL itself only converts its own list literals element by element.
    /// </summary>
    public sealed class Godot_ObjectList_Adapter<T> : RqlTypeAdapter<List<T>> where T : GodotObject
    {
        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            // RQL list literals keep RQL's own element conversion (object literals, refs, paths).
            if (target != typeof(List<T>) || value is not IEnumerable items || value is string || value is List<object?>)
                return AdapterOutcome.NotHandled;
            var list = new List<T>();
            foreach (var item in items)
            {
                var plain = item is Variant variant ? GodotVariants.ToClr(variant) : item;
                if (!kernel.TryCoerce(plain, typeof(T), out var element, out var why, $"[{list.Count}]"))
                {
                    reason = why;
                    return AdapterOutcome.Failed;
                }
                list.Add((T)element!);
            }
            result = list;
            return AdapterOutcome.Ok;
        }
    }
}
#endif
