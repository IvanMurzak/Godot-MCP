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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The single capture point for the plugin's own diagnostic lines. Every <c>GD.Print</c> /
    /// <c>GD.PushWarning</c> / <c>GD.PushError</c> the addon emits routes through here so the line is BOTH
    /// (1) written to the Godot Output by severity AND (2) appended to
    /// <see cref="GodotLogCollector.Current"/>, making it retrievable via the <c>console-get-logs</c> tool.
    /// Before this helper, those ad-hoc <c>GD.Push*</c>/<c>GD.Print</c> call sites (connect/disconnect,
    /// drain-timeout, config save/load, skill-gen, dev-control, dispatcher, runtime-capture, the #171 rebind
    /// warning, …) were GD-only and therefore invisible to <c>console-get-logs</c> — this routing is the
    /// intentional behavior change of PR-2.
    ///
    /// <para>
    /// Lives OUTSIDE <c>#if TOOLS</c> (it ships into an exported game build where
    /// <see cref="Runtime.GodotMcpRuntime"/> / <c>RuntimeErrorCapture</c> /
    /// <see cref="MainThreadDispatch.MainThreadDispatcher"/> use it) and touches only the Godot runtime
    /// <see cref="GD"/> API — exactly the surface <see cref="GodotMcpConnection"/> already depends on. Not
    /// unit-tested (the <see cref="GD"/> sink faults in the binary-less xUnit host, like
    /// <c>GodotMcpConnection.RouteFrameworkLog</c> before it); its severity→sink decision is trivial and
    /// covered by the Suite-3 headless smoke.
    /// </para>
    ///
    /// <para>
    /// <see cref="Emit"/>'s internal try/catch is what lets every call site DROP its own defensive
    /// <c>try { GD.Push* } catch { }</c> wrapper: a diagnostic must never throw (GD may be unavailable mid
    /// editor-reload, and a collector append must never escape into the ALC-unloading teardown path). This
    /// subsumes the former <c>GodotMcpPlugin.TryLog</c>/<c>TryLogTeardownError</c>,
    /// <c>GodotMcpConnection.PushWarningSafe</c>, and the per-call drain-path try/catch blocks.
    /// </para>
    /// </summary>
    internal static class GodotMcpLog
    {
        /// <summary>Info-level diagnostic: <c>GD.Print</c> + collector append (never throws).</summary>
        internal static void Info(string message)
            => Emit(GodotLogType.Log, message, () => GD.Print(message));

        /// <summary>Warning-level diagnostic: <c>GD.PushWarning</c> + collector append (never throws).</summary>
        internal static void Warning(string message)
            => Emit(GodotLogType.Warning, message, () => GD.PushWarning(message));

        /// <summary>Error-level diagnostic: <c>GD.PushError</c> + collector append (never throws).</summary>
        internal static void Error(string message)
            => Emit(GodotLogType.Error, message, () => GD.PushError(message));

        /// <summary>
        /// Route an already-leveled framework line to the matching <see cref="GD"/> sink by severity AND the
        /// collector, preserving the incoming <paramref name="logType"/> on the collector append (so Debug /
        /// Trace are not flattened to Log). Replaces the former <c>GodotMcpConnection.RouteFrameworkLog</c>
        /// body, so framework logs and ad-hoc diagnostics share this one path.
        /// </summary>
        internal static void Route(GodotLogType logType, string message)
        {
            switch (logType)
            {
                case GodotLogType.Error:
                    Emit(logType, message, () => GD.PushError(message));
                    break;
                case GodotLogType.Warning:
                    Emit(logType, message, () => GD.PushWarning(message));
                    break;
                default:
                    Emit(logType, message, () => GD.Print(message));
                    break;
            }
        }

        /// <summary>
        /// The one sanctioned native sink: invoke the <see cref="GD"/> writer, then append to the collector.
        /// BOTH steps are independently swallowed — a diagnostic must NEVER throw (GD may be unavailable mid
        /// editor-reload, and the collector append must never escape into a teardown / unload path). This
        /// internal try/catch is what lets every call site drop its own defensive wrapper.
        /// </summary>
        static void Emit(GodotLogType logType, string message, Action gd)
        {
            try { gd(); }
            catch { /* GD may be unavailable mid editor-reload; swallow */ }

            try { GodotLogCollector.Current?.Append(logType, message); }
            catch { /* a diagnostic must never throw */ }
        }
    }
}
