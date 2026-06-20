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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_RuntimeErrors
    {
        public const string RuntimeErrorsGetToolId = "runtime-errors-get";

        [AiTool
        (
            RuntimeErrorsGetToolId,
            Title = "Runtime Errors / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Retrieve errors raised inside the RUNNING game — GDScript runtime errors, " +
            "push_error/push_warning, shader errors (Godot 4.5+ engine hook), and C# unhandled / unobserved-" +
            "Task exceptions (with full managed stack traces). This is the in-game counterpart to " +
            "'console-get-logs' / 'script-validate' (which are editor-side): it lets an agent driving a live " +
            "game detect real gameplay/runtime bugs instead of assuming the game is healthy because the editor " +
            "console is quiet.\n" +
            "Poll loop: pass the 'highestSequence' from the previous call as 'sinceSequence' to get ONLY newer " +
            "errors. Start with sinceSequence=0 (default) for everything captured so far.\n" +
            "Inputs:\n" +
            "  - 'sinceSequence' (default 0): return only errors with a sequence number greater than this.\n" +
            "  - 'maxEntries' (default 100, min 1): cap the returned page (newest kept; 'truncated' flags a cap).\n" +
            "Result (RuntimeErrorsResult): 'available' (false when the game did not enable runtime error " +
            "capture — then an empty list proves nothing), 'ok' (no error-severity entries), the 'errors' " +
            "[{ sequence, message, type, source, file, line, function, stackTrace, frames, timestamp }] " +
            "oldest-first, counts, 'highestSequence' (poll with it next), and a 'truncated' flag. On Godot " +
            "4.5+ a GDScript runtime error also carries 'frames' (the deep multi-frame call stack, " +
            "innermost-first, each { function, file, line }) and a formatted 'stackTrace'.\n" +
            "SECURITY: messages and stack traces are forwarded verbatim and may contain sensitive runtime " +
            "data (absolute filesystem paths, machine/user names, or a secret that appeared in an exception). " +
            "Capture is OFF by default and the developer must enable it via WithRuntimeErrorCapture(); enable " +
            "it only on a trusted loopback + token connection.")]
        public RuntimeErrorsResult Get
        (
            [Description("Return only errors with a sequence number greater than this. Pass the previous " +
                "result's 'highestSequence' to poll only new errors. Default 0 (all captured).")]
            long sinceSequence = 0,
            [Description("Maximum number of errors to return. Minimum 1, default 100. Newest kept when capped.")]
            int maxEntries = 100
        )
        {
            if (maxEntries < 1)
                throw new ArgumentException($"maxEntries must be >= 1; got {maxEntries}.", nameof(maxEntries));

            var collector = RuntimeErrorCollector.Current;
            if (collector == null)
            {
                return new RuntimeErrorsResult
                {
                    Available = false,
                    Ok = true,
                    Note = "Runtime error capture is not enabled in this game. Initialize the in-game runtime " +
                           "with GodotMcpRuntime.Initialize(b => b.WithRuntimeErrorCapture()) to capture in-game " +
                           "runtime errors. (Absence of errors here does NOT mean the game is error-free.)",
                };
            }

            var errors = collector.QuerySince(sinceSequence, maxEntries, out var truncated);

            var errorCount = errors.Count(e => IsErrorSeverity(e));
            var warningCount = errors.Length - errorCount;

            var result = new RuntimeErrorsResult
            {
                Available = true,
                Count = errors.Length,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                HighestSequence = collector.HighestSequence,
                Truncated = truncated,
                Errors = errors.ToList(),
                Ok = errorCount == 0,
            };
            result.Note = BuildNote(result, sinceSequence);
            return result;
        }

        /// <summary>
        /// Error-vs-warning classification for the result counts. Engine WARNING entries (and only those) are
        /// warnings; everything else — engine Error/Script/Shader and BOTH managed-fault sources — is an
        /// error (a C# unhandled / unobserved-Task exception is always error-severity).
        /// </summary>
        static bool IsErrorSeverity(RuntimeError e)
        {
            if (e.Source != RuntimeErrorSource.Engine)
                return true; // managed faults are always errors
            // Engine 'Type' carries the EngineErrorKind name; only "Warning" is non-error.
            return !string.Equals(e.Type, nameof(EngineErrorKind.Warning), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Compose the human-readable summary note.</summary>
        static string BuildNote(RuntimeErrorsResult result, long sinceSequence)
        {
            var scope = sinceSequence > 0 ? $" since sequence {sinceSequence}" : string.Empty;

            string note;
            if (result.Count == 0)
            {
                note = $"No new runtime errors{scope} (capture active).";
            }
            else
            {
                var noun = result.ErrorCount == 1 ? "error" : "errors";
                note = $"{result.ErrorCount} runtime {noun}{scope}.";
                if (result.WarningCount > 0)
                    note += $" {result.WarningCount} warning(s).";
            }

            if (result.Truncated)
                note += $" NOTE: more than {result.Count} matched — only the newest {result.Count} were " +
                        "returned; poll again with 'sinceSequence' = 'highestSequence'.";

            return note;
        }
    }
}
