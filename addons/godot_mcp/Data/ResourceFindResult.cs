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

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Structured result of a <c>resource-find</c> call: the list of matching <see cref="ResourceInfo"/>
    /// hits plus a count. The Godot analog of a multi-hit <c>assets-find</c> response.
    ///
    /// <para>
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host.
    /// </para>
    /// </summary>
    [System.Serializable]
    [Description("Result of resource-find: a count plus the list of matching resources (path/uid/type).")]
    public class ResourceFindResult
    {
        [JsonInclude, JsonPropertyName("count")]
        [Description("Number of matching resources found.")]
        public int Count { get; set; } = 0;

        [JsonInclude, JsonPropertyName("resources")]
        [Description("The matching resources, each with its res:// path, uid, and type.")]
        public List<ResourceInfo> Resources { get; set; } = new();

        public ResourceFindResult() { }

        public override string ToString() => $"ResourceFindResult ({Count} matches)";
    }
}
