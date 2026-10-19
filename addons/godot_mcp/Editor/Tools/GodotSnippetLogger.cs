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
using System;
using com.IvanMurzak.CodeExec;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Captures what a <c>script-execute</c> snippet prints through the engine (<c>GD.Print</c>, <c>GD.PushWarning</c>,
    /// <c>GD.PushError</c>). Godot 4.5+ only: an engine logger (<c>OS.AddLogger</c>) is registered for the run and
    /// removed right after, so no addon object stays rooted in the engine across a C# reload. On older engines only
    /// <c>Console</c> output is captured.
    /// </summary>
    public sealed class GodotPrintLogCapture : ILogCapture
    {
        public IDisposable Begin(Action<LogEntry> sink)
        {
#if GODOT4_5_OR_GREATER
            var logger = new GodotSnippetLogger(sink);
            OS.AddLogger(logger);
            return new Registration(logger);
        }

        sealed class Registration : IDisposable
        {
            GodotSnippetLogger? _logger;

            public Registration(GodotSnippetLogger logger) { _logger = logger; }

            public void Dispose()
            {
                var logger = _logger;
                _logger = null;
                if (logger == null)
                    return;
                logger.Detach();
                OS.RemoveLogger(logger);
            }
        }
#else
            return NoCapture.Instance;
        }

        sealed class NoCapture : IDisposable
        {
            public static readonly NoCapture Instance = new NoCapture();
            public void Dispose() { }
        }
#endif
    }

#if GODOT4_5_OR_GREATER
    /// <summary>The engine logger behind <see cref="GodotPrintLogCapture"/>. The engine may call it from any thread.</summary>
    public sealed partial class GodotSnippetLogger : Logger
    {
        volatile Action<LogEntry>? _sink;

        // Godot re-instantiates script classes through the parameterless constructor (hot reload).
        public GodotSnippetLogger() { }

        public GodotSnippetLogger(Action<LogEntry> sink) { _sink = sink; }

        public void Detach() => _sink = null;

        public override void _LogMessage(string message, bool error)
            => _sink?.Invoke(new LogEntry(error ? "error" : "info", message.TrimEnd('\r', '\n')));

        // errorType is bound as Int32 (Error=0, Warning=1, Script=2, Shader=3); see GodotScriptErrorLogger.
        public override void _LogError(
            string function,
            string file,
            int line,
            string code,
            string rationale,
            bool editorNotify,
            int errorType,
            global::Godot.Collections.Array<global::Godot.ScriptBacktrace> scriptBacktraces)
            => _sink?.Invoke(new LogEntry(errorType == 1 ? "warning" : "error", string.IsNullOrEmpty(rationale) ? code : rationale));
    }
#endif
}
#endif
