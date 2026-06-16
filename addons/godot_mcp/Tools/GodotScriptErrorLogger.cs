/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// GODOT 4.5+ ONLY. Godot.Logger / OS.AddLogger do NOT exist in the addon's SDK floor (Godot.NET.Sdk/4.3.0),
// so referencing them outside this guard would break the required `dotnet build (.NET 8)` CI gate (which
// pins 4.3.0) with CS0246. The Godot.NET.Sdk defines GODOT4_5_OR_GREATER only when building against the
// 4.5+ SDK (e.g. the infra testbed pins 4.5.1), so this whole file compiles in on 4.5+ and out on 4.3/4.4.
// On the floor, GodotScriptErrorLoggerBridge.TryInstall is a no-op stub (see the #else partial below) and
// script-validate falls back to the per-file Reload() error-code probe (Tool_Script.Validate.cs).
#if TOOLS && GODOT4_5_OR_GREATER
#nullable enable
using System;
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
    ///
    /// <para>
    /// Godot calls <see cref="_LogError"/> from the thread the error originated on, so all buffer writes
    /// happen inside <see cref="ScriptErrorCapture"/> under its lock. We do NOT touch any non-thread-safe
    /// Godot object here — only forward primitives — so the multi-threaded callback is safe.
    /// </para>
    /// </summary>
    public sealed partial class GodotScriptErrorLogger : Logger
    {
        readonly ScriptErrorCapture _capture;

        public GodotScriptErrorLogger(ScriptErrorCapture capture)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        }

        // NOTE: the engine's abstract _LogError binds 'errorType' as Int32 (not the Logger.ErrorType enum),
        // so the override MUST use 'int' to match — the enum values (Error=0/Warning=1/Script=2/Shader=3)
        // are mapped from the int below. The 'scriptBacktraces' parameter is accepted but unused (we forward
        // primitives only, keeping the multi-threaded callback free of non-thread-safe Godot object access).
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
            _capture.Route(
                kind: MapKind(errorType),
                filePath: file,
                line: line,
                // 'code' is the failed C++ condition string; 'rationale' is the human error text.
                message: code,
                rationale: rationale);
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
        static GodotScriptErrorLogger? _installed;

        /// <summary>
        /// Install the engine-error logger and return the router it feeds. The router's <see cref="ScriptErrorCapture.LogSink"/>
        /// is wired to <paramref name="collector"/> so passive engine errors land in <c>console-get-logs</c>.
        /// Returns null only if <paramref name="collector"/> is null (nothing to wire) — callers treat null as
        /// "unavailable". Idempotent-friendly: the caller installs once at boot.
        /// </summary>
        public static ScriptErrorCapture? TryInstall(GodotLogCollector collector)
        {
            if (collector == null)
                return null;

            // Symmetric with Uninstall: tear down any prior registration FIRST. In the normal paired
            // _EnterTree/_ExitTree lifecycle _installed is already null here, but a stray double-install
            // (two _EnterTree without an intervening Teardown) would otherwise overwrite _installed without
            // OS.RemoveLogger/Free()-ing the previous logger — leaking it registered in the engine, which
            // re-pins the collectible ALC (the exact godot#78513 unload failure this bridge removes) AND
            // orphans it beyond Uninstall's reach. Uninstall is idempotent, so this is a safe no-op when
            // nothing is installed.
            Uninstall();

            var capture = new ScriptErrorCapture
            {
                LogSink = (logType, message) => collector.Append(logType, message),
            };

            var logger = new GodotScriptErrorLogger(capture);
            OS.AddLogger(logger); // static API in GodotSharp 4.5

            _installed = logger; // retain so Uninstall() can remove the exact same instance
            ScriptErrorCapture.Current = capture;
            return capture;
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
