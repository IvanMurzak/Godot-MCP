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
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Severity of a captured editor log line — the Godot analog of Unity's <c>UnityEngine.LogType</c>.
    /// Godot's logging surface (<c>GD.Print</c> / <c>GD.PushWarning</c> / <c>GD.PushError</c>) collapses
    /// to three levels, so this enum mirrors them rather than Unity's five (Unity's Assert/Exception have
    /// no distinct Godot counterpart).
    /// </summary>
    [Description("Severity of a captured editor log line: Log, Warning, or Error.")]
    public enum GodotLogType
    {
        [Description("Informational message (GD.Print).")]
        Log = 0,
        [Description("Warning message (GD.PushWarning).")]
        Warning = 1,
        [Description("Error message (GD.PushError).")]
        Error = 2,
    }

    /// <summary>
    /// One captured editor log line — the Godot analog of Unity-MCP's <c>LogEntry</c>. Holds the
    /// severity, message text, a timestamp, and an optional stack trace. Produced by
    /// <see cref="Tools.GodotLogCollector"/> and returned by the <c>console-get-logs</c> tool.
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain
    /// xUnit host.
    /// </summary>
    [System.Serializable]
    [Description("A captured Godot editor log line: severity, message, timestamp, and optional stack trace.")]
    public class LogEntry
    {
        [JsonInclude, JsonPropertyName("logType")]
        [Description("Severity of the log line (Log / Warning / Error).")]
        public GodotLogType LogType { get; set; } = GodotLogType.Log;

        [JsonInclude, JsonPropertyName("message")]
        [Description("The log message text.")]
        public string Message { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("timestamp")]
        [Description("UTC timestamp when the line was captured.")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonInclude, JsonPropertyName("stackTrace")]
        [Description("Optional stack trace associated with the line; null when none was captured.")]
        public string? StackTrace { get; set; } = null;

        public LogEntry() { }

        public LogEntry(GodotLogType logType, string message, DateTime timestamp, string? stackTrace = null)
        {
            LogType = logType;
            Message = message ?? string.Empty;
            Timestamp = timestamp;
            StackTrace = string.IsNullOrEmpty(stackTrace) ? null : stackTrace;
        }

        public override string ToString() => ToString(includeStackTrace: false);

        public string ToString(bool includeStackTrace)
            => includeStackTrace && !string.IsNullOrEmpty(StackTrace)
                ? $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{LogType}] {Message}\nStack Trace:\n{StackTrace}"
                : $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{LogType}] {Message}";
    }
}
