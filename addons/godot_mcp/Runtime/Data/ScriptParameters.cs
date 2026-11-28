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

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// The parameters of a <c>csharp-exec</c> snippet: each one a name, the .NET type the snippet declares it with, and its
    /// value, already converted to that type. Built from the agent's RQL value by <c>Godot_ScriptParameters_Adapter</c>
    /// (<c>{n: 6, "Vector3 at": [1, 2, 3], target: ref("Node3D#12")}</c>). Pure-managed.
    /// </summary>
    public sealed class ScriptParameters
    {
        public ScriptParameters(IReadOnlyList<ScriptParameter> items) => Items = items ?? throw new ArgumentNullException(nameof(items));

        /// <summary>In the order given: the snippet's variables, or the full-source method's arguments.</summary>
        public IReadOnlyList<ScriptParameter> Items { get; }
    }

    /// <summary>One <c>csharp-exec</c> parameter: its name, the type it is declared with and its value.</summary>
    public sealed class ScriptParameter
    {
        public ScriptParameter(string name, Type type, object? value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Value = value;
        }

        public string Name { get; }
        public Type Type { get; }
        public object? Value { get; }
    }
}
