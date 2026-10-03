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
        [Description("Retrieve captured Godot-MCP editor log lines. By default (sinceSequence=0), returns " +
            "newest-first. When sinceSequence > 0, acts as a polling cursor and returns only entries newer " +
            "than that sequence, oldest-first, useful for agents fetching log deltas. The Godot analog of " +
            "Unity's 'console-get-logs'. NOTE: Godot's C# API exposes no global log hook, so this returns " +
            "the plugin's own captured editor activity (not the entire Godot editor console) — including its " +
            "connection lifecycle diagnostics (connect/disconnect, drain-timeout, config save/load, skill-gen, " +
            "dev-control, dispatcher, and runtime-capture warnings).\n" +
            "SCOPE WARNING: this is the EDITOR process only. Godot runs a play-session started with " +
            "'editor-application-set-state' as a SEPARATE OS process, and no editor API exposes that " +
            "child's stdout or the editor Output panel to a plugin — so the running game's GD.Print output " +
            "will NEVER appear here, and an empty result is NOT evidence that the game printed nothing. " +
            "A running game's print output is not readable through ANY tool today. The closest available " +
            "channel covers ERRORS only: have the game initialize the in-game runtime " +
            "(GodotMcpRuntime.Initialize(b => b.WithRuntimeErrorCapture()).Build() + Connect()) and read " +
            "'runtime-errors-get'.\n" +
            "Inputs:\n" +
            "  - 'sinceSequence' (default 0): polling cursor. 0 = return all available, newest-first. " +
            "    When > 0, return only entries with sequence > this value, oldest-first.\n" +
            "  - 'maxEntries' (default 100, min 1): caps the returned array (most-recent lines kept when capping).\n" +
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
            int lastMinutes = 0,
            [Description("Polling cursor: 0 returns all available entries (newest-first); when > 0, " +
                "returns only entries with sequence > this value (oldest-first). Use the highest sequence " +
                "from a prior call to fetch only new entries. Default 0.")]
            long sinceSequence = 0
        )
        {
            if (maxEntries < 1)
                throw new ArgumentException($"maxEntries must be >= 1; got {maxEntries}.", nameof(maxEntries));

            var collector = GodotLogCollector.GetOrCreate();
            return collector.Query(
                maxEntries: maxEntries,
                logTypeFilter: logTypeFilter,
                includeStackTrace: includeStackTrace,
                lastMinutes: lastMinutes,
                sinceSequence: sinceSequence);
        }
    }
}
