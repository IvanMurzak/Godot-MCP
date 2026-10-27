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
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// What <c>script-execute</c> returns: the snippet's result (the same serialized member Unity-MCP's
    /// <c>script-execute</c> returns) plus the output it printed. Pure-managed.
    /// </summary>
    [System.Serializable]
    [Description("The result of 'script-execute': the returned value and the output printed while the code ran.")]
    public class ScriptExecuteResult
    {
        [JsonInclude, JsonPropertyName("result")]
        [Description("The returned value: its type name and serialized value. 'Success' for code that returns nothing.")]
        public SerializedMember? Result { get; set; }

        [JsonInclude, JsonPropertyName("logs")]
        [Description("Lines printed while the code ran (GD.Print, push_warning / push_error, Console). Omitted when none.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Logs { get; set; }

        /// <summary>
        /// The value as compact text ("Dictionary&lt;string, int&gt;: {a: 1}") for the RQL tool format, or null when the
        /// serialized <see cref="Result"/> reads well as it is (a scalar). Not serialized: the Json format keeps Result.
        /// </summary>
        [JsonIgnore]
        internal string? Text { get; set; }

        public ScriptExecuteResult() { }

        public override string ToString() => Result?.ToString() ?? "null";
    }
}
