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
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat
{
    /// <summary>
    /// A tool argument whose .NET type the tool decides when it runs (reflection-method-call: the target and arguments of
    /// the method it found): the agent's RQL value as evaluated (<c>ref("Node3D#12")</c>, <c>[1, 2, 3]</c>, an object
    /// literal), converted with <see cref="TryTo"/> by the same rules as <c>set</c> and every other tool argument.
    /// </summary>
    public sealed class RqlValue
    {
        private readonly RqlKernel _kernel;

        internal RqlValue(object? raw, RqlKernel kernel)
        {
            Raw = raw;
            _kernel = kernel;
        }

        /// <summary>The evaluated value (an RQL literal, a live object from <c>ref</c> / <c>godot.find</c>, text, a number).</summary>
        public object? Raw { get; }

        /// <summary>The value converted to <paramref name="type"/> (adapters, object literals, numbers, enums); the reason when it cannot be.</summary>
        public bool TryTo(Type type, out object? value, out string? error) => _kernel.TryCoerce(Raw, type, out value, out error);
    }

    /// <summary>Named <see cref="RqlValue"/>s: an RQL map <c>{amount: 5, at: [1, 2, 3]}</c> whose value types the tool decides.</summary>
    public sealed class RqlValues
    {
        internal RqlValues(IReadOnlyList<KeyValuePair<string, RqlValue>> items) => Items = items;

        /// <summary>In the order given.</summary>
        public IReadOnlyList<KeyValuePair<string, RqlValue>> Items { get; }
    }
}
