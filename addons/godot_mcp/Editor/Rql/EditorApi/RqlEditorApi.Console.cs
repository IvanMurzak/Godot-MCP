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
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        /// <summary>Most entries the <c>console</c> source returns (the collector's capacity).</summary>
        public const int MaxLogEntries = GodotLogCollector.Capacity;

        /// <summary>The <c>console</c> source: the captured editor log lines, oldest first (stack traces included).</summary>
        public static List<LogEntry> ConsoleEntries() =>
            GodotLogCollector.Current is { } logs
                ? logs.Query(maxEntries: MaxLogEntries, includeStackTrace: true).OrderBy(e => e.Sequence).ToList()
                : new List<LogEntry>();
    }
}
#endif
