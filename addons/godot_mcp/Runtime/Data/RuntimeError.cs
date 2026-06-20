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
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Where a captured <see cref="RuntimeError"/> originated. Lets the agent tell an engine-side error
    /// (a GDScript runtime error, <c>push_error</c>, shader error — captured via Godot 4.5's
    /// <c>OS.AddLogger</c> hook) apart from a managed C# fault (an unhandled exception or an unobserved
    /// faulted <see cref="System.Threading.Tasks.Task"/>), which carry a full managed stack trace.
    /// </summary>
    [Description("Origin of a captured in-game runtime error.")]
    public enum RuntimeErrorSource
    {
        /// <summary>
        /// The Godot engine's error/warning stream (GDScript runtime error, <c>push_error</c>,
        /// <c>push_warning</c>, shader error, generic engine error) — captured via the 4.5+
        /// <c>OS.AddLogger</c> hook. Carries file / line / function but NOT a deep call stack.
        /// </summary>
        [Description("Godot engine error stream (GDScript runtime / push_error / push_warning / shader).")]
        Engine = 0,

        /// <summary>
        /// A managed <see cref="System.AppDomain.UnhandledException"/> — a C# exception that escaped to the
        /// top of a thread. Carries the exception type, message, and full managed stack trace.
        /// </summary>
        [Description("C# unhandled exception (AppDomain.CurrentDomain.UnhandledException) with full stack trace.")]
        UnhandledException = 1,

        /// <summary>
        /// A managed <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/> — a faulted
        /// <see cref="System.Threading.Tasks.Task"/> whose exception was never awaited/observed. Carries the
        /// exception type, message, and full managed stack trace.
        /// </summary>
        [Description("C# unobserved faulted-Task exception (TaskScheduler.UnobservedTaskException) with full stack trace.")]
        UnobservedTaskException = 2,
    }

    /// <summary>
    /// One captured in-game runtime error — the structured row returned by the <c>runtime-errors-get</c>
    /// MCP tool. Closes the gap (issue #160) where errors raised inside a RUNNING game (not just editor-side
    /// script errors) were invisible to the agent: an agent could launch the game, poll for logs, see
    /// silence, and wrongly conclude the game was healthy.
    ///
    /// <para>
    /// Field availability depends on the <see cref="Source"/>:
    /// <list type="bullet">
    /// <item><b><see cref="RuntimeErrorSource.Engine"/></b> (Godot 4.5+ logger hook): <see cref="File"/>,
    /// <see cref="Line"/>, <see cref="Function"/> are the error's ORIGIN. On Godot 4.5+ a GDScript runtime
    /// error ALSO carries the deep multi-frame call stack (issue #163): <see cref="Frames"/> is the ordered
    /// (innermost-first) backtrace and <see cref="StackTrace"/> is its formatted rendering. On Godot &lt; 4.5
    /// (or when no backtrace was tracked) <see cref="Frames"/> is null/empty and <see cref="StackTrace"/> is
    /// null (origin only).</item>
    /// <item><b><see cref="RuntimeErrorSource.UnhandledException"/> /
    /// <see cref="RuntimeErrorSource.UnobservedTaskException"/></b>: <see cref="StackTrace"/> carries the
    /// full managed stack; <see cref="File"/>/<see cref="Line"/>/<see cref="Function"/> are empty/-1 (the
    /// origin is inside the stack trace) and <see cref="Frames"/> is null (the managed stack lives in the
    /// string).</item>
    /// </list>
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host and ships into a game build.
    /// </summary>
    [System.Serializable]
    [Description("A single captured in-game runtime error: message, type, origin (file/line/function), " +
        "source, an optional managed stack trace, a monotonic sequence number, and a UTC timestamp.")]
    public class RuntimeError
    {
        [JsonInclude, JsonPropertyName("sequence")]
        [Description("Monotonic 1-based sequence number assigned when captured. Pass the largest you have " +
            "seen as 'sinceSequence' to 'runtime-errors-get' to poll only NEWER errors.")]
        public long Sequence { get; set; } = 0;

        [JsonInclude, JsonPropertyName("message")]
        [Description("The error message text.")]
        public string Message { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("type")]
        [Description("The error type/severity: for engine errors one of Error / Warning / Script / Shader; " +
            "for managed faults the CLR exception type name (e.g. 'System.NullReferenceException').")]
        public string Type { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("source")]
        [Description("Where the error came from: Engine (Godot error stream) / UnhandledException / " +
            "UnobservedTaskException (managed C# faults with a full stack trace).")]
        public RuntimeErrorSource Source { get; set; } = RuntimeErrorSource.Engine;

        [JsonInclude, JsonPropertyName("file")]
        [Description("Origin file of an engine error (e.g. 'res://scripts/player.gd'); empty for managed " +
            "faults (the origin is in the stack trace instead).")]
        public string File { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("line")]
        [Description("1-based origin line of an engine error, or -1 when unknown / for managed faults.")]
        public int Line { get; set; } = -1;

        [JsonInclude, JsonPropertyName("function")]
        [Description("Origin function of an engine error; empty when unknown / for managed faults.")]
        public string Function { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("stackTrace")]
        [Description("Stack trace string: for a C# fault (UnhandledException / UnobservedTaskException) the " +
            "full managed stack; for a Godot 4.5+ engine GDScript error the formatted multi-frame backtrace " +
            "(see 'frames'); null for engine errors with no tracked backtrace (Godot < 4.5 or release builds " +
            "without call-stack tracking).")]
        public string? StackTrace { get; set; } = null;

        [JsonInclude, JsonPropertyName("frames")]
        [Description("Structured deep backtrace for a Godot 4.5+ engine GDScript error: ordered stack frames " +
            "(innermost-first), each with function / file / line. Null for managed C# faults (their stack is " +
            "in 'stackTrace') and for engine errors with no tracked backtrace (Godot < 4.5 or release builds " +
            "without call-stack tracking).")]
        public List<RuntimeErrorFrame>? Frames { get; set; } = null;

        [JsonInclude, JsonPropertyName("timestamp")]
        [Description("UTC timestamp when the error was captured.")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public RuntimeError() { }

        public RuntimeError(
            RuntimeErrorSource source,
            string message,
            string type,
            string? file = null,
            int line = -1,
            string? function = null,
            string? stackTrace = null,
            DateTime? timestamp = null,
            List<RuntimeErrorFrame>? frames = null)
        {
            Source = source;
            Message = message ?? string.Empty;
            Type = type ?? string.Empty;
            File = file ?? string.Empty;
            Line = line;
            Function = function ?? string.Empty;
            StackTrace = string.IsNullOrEmpty(stackTrace) ? null : stackTrace;
            Frames = frames != null && frames.Count > 0 ? frames : null;
            Timestamp = timestamp ?? DateTime.UtcNow;
        }

        public override string ToString()
        {
            var loc = string.IsNullOrEmpty(File)
                ? string.Empty
                : (Line >= 0 ? $" {File}:{Line}" : $" {File}");
            var fn = string.IsNullOrEmpty(Function) ? string.Empty : $" ({Function})";
            return $"#{Sequence} [{Source}/{Type}]{loc}{fn} — {Message}";
        }
    }
}
