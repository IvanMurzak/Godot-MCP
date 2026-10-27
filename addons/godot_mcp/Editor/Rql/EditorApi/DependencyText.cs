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
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// One entry of Godot's <c>ResourceLoader.GetDependencies</c>: <c>"uid://…::Type::res://path"</c> (Godot 4.3+; any slice may be
    /// empty) or a bare <c>"res://path"</c>. Pure (unit-tested).
    /// </summary>
    public static class DependencyText
    {
        /// <summary>The entry's res:// path, else its uid:// (resolved by the caller), else empty.</summary>
        public static string PathOf(string? entry)
        {
            var parts = (entry ?? "").Split(new[] { "::" }, StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            return parts.LastOrDefault(p => p.StartsWith("res://", StringComparison.Ordinal))
                   ?? parts.FirstOrDefault(p => p.StartsWith("uid://", StringComparison.Ordinal))
                   ?? "";
        }
    }
}
#endif
