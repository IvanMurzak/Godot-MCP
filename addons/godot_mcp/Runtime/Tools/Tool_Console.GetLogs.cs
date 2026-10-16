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
        [Description("Retrieve captured Godot-MCP editor log lines: newest-first by default, oldest-first when " +
            "polling with 'sinceSequence'. To fetch only new lines, pass the highest 'sequence' you have received " +
            "as 'sinceSequence'. The Godot analog of Unity's 'console-get-logs'. NOTE: Godot's C# API exposes no global log hook, so this returns " +
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
            "  - 'sinceSequence' (default 0, min 0): polling cursor. 0 = newest-first. > 0 = only lines with a " +
            "higher sequence, oldest-first, so the next poll (with the highest sequence returned) continues where " +
            "this page stopped. If returned sequences are lower than your cursor, the log restarted. Lines evicted " +
            "from the 1000-line buffer or cleared before you poll are not returned.\n" +
            "  - 'maxEntries' (default 100, min 1): caps the returned array — the most recent lines at " +
            "sinceSequence=0, the oldest page with a cursor.\n" +
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
            [Description("Polling cursor: pass the highest 'sequence' you have received to get only newer lines, " +
                "oldest-first. 0 returns lines newest-first. Minimum 0, default 0.")]
            long sinceSequence = 0
        )
        {
            if (maxEntries < 1)
                throw new ArgumentException($"maxEntries must be >= 1; got {maxEntries}.", nameof(maxEntries));
            if (sinceSequence < 0)
                throw new ArgumentException($"sinceSequence must be >= 0; got {sinceSequence}.", nameof(sinceSequence));

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
