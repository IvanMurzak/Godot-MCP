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

            var capture = new ScriptErrorCapture
            {
                LogSink = (logType, message) => collector.Append(logType, message),
            };

            var logger = new GodotScriptErrorLogger(capture);
            OS.AddLogger(logger); // static API in GodotSharp 4.5

            ScriptErrorCapture.Current = capture;
            return capture;
        }
    }
}
#endif
