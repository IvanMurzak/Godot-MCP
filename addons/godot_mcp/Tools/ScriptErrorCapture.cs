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
using com.IvanMurzak.Godot.MCP.Data;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Coarse engine-error category surfaced by Godot's logging hook — the pure-managed mirror of Godot
    /// 4.5's <c>Logger.ErrorType</c> (Error / Warning / Script / Shader), so the routing/classification
    /// logic can be unit-tested in the plain xUnit host with no Godot binary. The 4.5-only <c>Logger</c>
    /// subclass (behind <c>#if GODOT4_5_OR_GREATER</c>) maps the engine enum onto this one before forwarding.
    /// </summary>
    public enum EngineErrorKind
    {
        /// <summary>A generic engine error (<c>ERROR_TYPE_ERROR</c>).</summary>
        Error = 0,
        /// <summary>A generic engine warning (<c>ERROR_TYPE_WARNING</c>).</summary>
        Warning = 1,
        /// <summary>A GDScript parse/compile error (<c>ERROR_TYPE_SCRIPT</c>) — the one this feature targets.</summary>
        Script = 2,
        /// <summary>A shader error (<c>ERROR_TYPE_SHADER</c>).</summary>
        Shader = 3,
    }

    /// <summary>
    /// Pure-managed router for engine log/error callbacks captured via Godot 4.5's <c>OS.AddLogger</c> hook.
    /// It does TWO independent jobs from a single engine callback, decoupled from the Godot type so both are
    /// unit-testable:
    ///
    /// <list type="number">
    /// <item><b>Passive log capture</b> (always on): every engine error/warning is appended to the supplied
    /// <see cref="GodotLogCollector"/> so <c>console-get-logs</c> finally sees engine-wide GDScript parse
    /// errors — not just the plugin's own <c>GD.Print</c> output.</item>
    /// <item><b>On-demand validation</b> (only while a capture session is active): when
    /// <see cref="BeginSession"/> has opened a session, <see cref="EngineErrorKind.Script"/> callbacks are
    /// additionally collected as structured <see cref="ScriptDiagnostic"/> rows that the
    /// <c>script-validate</c> tool harvests after a deliberate <c>Reload()</c>.</item>
    /// </list>
    ///
    /// <para>
    /// Godot's logger callback is multi-threaded, so every buffer mutation is guarded by a lock. The
    /// session is a simple single-active-session model (validation runs serially on the editor main thread),
    /// captured under the same lock.
    /// </para>
    /// </summary>
    public sealed class ScriptErrorCapture
    {
        readonly object _gate = new();
        List<ScriptDiagnostic>? _session;
        string? _sessionTarget;

        /// <summary>
        /// Process-wide capture installed by the 4.5 <c>Logger</c> hook at editor boot. Null when the live
        /// Godot version predates 4.5 (no <c>OS.AddLogger</c>) or before boot wiring runs; callers must
        /// null-check / fall back to the per-file <c>Reload()</c> probe in that case.
        /// </summary>
        public static ScriptErrorCapture? Current { get; set; }

        /// <summary>
        /// Optional sink for passive log capture — typically <see cref="GodotLogCollector.Append(GodotLogType, string, string)"/>
        /// bound at boot. Kept as a delegate so this router stays pure-managed (the editor boot injects the
        /// real collector; unit tests inject a fake). May be null (capture-session-only mode).
        /// </summary>
        public Action<GodotLogType, string>? LogSink { get; set; }

        /// <summary>True while a validation capture session is open (see <see cref="BeginSession"/>).</summary>
        public bool SessionActive
        {
            get { lock (_gate) { return _session != null; } }
        }

        /// <summary>
        /// Open a fresh validation capture session, discarding any prior session's buffer. While open,
        /// <see cref="EngineErrorKind.Script"/> callbacks are collected as <see cref="ScriptDiagnostic"/>
        /// rows. Pair with <see cref="EndSession"/> in a finally so a thrown reload never leaks a session.
        /// </summary>
        /// <param name="targetPath">
        /// The <c>res://</c> path being validated. When non-empty, ONLY engine errors whose reported file
        /// matches this path are collected — this prevents cross-talk, since Godot's logger callback is
        /// multi-threaded and an unrelated off-thread script error (a background reimport, the next file in
        /// a full scan, or a dependency error touching ANOTHER file) can fire during this session's window
        /// and would otherwise be silently mis-attributed to <paramref name="targetPath"/>. Pass null/empty
        /// to collect every script error regardless of path (legacy behavior; used only when no specific
        /// file is under test).
        /// </param>
        public void BeginSession(string? targetPath = null)
        {
            lock (_gate)
            {
                _session = new List<ScriptDiagnostic>();
                _sessionTarget = string.IsNullOrEmpty(targetPath) ? null : targetPath;
            }
        }

        /// <summary>
        /// Close the active session and return the diagnostics it captured (a fresh copy). Returns an empty
        /// array when no session was open. Safe to call without a matching <see cref="BeginSession"/>.
        /// </summary>
        public ScriptDiagnostic[] EndSession()
        {
            lock (_gate)
            {
                var captured = _session?.ToArray() ?? Array.Empty<ScriptDiagnostic>();
                _session = null;
                _sessionTarget = null;
                return captured;
            }
        }

        /// <summary>
        /// Route one engine callback. Appends to <see cref="LogSink"/> (passive capture, always) and — when a
        /// session is open and the callback is a <see cref="EngineErrorKind.Script"/> error whose file matches
        /// the session target (see <see cref="BeginSession"/>) — records a structured diagnostic.
        /// <paramref name="line"/> may be -1 when unknown; <paramref name="filePath"/> is the engine source
        /// path (kept as-is; it may be a <c>res://</c> or absolute path). Thread-safe.
        /// </summary>
        public void Route(EngineErrorKind kind, string? filePath, int line, string? message, string? rationale)
        {
            // Build the human line: prefer the engine "rationale" (the actual error text) and fall back to
            // the C++ assertion "message" (the failed condition) when no rationale is present.
            var text = !string.IsNullOrEmpty(rationale) ? rationale!
                     : !string.IsNullOrEmpty(message) ? message!
                     : "(no message)";

            var logType = kind == EngineErrorKind.Warning ? GodotLogType.Warning : GodotLogType.Error;

            // Passive capture: surface a path:line prefix so console-get-logs is self-describing.
            LogSink?.Invoke(logType, FormatLogLine(kind, filePath, line, text));

            // Validation capture: only script errors, only while a session is open.
            if (kind != EngineErrorKind.Script)
                return;

            lock (_gate)
            {
                if (_session == null)
                    return;

                // Path-match filter: when the session targets a specific file, drop script errors the engine
                // reported against a DIFFERENT file. Godot's logger callback is multi-threaded, so an error
                // from an unrelated off-thread reload (a background reimport, the next scanned file, or a
                // dependency error in ANOTHER script) can fire mid-session and would otherwise be silently
                // mis-attributed to the file under validation. An empty engine path is kept (the caller stamps
                // the target path on it) since the engine occasionally omits the source for a script error.
                if (_sessionTarget != null
                    && !string.IsNullOrEmpty(filePath)
                    && !PathsMatch(filePath, _sessionTarget))
                {
                    return;
                }

                _session.Add(new ScriptDiagnostic(
                    path: filePath ?? string.Empty,
                    line: line,
                    message: text,
                    severity: ScriptDiagnosticSeverity.Error));
            }
        }

        /// <summary>
        /// Compare an engine-reported source path against the session target. The session target is always a
        /// <c>res://</c> path (from <c>RequireScriptResPath</c> / the res:// scan), but Godot's logger callback
        /// documents the reported file as possibly a <c>res://</c> OR an absolute/<c>user://</c> path. A plain
        /// equality check would therefore SILENTLY DROP the genuine diagnostic when the engine reports an
        /// absolute path for the file under test. Both sides are normalized before comparing:
        /// <list type="bullet">
        /// <item>strip the <c>res://</c> / <c>user://</c> scheme,</item>
        /// <item>normalize back-slashes to forward-slashes (Windows absolute paths),</item>
        /// <item>case-insensitive ordinal comparison (file systems and the engine vary letter case).</item>
        /// </list>
        /// An absolute engine path matches when its tail equals the scheme-stripped res:// target (e.g.
        /// <c>C:/proj/scripts/p.gd</c> ⊃ <c>scripts/p.gd</c> from <c>res://scripts/p.gd</c>). Kept pure-managed
        /// (no <c>ProjectSettings.LocalizePath</c>) so the router stays unit-testable in the plain xUnit host.
        /// <para>
        /// The match is asymmetric on purpose: only the ENGINE path may be the longer (absolute) form of the
        /// target — never the reverse. A reversed <c>target.EndsWith("/" + engine)</c> clause would over-match
        /// when a deeper target shares a basename with a shallower unrelated file (e.g. target
        /// <c>res://scripts/ui/player.gd</c> wrongly collecting an error for <c>res://player.gd</c>), re-opening
        /// the exact cross-talk this filter exists to prevent — so it is deliberately omitted.
        /// </para>
        /// </summary>
        static bool PathsMatch(string enginePath, string target)
        {
            var a = NormalizePath(enginePath);
            var b = NormalizePath(target);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
                || a.EndsWith("/" + b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Strip the Godot resource scheme and normalize separators for path comparison.</summary>
        static string NormalizePath(string path)
        {
            var p = path.Replace('\\', '/');
            if (p.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                return p.Substring("res://".Length);
            if (p.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
                return p.Substring("user://".Length);
            return p;
        }

        /// <summary>
        /// Format a captured engine error/warning into a single console line:
        /// <c>"[Script] res://x.gd:12 — unexpected token"</c>. Pure string logic, unit-tested.
        /// </summary>
        public static string FormatLogLine(EngineErrorKind kind, string? filePath, int line, string text)
        {
            var loc = string.IsNullOrEmpty(filePath)
                ? string.Empty
                : (line >= 0 ? $" {filePath}:{line}" : $" {filePath}");
            return $"[{kind}]{loc} — {text}";
        }
    }
}
