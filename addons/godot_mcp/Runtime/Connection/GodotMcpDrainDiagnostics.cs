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

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Pure-managed diagnostic seam for the bounded teardown drain in
    /// <see cref="GodotMcpConnection.DisconnectAndDrain(TimeSpan)"/>.
    ///
    /// <para>
    /// <b>Why.</b> <c>DisconnectAndDrain</c> bounded-JOINs the dispatched SignalR teardown via the reused
    /// client's <c>IConnection.WaitForImmediateTeardown(TimeSpan)</c> — the connection root of the
    /// godot#78513 reload fix. That call returns a <see cref="bool"/>: <c>true</c> when the dispatched
    /// teardown actually DRAINED within the bound (or there was nothing pending / the client was already
    /// disposed — i.e. the transport's threads/handles are released before the collectible ALC unloads),
    /// and <c>false</c> when the bounded wait TIMED OUT or faulted (a black-holed host that never finished
    /// the dispatched teardown). On the <c>false</c> path the method still proceeds with <c>Dispose</c> + the
    /// ALC unload — which is the correct bounded behaviour — but doing so SILENTLY re-introduces the
    /// godot#78513 symptom (live transport threads/GC handles may briefly outlive the reload). This seam
    /// surfaces that timeout as exactly ONE warning so the silent reintroduction is at least visible in the
    /// editor log / runtime; the completion path emits nothing.
    /// </para>
    ///
    /// <para>
    /// <b>Seam discipline.</b> Pure-BCL (no Godot native types), so the timeout→warning DECISION is
    /// CI-unit-testable without a Godot binary (the editor-only <c>GodotMcpConnection</c> instantiates the
    /// SignalR client and is verified via the headless Godot smoke instead). The warning is routed through an
    /// injectable <see cref="Warn"/> sink that defaults to <c>null</c> (no-op); the production caller wires it
    /// to a defensively-wrapped <c>GD.PushWarning</c>. This mirrors the existing
    /// <see cref="GodotMcpAssemblyResolver.Log"/> / <see cref="GodotMcpStjReflectionCache.Log"/> static-sink
    /// pattern. <see cref="ReportDrainResult"/> NEVER throws (a faulting sink is swallowed), preserving the
    /// "never throw into the ALC-unloading handler" contract.
    /// </para>
    /// </summary>
    public static class GodotMcpDrainDiagnostics
    {
        /// <summary>
        /// Optional warning sink (e.g. a defensively-wrapped <c>GD.PushWarning</c>). Stays pure-BCL by taking
        /// a delegate; defaults to <c>null</c> (no-op) so unit tests can assert the timeout→warning DECISION
        /// via the <see cref="ReportDrainResult"/> return value alone, and can also inject a capturing sink to
        /// assert the message text. Never invoked on the completion path.
        /// </summary>
        public static Action<string>? Warn { get; set; }

        /// <summary>
        /// Build the single diagnostic message emitted when the bounded teardown wait times out. Pure function
        /// (exposed for the unit test to pin the message shape without coupling to the sink).
        /// </summary>
        public static string FormatTimeoutWarning(TimeSpan timeout) =>
            $"[Godot-MCP] connection teardown did not drain within {timeout} (host may be unreachable); " +
            "proceeding with unload anyway — transport threads may briefly outlive the reload (godot#78513).";

        /// <summary>
        /// Report the result of the bounded <c>WaitForImmediateTeardown</c> drain. When <paramref name="drained"/>
        /// is <c>false</c> (timed out / faulted), emit exactly ONE warning via <see cref="Warn"/> (if set) and
        /// return <c>true</c>. When <paramref name="drained"/> is <c>true</c> (completed / nothing to drain),
        /// emit nothing and return <c>false</c>. Never throws: a faulting sink is swallowed so this stays safe to
        /// call from the ALC-unloading teardown path.
        /// </summary>
        /// <param name="drained">
        /// The <see cref="bool"/> returned by <c>IConnection.WaitForImmediateTeardown(TimeSpan)</c>: <c>true</c>
        /// = drained within the bound (or nothing pending); <c>false</c> = the bounded wait timed out or faulted.
        /// </param>
        /// <param name="timeout">The bound that was applied (only used to format the warning text).</param>
        /// <returns><c>true</c> if a timeout warning was raised; <c>false</c> on the completion (no-warning) path.</returns>
        public static bool ReportDrainResult(bool drained, TimeSpan timeout)
        {
            if (drained)
                return false;

            try
            {
                Warn?.Invoke(FormatTimeoutWarning(timeout));
            }
            catch
            {
                // Diagnostics must never break the ALC-unload teardown path — swallow a faulting sink
                // (e.g. GD unavailable mid editor-reload), exactly like the GD.Push* call sites do.
            }

            return true;
        }
    }
}
