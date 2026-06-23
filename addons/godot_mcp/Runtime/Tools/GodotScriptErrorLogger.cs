/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// GODOT 4.5+ ONLY (NOT editor-only). Godot.Logger / OS.AddLogger do NOT exist in the addon's SDK floor
// (Godot.NET.Sdk/4.3.0), so referencing them outside this guard would break the required
// `dotnet build (.NET 8)` CI gate (which pins 4.3.0) with CS0246. The Godot.NET.Sdk defines
// GODOT4_5_OR_GREATER only when building against the 4.5+ SDK (e.g. the infra testbed pins 4.5.1), so this
// whole file compiles in on 4.5+ and out on 4.3/4.4. On the floor, GodotScriptErrorLoggerBridge.TryInstall
// is a no-op stub (see GodotScriptErrorLogger.Stub.cs) and script-validate falls back to the per-file
// Reload() error-code probe (Tool_Script.Validate.cs).
//
// This file lives under Runtime/ and is NOT gated by #if TOOLS: OS.AddLogger / Godot.Logger are ENGINE
// (runtime) APIs, not editor APIs — available inside a running / exported game as well as the editor. Two
// consumers install the SAME bridge:
//   * the editor boot (Editor/GodotMcpPlugin.cs) captures engine script errors raised IN-EDITOR, and
//   * the in-game runtime (Runtime/RuntimeErrorCapture.cs) captures engine errors raised in the RUNNING
//     GAME (GDScript runtime errors, push_error/push_warning, shader errors) — closing the gap where in-game
//     runtime errors were invisible to the agent (issue #160).
// The runtime/editor boundary guard (scripts/check-runtime-boundary.py) passes this file because it
// references no editor-only API (EditorInterface / EditorPlugin / EditorFileSystem / EditorScript) — only
// the engine OS singleton + Godot.Logger.
#if GODOT4_5_OR_GREATER
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using com.IvanMurzak.Godot.MCP.Data;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Godot 4.5+ <see cref="Logger"/> that taps the engine's global error stream and forwards every
    /// error/warning to the pure-managed <see cref="ScriptErrorCapture"/> router. This is the single
    /// registration (via <see cref="OS.AddLogger"/>) that powers BOTH feature deliverables: passive
    /// engine-log capture into <c>console-get-logs</c> AND on-demand <c>script-validate</c> diagnostics
    /// (the router collects <see cref="EngineErrorKind.Script"/> rows while a validation session is open).
    /// It is ALSO the source of the deep multi-frame GDScript backtrace surfaced by <c>runtime-errors-get</c>
    /// (issue #163): the engine hands a <see cref="ScriptBacktrace"/> array to <see cref="_LogError"/>, which
    /// this class materializes into managed primitives before forwarding.
    ///
    /// <para>
    /// Godot calls <see cref="_LogError"/> from the thread the error originated on, so all buffer writes
    /// happen inside <see cref="ScriptErrorCapture"/> under its lock. <see cref="ScriptBacktrace"/> is a
    /// NON-thread-safe Godot object; we read it ONLY here, on the originating thread, copying each frame's
    /// function/file/line into plain <see cref="RuntimeErrorFrame"/> primitives (and the engine's formatted
    /// string) BEFORE handing the record to <see cref="ScriptErrorCapture.Route"/>. No live Godot object is
    /// ever stored or forwarded across the thread boundary — that invariant is what keeps the multi-threaded
    /// callback safe.
    /// </para>
    /// </summary>
    public sealed partial class GodotScriptErrorLogger : Logger
    {
        // The router the live logger forwards every engine callback into. NOT readonly: a re-entrant
        // install (issue #171) must be able to REBIND this to a fresh capture without removing+re-adding the
        // engine Logger (OS.RemoveLogger/AddLogger), so the single registered logger always feeds whichever
        // ScriptErrorCapture is current. Volatile so the rebind written on the main (install) thread is visible
        // to the engine's multi-threaded _LogError callback without a lock on the hot error path.
        // Nullable because the parameterless ctor (hot-reload re-instantiation, below) leaves it unset — that
        // orphan never gets registered with the engine, so it is never routed into; _LogError null-guards it.
        volatile ScriptErrorCapture? _capture;

        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live engine-registered <see cref="Logger"/> via its parameterless ctor,
        /// so a parameter-only class throws <c>MissingMemberException</c> and breaks the reload. Our teardown
        /// removed the REAL logger via <c>OS.RemoveLogger</c> before the reload, and the reloaded plugin
        /// re-installs a fresh one, so this re-instantiated shell is a never-routed-into orphan — <see cref="_capture"/>
        /// stays null and <see cref="_LogError"/> null-guards it.
        /// </summary>
        public GodotScriptErrorLogger() { }

        public GodotScriptErrorLogger(ScriptErrorCapture capture)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        }

        /// <summary>
        /// The router this live logger currently forwards into. Exposed so the bridge can detect a
        /// sink-identity mismatch on a re-entrant install (issue #171) and rebind rather than silently keep
        /// routing engine errors to a stale capture (whose sink feeds a collector the current handle no longer
        /// reads — the #160 silent-quiet failure).
        /// </summary>
        public ScriptErrorCapture? Capture => _capture;

        /// <summary>
        /// Repoint this already-registered logger at a NEW <paramref name="capture"/> WITHOUT touching
        /// <see cref="OS.AddLogger"/>/<see cref="OS.RemoveLogger"/>. Used by the bridge's re-entrant install
        /// path (issue #171): an independent engine-logger teardown + a re-install would otherwise leave the
        /// single live logger forwarding to the FIRST capture's sink (routing errors to an orphaned collector),
        /// because the engine still holds the original Logger instance. Rebinding the field keeps the single
        /// registration but makes the new capture authoritative. The write is volatile, so the engine's
        /// off-thread <see cref="_LogError"/> sees the new router immediately.
        /// </summary>
        public void RebindCapture(ScriptErrorCapture capture)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        }

        // NOTE: the engine's abstract _LogError binds 'errorType' as Int32 (not the Logger.ErrorType enum),
        // so the override MUST use 'int' to match — the enum values (Error=0/Warning=1/Script=2/Shader=3)
        // are mapped from the int below.
        public override void _LogError(
            string function,
            string file,
            int line,
            string code,
            string rationale,
            bool editorNotify,
            int errorType,
            // Fully-qualified with global:: — the enclosing 'com.IvanMurzak.Godot.MCP.Tools' namespace would
            // otherwise bind 'Godot.Collections' to 'com.IvanMurzak.Godot.Collections' (CS0234).
            global::Godot.Collections.Array<global::Godot.ScriptBacktrace> scriptBacktraces)
        {
            // Null-guard the router: a hot-reload-re-instantiated orphan (parameterless ctor) has no capture
            // and is never the registered logger, but guard defensively so a stray engine callback is a no-op
            // rather than a NullReferenceException escaping back into the engine's log path.
            var capture = _capture;
            if (capture == null)
                return;

            // Materialize the deep multi-frame backtrace NOW, on the originating thread, while the
            // non-thread-safe ScriptBacktrace objects are still valid in this callback's frame. The result is
            // pure managed primitives — safe to forward across the thread boundary into the collector. Any
            // fault while reading the engine objects is swallowed to a null backtrace (origin-only) rather
            // than escaping back into the engine's log path.
            IReadOnlyList<RuntimeErrorFrame>? frames = null;
            string? stackTrace = null;
            try
            {
                MaterializeBacktrace(scriptBacktraces, out frames, out stackTrace);
            }
            catch
            {
                // A backtrace read failure must never abort error capture — degrade to origin-only.
                frames = null;
                stackTrace = null;
            }

            capture.Route(
                kind: MapKind(errorType),
                filePath: file,
                line: line,
                // 'code' is the failed C++ condition string; 'rationale' is the human error text.
                message: code,
                rationale: rationale,
                // 'function' is the originating function name — forwarded so the in-game runtime's structured
                // ErrorSink can populate RuntimeError.Function (the editor passive-log path ignores it).
                function: function,
                // Deep GDScript backtrace (issue #163), already copied to managed primitives above.
                frames: frames,
                stackTrace: stackTrace);
        }

        // ── Backtrace materialization (originating-thread only) ──────────────────────────────────────────
        //
        // CRITICAL THREAD-SAFETY INVARIANT: every ScriptBacktrace access below happens synchronously inside
        // _LogError, on the thread the error originated on. ScriptBacktrace is a RefCounted Godot object and
        // is NOT thread-safe; we never store or return one — only the plain managed primitives we copy out of
        // it. The out-params are the ONLY things that cross back to Route() / the collector / another thread.

        /// <summary>
        /// Copy the engine's per-language <see cref="ScriptBacktrace"/> array into managed primitives:
        /// <paramref name="frames"/> (innermost-first, function/file/line per frame) and
        /// <paramref name="stackTrace"/> (the engine's <see cref="ScriptBacktrace.Format()"/> string). Picks
        /// the FIRST non-empty backtrace in the array — Godot orders them by language and the script language
        /// that raised the error has the populated stack (GDScript at runtime); a non-empty pick yields the
        /// real call stack rather than an empty sibling-language entry. Both out-params are null when no
        /// non-empty backtrace exists (release builds without call-stack tracking, or a pure-engine error),
        /// preserving the origin-only fallback.
        /// </summary>
        static void MaterializeBacktrace(
            global::Godot.Collections.Array<global::Godot.ScriptBacktrace>? scriptBacktraces,
            out IReadOnlyList<RuntimeErrorFrame>? frames,
            out string? stackTrace)
        {
            frames = null;
            stackTrace = null;

            if (scriptBacktraces == null || scriptBacktraces.Count == 0)
                return;

            foreach (var backtrace in scriptBacktraces)
            {
                if (backtrace == null || backtrace.IsEmpty())
                    continue;

                var frameCount = backtrace.GetFrameCount();
                if (frameCount <= 0)
                    continue;

                var list = new List<RuntimeErrorFrame>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    // Normalize the engine line to RuntimeErrorFrame's "-1 == unknown" sentinel: Godot reports
                    // 0 (not -1) for a frame with no usable line info (e.g. a function-signature frame, or a
                    // release export without call-stack line tracking — godotengine/godot#106484). Left raw, a
                    // 0-line frame would render "res://x.gd:0" and serialize line:0, contradicting the contract
                    // that ToString()/serialization treat <0 as "no line".
                    var frameLine = backtrace.GetFrameLine(i);
                    list.Add(new RuntimeErrorFrame(
                        function: backtrace.GetFrameFunction(i),
                        file: backtrace.GetFrameFile(i),
                        line: frameLine > 0 ? frameLine : -1));
                }

                if (list.Count == 0)
                    continue;

                frames = list;
                // The engine's own formatted rendering is the most faithful human-readable string; fall back
                // to a managed join only if Format() yields nothing (shouldn't, for a non-empty backtrace).
                stackTrace = BuildStackTraceString(backtrace, list);
                return;
            }
        }

        /// <summary>
        /// Render a human-readable stack-trace string for a captured backtrace. Prefers the engine's
        /// <see cref="ScriptBacktrace.Format()"/>, which is ALREADY self-heading — it emits its own
        /// "&lt;language&gt; backtrace (most recent call first):" header — so we return it verbatim and do NOT
        /// add a second language prefix (which would double-head the output). Only the managed-join fallback
        /// (used when Format() returns empty) gets a "&lt;language&gt; backtrace:" prefix, since the joined
        /// frames carry no header of their own.
        /// </summary>
        static string BuildStackTraceString(global::Godot.ScriptBacktrace backtrace, List<RuntimeErrorFrame> frames)
        {
            string? formatted = null;
            try { formatted = backtrace.Format(); } catch { /* fall back to the managed join below */ }

            // Format() already prepends its own "<language> backtrace (...)" header — return it as-is.
            if (!string.IsNullOrWhiteSpace(formatted))
                return formatted!;

            // Managed-join fallback: the joined frames have no header, so synthesize one from the language.
            var sb = new StringBuilder();
            foreach (var frame in frames)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(frame.ToString());
            }
            var body = sb.ToString();

            string? language = null;
            try { language = backtrace.GetLanguageName(); } catch { /* best-effort label */ }

            return string.IsNullOrEmpty(language)
                ? body
                : $"{language} backtrace:\n{body}";
        }

        // We intentionally do NOT override _LogMessage: ordinary print()/stdout traffic is already covered by
        // the plugin's own GD.* capture path (GodotMcpPlugin.Log*). Tapping it here would double-capture and
        // flood the ring buffer. The error stream (_LogError) is the gap this feature closes.

        static EngineErrorKind MapKind(int errorType) => errorType switch
        {
            (int)Logger.ErrorType.Error => EngineErrorKind.Error,
            (int)Logger.ErrorType.Warning => EngineErrorKind.Warning,
            (int)Logger.ErrorType.Script => EngineErrorKind.Script,
            (int)Logger.ErrorType.Shader => EngineErrorKind.Shader,
            _ => EngineErrorKind.Error,
        };
    }

    /// <summary>
    /// 4.5+ implementation of the version-agnostic install bridge: constructs the <see cref="Logger"/>,
    /// registers it via <see cref="OS.AddLogger"/>, and wires the router's passive log sink to the supplied
    /// collector. Returns the live capture so the tool layer can drive validation sessions. Main-thread only.
    /// </summary>
    public static class GodotScriptErrorLoggerBridge
    {
        // The Logger we registered with OS.AddLogger, retained so Uninstall can hand the SAME instance to
        // OS.RemoveLogger on teardown. The engine holds a strong native+managed handle to this object; that
        // handle is what roots the collectible AssemblyLoadContext (the logger type is DEFINED in the
        // collectible addon assembly), so leaving it registered makes the hot-reload ALC unload fail with the
        // godotengine/godot#78513 ".NET: Failed to unload assemblies" flood. Static field => one registration
        // per loaded assembly, which matches the single boot-time TryInstall.
        //
        // Separate-process safety: the editor (Editor/GodotMcpPlugin.cs) and the in-game runtime
        // (Runtime/RuntimeErrorCapture.cs) BOTH register through this bridge, but Godot launches the running
        // game (F5 / "Play") as a SEPARATE OS PROCESS — the editor and the game never share an address space,
        // so each process owns its own copy of this static and one cannot tear down or clobber the other's
        // registration. The non-destructive guard in TryInstall below additionally protects against a stray
        // in-process double-install (e.g. a re-init) so it never silently leaks the prior logger.
        static GodotScriptErrorLogger? _installed;

        /// <summary>
        /// Install the engine-error logger and return the router it feeds. The router's <see cref="ScriptErrorCapture.LogSink"/>
        /// is wired to <paramref name="collector"/> so passive engine errors land in <c>console-get-logs</c>.
        /// Returns null only if <paramref name="collector"/> is null (nothing to wire) — callers treat null as
        /// "unavailable". Idempotent-friendly: the caller installs once at boot. This is the EDITOR path
        /// (Editor/GodotMcpPlugin.cs); the in-game runtime uses the <see cref="TryInstall(ScriptErrorCapture)"/>
        /// overload to wire a structured <see cref="ScriptErrorCapture.ErrorSink"/> instead.
        /// </summary>
        public static ScriptErrorCapture? TryInstall(GodotLogCollector collector)
        {
            if (collector == null)
                return null;

            var capture = new ScriptErrorCapture
            {
                LogSink = (logType, message) => collector.Append(logType, message),
            };

            return TryInstall(capture);
        }

        /// <summary>
        /// Install the engine-error logger feeding a caller-supplied <paramref name="capture"/> router and
        /// publish it as <see cref="ScriptErrorCapture.Current"/>. The IN-GAME RUNTIME path
        /// (Runtime/RuntimeErrorCapture.cs): the caller pre-wires <paramref name="capture"/>'s
        /// <see cref="ScriptErrorCapture.ErrorSink"/> so engine errors raised in the running game are captured
        /// as structured <see cref="Data.RuntimeError"/> rows. Returns the same <paramref name="capture"/> on
        /// success, or null if it was null. Main-thread only (mirrors <see cref="OS.AddLogger"/>).
        /// </summary>
        public static ScriptErrorCapture? TryInstall(ScriptErrorCapture capture)
        {
            if (capture == null)
                return null;

            // Decide RegisterNew / Rebind / AlreadyCurrent via the pure-managed coordinator so this native path
            // and the unit tests share ONE decision (issue #171). PlanBridgeInstall also republishes
            // ScriptErrorCapture.Current = capture, so the published router never lags the live logger.
            var action = ScriptErrorCapture.PlanBridgeInstall(_installed?.Capture, capture);
            switch (action)
            {
                case BridgeInstallAction.AlreadyCurrent:
                    // Benign idempotent re-assert (RuntimeErrorCapture.Install's re-entry with the same capture).
                    return capture;

                case BridgeInstallAction.Rebind:
                    // ALREADY installed on a DIFFERENT capture — issue #171. REBIND the single live logger to the
                    // new capture rather than (a) OS.RemoveLogger/AddLogger churn (which re-pins the collectible
                    // ALC — the godot#78513 unload failure this bridge removes) or (b) silently keeping the OLD
                    // capture (the bug: the live logger would keep forwarding to the FIRST capture's sink, so
                    // engine errors land in a collector the current handle no longer reads → runtime-errors-get
                    // falls silently quiet, the #160 failure this feature prevents). Surface the rebind so a
                    // stale-sink regression is never silent again.
                    GD.PushWarning(
                        "[Godot-MCP] engine error-logger re-installed with a new capture; rebinding the live " +
                        "logger to it so runtime errors route to the current collector (issue #171).");
                    _installed!.RebindCapture(capture);
                    return capture;

                case BridgeInstallAction.RegisterNew:
                default:
                    var logger = new GodotScriptErrorLogger(capture);
                    OS.AddLogger(logger); // static API in GodotSharp 4.5

                    _installed = logger; // retain so Uninstall() can remove the exact same instance
                    return capture;
            }
        }

        /// <summary>
        /// Reverse <see cref="TryInstall"/>: remove the registered <see cref="Logger"/> from the engine via
        /// <see cref="OS.RemoveLogger(Logger)"/> (a public static API in GodotSharp 4.5+ — see
        /// <c>GodotSharp.xml</c> "Remove a custom logger added by OS.AddLogger"), then free the logger's native
        /// counterpart and clear the router. This drops the engine's strong handle to a GodotObject defined in
        /// the collectible addon assembly, which is what lets the hot-reload ALC actually unload (closing the
        /// godotengine/godot#78513 ".NET: Failed to unload assemblies" flood on Alt+B rebuild).
        ///
        /// <para>
        /// MAIN-THREAD ONLY: <see cref="OS.RemoveLogger"/> is an engine call and mirrors the
        /// <see cref="OS.AddLogger"/> in <c>GodotMcpPlugin._EnterTree</c>; the caller must invoke this from the
        /// editor main thread (the #78513 ALC-unload path always is). Idempotent (safe to call when nothing is
        /// installed, and safe to call twice) and defensive (a removal failure is swallowed — teardown must not
        /// crash).
        /// </para>
        /// </summary>
        public static void Uninstall()
        {
            var logger = _installed;
            _installed = null;

            if (logger != null)
            {
                // RemoveLogger drops the engine's reference; Dispose() then deterministically releases the
                // managed↔native binding. Both are best-effort — a throw here must not abort the rest of
                // teardown (the connection/dock are already being torn down).
                try
                {
                    OS.RemoveLogger(logger); // static API in GodotSharp 4.5+
                }
                catch (Exception)
                {
                    // Swallow: removal failing must not crash teardown. Worst case the logger stays registered
                    // and the #78513 flood persists — no worse than the pre-fix behavior.
                }

                try
                {
                    // Godot.Logger is RefCounted-derived, so it has NO `free()` builtin — calling Free() throws
                    // "Invalid call. Nonexistent function 'free'". Dispose() is the correct, valid release: it
                    // breaks the native object's strong GCHandle back to this managed wrapper SYNCHRONOUSLY here.
                    // That matters — the wrapper type lives in the collectible addon ALC, so leaving the cycle
                    // for the finalizer is exactly the timing fragility that makes the engine give up on the ALC
                    // unload (#78513). Dispose() is idempotent, so a double-call / already-disposed logger is safe.
                    logger.Dispose();
                }
                catch (Exception)
                {
                    // Swallow: a disposed/already-freed logger throwing here is benign during unload.
                }
            }

            // Always clear the router so a stale capture/validation session never leaks across a reload, even if
            // nothing was installed (e.g. collector was null at boot). Pure-managed — cannot fault on the engine.
            try { ScriptErrorCapture.Current = null; } catch { /* swallow during unload */ }
        }
    }
}
#endif
