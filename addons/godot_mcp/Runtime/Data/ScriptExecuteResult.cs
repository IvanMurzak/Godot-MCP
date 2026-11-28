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
using System.Collections.Generic;
using System.ComponentModel;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// What <c>csharp-exec</c> returns: the snippet's value as a plain object (values of the snippet's own types were
    /// converted to maps and lists before the snippet unloaded) and the output it printed. The tool binding presents
    /// <see cref="Result"/> with RQL (<c>Engine.Present</c>) after <see cref="TypeName"/>: <c>System.Int32: 3</c>,
    /// <c>Dictionary&lt;string, int&gt;: {a:1,b:2}</c>. Pure-managed.
    /// </summary>
    [Description("The result of 'csharp-exec': the returned value and the output printed while the code ran.")]
    public class ScriptExecuteResult
    {
        /// <summary>The returned value (null for a null result or code that returns nothing).</summary>
        public object? Result { get; set; }

        /// <summary>
        /// The type shown before the value: the full name of a scalar (<c>System.Int32</c>), the C# name of anything else
        /// (<c>List&lt;Vector3&gt;</c>, <c>anonymous</c>); null for code that returns nothing (shown as <c>Success</c>).
        /// </summary>
        public string? TypeName { get; set; }

        /// <summary>Lines printed while the code ran (GD.Print, push_warning / push_error, Console); null when none.</summary>
        public List<string>? Logs { get; set; }

        /// <summary>Most characters of the presented value; 0 = no limit.</summary>
        public int MaxValueChars { get; set; }

        public ScriptExecuteResult() { }

        public override string ToString() => TypeName == null ? "Success" : TypeName + ": " + (Result?.ToString() ?? "null");
    }
}
