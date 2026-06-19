/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Console
    {
        public const string ConsoleGetLogsToolId = "console-get-logs";

        [AiTool
        (
            ConsoleGetLogsToolId,
            Title = "Console / Get Logs",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Retrieve captured Godot-MCP editor log lines, newest-first. The Godot analog of " +
            "Unity's 'console-get-logs'. NOTE: Godot's C# API exposes no global log hook, so this returns " +
            "the plugin's own captured editor activity (not the entire Godot editor console).\n" +
            "Inputs:\n" +
            "  - 'maxEntries' (default 100, min 1): caps the returned array (most-recent lines kept).\n" +
            "  - 'logTypeFilter' (default null = all): restrict to Log / Warning / Error.\n" +
            "  - 'includeStackTrace' (default false): include stack-trace strings.\n" +
            "  - 'lastMinutes' (default 0 = all): only lines captured in the last N minutes.")]
        public LogEntry[] GetLogs
        (
            [Description("Maximum number of log entries to return. Minimum 1, default 100.")]
            int maxEntries = 100,
            [Description("Filter by severity (Log / Warning / Error). Null means all severities.")]
            GodotLogType? logTypeFilter = null,
            [Description("Include stack traces in the output. Default false.")]
            bool includeStackTrace = false,
            [Description("Return logs from the last N minutes. 0 returns all available logs. Default 0.")]
            int lastMinutes = 0
        )
        {
            if (maxEntries < 1)
                throw new ArgumentException($"maxEntries must be >= 1; got {maxEntries}.", nameof(maxEntries));

            var collector = GodotLogCollector.GetOrCreate();
            return collector.Query(
                maxEntries: maxEntries,
                logTypeFilter: logTypeFilter,
                includeStackTrace: includeStackTrace,
                lastMinutes: lastMinutes);
        }
    }
}
