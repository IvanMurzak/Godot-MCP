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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Pure-managed factory that maps the two in-game error sources into <see cref="RuntimeError"/> rows:
    /// the engine error stream (<see cref="EngineErrorRecord"/>, via the 4.5 <c>OS.AddLogger</c> hook) and
    /// managed C# faults (<see cref="System.Exception"/>, via the <c>AppDomain.UnhandledException</c> /
    /// <c>TaskScheduler.UnobservedTaskException</c> hooks). Split out of <see cref="RuntimeErrorCapture"/> so
    /// the mapping/formatting is unit-testable in the plain xUnit host with NO Godot binary and NO live
    /// runtime — the install/hook wiring (which touches <c>OS.AddLogger</c> / static AppDomain events) is the
    /// Godot/runtime-coupled part verified by the headless smoke.
    ///
    /// No Godot API surface, no <c>#if TOOLS</c> — ships into a game build.
    /// </summary>
    public static class RuntimeErrorFactory
    {
        /// <summary>
        /// Build a <see cref="RuntimeError"/> from an engine error-stream callback. The engine yields the
        /// ORIGIN (file / line / function) + message + kind. On Godot 4.5+ a GDScript runtime error ALSO
        /// carries the deep multi-frame call stack (issue #163): the record's already-materialized
        /// <see cref="EngineErrorRecord.Frames"/> become <see cref="RuntimeError.Frames"/> and its
        /// <see cref="EngineErrorRecord.StackTrace"/> becomes <see cref="RuntimeError.StackTrace"/>. On Godot
        /// &lt; 4.5 (or for an engine error with no tracked backtrace) both stay null/empty — the documented
        /// origin-only fallback. The engine kind is rendered as the <see cref="RuntimeError.Type"/> string
        /// (Error / Warning / Script / Shader).
        /// </summary>
        public static RuntimeError FromEngine(EngineErrorRecord record)
        {
            // Copy the record's frames into a fresh List<> the RuntimeError owns — the record's IReadOnlyList
            // is already plain managed primitives (materialized off the non-thread-safe ScriptBacktrace inside
            // the logger callback), so this is a pure managed copy with no Godot object involved.
            List<RuntimeErrorFrame>? frames = record.Frames != null && record.Frames.Count > 0
                ? record.Frames.ToList()
                : null;

            return new RuntimeError(
                source: RuntimeErrorSource.Engine,
                message: record.Message ?? string.Empty,
                type: record.Kind.ToString(),
                file: record.FilePath,
                line: record.Line,
                function: record.Function,
                stackTrace: record.StackTrace,
                frames: frames);
        }

        /// <summary>
        /// Build a <see cref="RuntimeError"/> from a managed C# fault. <paramref name="source"/> distinguishes
        /// an <c>AppDomain.UnhandledException</c> from a <c>TaskScheduler.UnobservedTaskException</c>. The
        /// exception's runtime type name becomes <see cref="RuntimeError.Type"/>, its message becomes
        /// <see cref="RuntimeError.Message"/>, and the FULL stack trace (flattened across inner exceptions)
        /// becomes <see cref="RuntimeError.StackTrace"/> — the deep managed stack the engine hook cannot give.
        /// Null-safe: a null exception yields a placeholder row rather than throwing inside a fault handler.
        /// </summary>
        public static RuntimeError FromException(RuntimeErrorSource source, Exception? exception)
        {
            if (exception == null)
            {
                return new RuntimeError(
                    source: source,
                    message: "(null exception)",
                    type: "System.Exception");
            }

            // ToString() on an Exception already renders "Type: Message\n   at frame...\n --- inner ---" with
            // every inner exception inlined — exactly the full-fidelity stack the issue asks for. We still set
            // Message/Type to the OUTER exception's discrete fields so the structured row stays queryable.
            return new RuntimeError(
                source: source,
                message: exception.Message ?? string.Empty,
                type: exception.GetType().FullName ?? exception.GetType().Name,
                stackTrace: exception.ToString());
        }
    }
}
