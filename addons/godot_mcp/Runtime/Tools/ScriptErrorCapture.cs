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
    /// One structured engine-error callback forwarded to <see cref="ScriptErrorCapture.ErrorSink"/> — the
    /// full origin (kind + file + line + function + already-resolved human message), distinct from the
    /// flattened single-line form <see cref="ScriptErrorCapture.LogSink"/> receives. Consumed by the in-game
    /// runtime to build <see cref="Data.RuntimeError"/> rows that preserve file/line/function. Pure-managed.
    ///
    /// <para>
    /// On Godot 4.5+, an engine SCRIPT error also carries the deep multi-frame GDScript call stack (issue
    /// #163): <see cref="Frames"/> is the ALREADY-MATERIALIZED list of managed frame primitives, and
    /// <see cref="StackTrace"/> is the engine's formatted backtrace string. Both are copied off the live
    /// non-thread-safe <c>ScriptBacktrace</c> inside the logger callback on the originating thread — this
    /// record only ever holds plain managed values, never a live Godot handle, so it is safe to forward
    /// across the thread boundary into the collector. On &lt; 4.5 (or for non-script engine errors) both are
    /// null/empty, preserving the origin-only behavior.
    /// </para>
    /// </summary>
    public readonly struct EngineErrorRecord
    {
        /// <summary>The engine error category (Error / Warning / Script / Shader).</summary>
        public EngineErrorKind Kind { get; }
        /// <summary>Origin source path (res:// or absolute), or null/empty when the engine omitted it.</summary>
        public string? FilePath { get; }
        /// <summary>1-based origin line, or -1 when unknown.</summary>
        public int Line { get; }
        /// <summary>Origin function name, or null/empty when the engine omitted it.</summary>
        public string? Function { get; }
        /// <summary>The resolved human-readable error text (rationale preferred, else the C++ condition).</summary>
        public string Message { get; }

        /// <summary>
        /// The deep multi-frame backtrace, innermost-first, materialized off Godot 4.5+'s
        /// <c>ScriptBacktrace</c> (issue #163). Null/empty when no backtrace was available (Godot &lt; 4.5,
        /// release builds without call-stack tracking, or a non-script error). Always plain managed values —
        /// never a live Godot object.
        /// </summary>
        public IReadOnlyList<RuntimeErrorFrame>? Frames { get; }

        /// <summary>
        /// The engine's formatted backtrace string (from <c>ScriptBacktrace.Format()</c>), or null when no
        /// backtrace was available. A human-readable multi-line rendering of <see cref="Frames"/>, surfaced as
        /// <see cref="Data.RuntimeError.StackTrace"/>.
        /// </summary>
        public string? StackTrace { get; }

        public EngineErrorRecord(EngineErrorKind kind, string? filePath, int line, string? function, string message)
            : this(kind, filePath, line, function, message, frames: null, stackTrace: null)
        {
        }

        public EngineErrorRecord(EngineErrorKind kind, string? filePath, int line, string? function, string message,
            IReadOnlyList<RuntimeErrorFrame>? frames, string? stackTrace)
        {
            Kind = kind;
            FilePath = filePath;
            Line = line;
            Function = function;
            Message = message;
            // Normalize an empty backtrace to null so consumers have a single "no frames" sentinel.
            Frames = frames != null && frames.Count > 0 ? frames : null;
            StackTrace = string.IsNullOrEmpty(stackTrace) ? null : stackTrace;
        }
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

        /// <summary>
        /// Optional STRUCTURED sink for full engine-error records (kind + file + line + function + message),
        /// distinct from the flattened-string <see cref="LogSink"/>. The in-game runtime
        /// (<c>RuntimeErrorCapture</c>) wires this so engine errors raised in a running game are captured as
        /// structured <see cref="Data.RuntimeError"/> rows for the <c>runtime-errors-get</c> tool — preserving
        /// the origin fields the formatted log line collapses. Invoked on every engine callback regardless of
        /// validation-session state (errors fire whether or not a <see cref="BeginSession"/> is open). May be
        /// null (the editor passive-log path leaves it unset and uses <see cref="LogSink"/> only).
        /// </summary>
        public Action<EngineErrorRecord>? ErrorSink { get; set; }

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
        /// Route one engine callback. Appends to <see cref="LogSink"/> (passive flattened capture, always),
        /// forwards a structured record to <see cref="ErrorSink"/> (always, when wired — the in-game runtime's
        /// path), and — when a session is open and the callback is a <see cref="EngineErrorKind.Script"/> error
        /// whose file matches the session target (see <see cref="BeginSession"/>) — records a structured
        /// diagnostic. <paramref name="line"/> may be -1 when unknown; <paramref name="filePath"/> is the engine
        /// source path (kept as-is; it may be a <c>res://</c> or absolute path); <paramref name="function"/> is
        /// the originating function name (may be null/empty when the engine omits it). Thread-safe.
        /// <para>
        /// <paramref name="frames"/> / <paramref name="stackTrace"/> carry the deep multi-frame GDScript
        /// backtrace (issue #163), already materialized off Godot 4.5+'s non-thread-safe
        /// <c>ScriptBacktrace</c> INSIDE the logger callback (on the originating thread) by the caller — this
        /// router only ever receives plain managed values, never a live Godot object, so the thread-safety
        /// invariant is preserved. Both are null/empty for the editor passive-log path and on Godot &lt; 4.5.
        /// </para>
        /// </summary>
        public void Route(EngineErrorKind kind, string? filePath, int line, string? message, string? rationale,
            string? function = null, IReadOnlyList<RuntimeErrorFrame>? frames = null, string? stackTrace = null)
        {
            // Build the human line: prefer the engine "rationale" (the actual error text) and fall back to
            // the C++ assertion "message" (the failed condition) when no rationale is present.
            var text = !string.IsNullOrEmpty(rationale) ? rationale!
                     : !string.IsNullOrEmpty(message) ? message!
                     : "(no message)";

            var logType = kind == EngineErrorKind.Warning ? GodotLogType.Warning : GodotLogType.Error;

            // Passive capture: surface a path:line prefix so console-get-logs is self-describing.
            LogSink?.Invoke(logType, FormatLogLine(kind, filePath, line, text));

            // Structured capture (in-game runtime): forward the full origin + deep backtrace so
            // runtime-errors-get can return file/line/function/type AND the multi-frame stack, not just a
            // flattened line. Always fires (errors are not session-gated). Wrapped for parity with the in-game
            // C# fault handlers: this runs on the engine's (multi-threaded) log callback, so a throwing sink
            // must never escape back into the engine.
            try { ErrorSink?.Invoke(new EngineErrorRecord(kind, filePath, line, function, text, frames, stackTrace)); }
            catch { /* a captured engine-error sink must never throw back into the engine callback */ }

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

        /// <summary>
        /// Pure-managed install/rebind coordinator shared by the engine-logger bridge (issue #171). Decides what
        /// the bridge does when asked to install <paramref name="incoming"/>, given whether a logger is already
        /// live (<paramref name="liveCapture"/> is the capture that logger currently forwards into, or null when
        /// nothing is registered). This logic is the load-bearing fix and is decoupled from the native
        /// <c>OS.AddLogger</c> path so it is unit-testable in the binary-less xUnit host: the real Godot 4.5+
        /// bridge calls this to make the SAME decision, then performs the native side effect the result names.
        /// <list type="bullet">
        /// <item><see cref="BridgeInstallAction.RegisterNew"/> — nothing was live; register a new logger.</item>
        /// <item><see cref="BridgeInstallAction.Rebind"/> — a logger is live on a DIFFERENT capture; repoint it
        /// at <paramref name="incoming"/> WITHOUT re-registering (no OS.RemoveLogger/AddLogger churn). This is
        /// the path that closes #171: it stops engine errors routing to a stale capture's collector.</item>
        /// <item><see cref="BridgeInstallAction.AlreadyCurrent"/> — a logger is live on the SAME capture; a
        /// benign idempotent re-assert (RuntimeErrorCapture.Install's re-entry path). No side effect needed.</item>
        /// </list>
        /// In all non-null-incoming cases <see cref="Current"/> ends up equal to <paramref name="incoming"/> so
        /// the published router never lags the live logger. Returns the action the caller must apply.
        /// </summary>
        public static BridgeInstallAction PlanBridgeInstall(ScriptErrorCapture? liveCapture, ScriptErrorCapture incoming)
        {
            if (incoming == null)
                throw new ArgumentNullException(nameof(incoming));

            BridgeInstallAction action;
            if (liveCapture == null)
                action = BridgeInstallAction.RegisterNew;
            else if (ReferenceEquals(liveCapture, incoming))
                action = BridgeInstallAction.AlreadyCurrent;
            else
                action = BridgeInstallAction.Rebind;

            // The published router must always track the capture the live logger ends up forwarding into — set it
            // here so neither the RegisterNew nor the Rebind caller can forget and leave Current pointing at a
            // stale capture (the very desync #171 is about).
            Current = incoming;
            return action;
        }
    }

    /// <summary>
    /// What the engine-logger bridge must do for a given install request — the outcome of
    /// <see cref="ScriptErrorCapture.PlanBridgeInstall"/>. Pure-managed so the decision is unit-testable apart
    /// from the native <c>OS.AddLogger</c> side effect each value names (issue #171).
    /// </summary>
    public enum BridgeInstallAction
    {
        /// <summary>No logger was live; register a new one via <c>OS.AddLogger</c>.</summary>
        RegisterNew = 0,
        /// <summary>A logger is live on a DIFFERENT capture; repoint it at the incoming one (no re-register).</summary>
        Rebind = 1,
        /// <summary>A logger is live on the SAME capture; benign idempotent re-assert — no side effect.</summary>
        AlreadyCurrent = 2,
    }
}
