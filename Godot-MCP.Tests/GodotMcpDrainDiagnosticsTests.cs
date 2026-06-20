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
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Regression tests for <see cref="GodotMcpDrainDiagnostics"/> — the pure-managed seam that decides whether
    /// <see cref="GodotMcpConnection.DisconnectAndDrain(TimeSpan)"/> emits a diagnostic warning when its bounded
    /// <c>WaitForImmediateTeardown</c> drain TIMES OUT (silently re-introducing godot#78513) vs COMPLETES.
    ///
    /// <para>
    /// The bool fed to <see cref="GodotMcpDrainDiagnostics.ReportDrainResult"/> is exactly what the reused
    /// client's <c>IConnection.WaitForImmediateTeardown(TimeSpan)</c> returns (verified against McpPlugin 6.10.0:
    /// <c>true</c> = drained within the bound / nothing pending / already disposed; <c>false</c> = the bounded
    /// <c>Task.Wait(timeout)</c> timed out or faulted). The editor/runtime-coupled <see cref="GodotMcpConnection"/>
    /// that calls into the live SignalR client is verified via the headless Godot smoke (test.md Suite 3); the
    /// timeout->warn / completion->no-warn DECISION, the single-warning guarantee, the never-throws contract, and
    /// the message shape are pinned here in the plain-xUnit host with a capturing sink (no Godot binary needed).
    /// </para>
    /// </summary>
    public class GodotMcpDrainDiagnosticsTests : IDisposable
    {
        readonly Action<string>? _savedWarn;

        public GodotMcpDrainDiagnosticsTests()
        {
            // Snapshot the production sink (wired by GodotMcpConnection's static ctor when the type is touched)
            // so each test can install a capturing sink and restore the original in Dispose — the seam's Warn is
            // process-wide static state.
            _savedWarn = GodotMcpDrainDiagnostics.Warn;
        }

        public void Dispose()
        {
            GodotMcpDrainDiagnostics.Warn = _savedWarn;
        }

        [Fact]
        public void Timeout_EmitsExactlyOneWarning_AndReportsTrue()
        {
            // THE TIMEOUT BRANCH: WaitForImmediateTeardown returned false (bounded wait did not drain) -> the
            // godot#78513 reintroduction must be surfaced as exactly ONE warning, and the call reports it warned.
            var captured = new List<string>();
            GodotMcpDrainDiagnostics.Warn = captured.Add;

            bool warned = GodotMcpDrainDiagnostics.ReportDrainResult(drained: false, timeout: TimeSpan.FromSeconds(2));

            Assert.True(warned);
            Assert.Single(captured);
            // The warning is informative and clearly a WARNING (proceed-anyway), referencing the godot#78513 root.
            Assert.Contains("godot#78513", captured[0]);
            Assert.Contains("did not drain", captured[0]);
        }

        [Fact]
        public void Completion_EmitsNoWarning_AndReportsFalse()
        {
            // THE COMPLETION BRANCH: WaitForImmediateTeardown returned true (drained within the bound, or nothing
            // pending / already disposed) -> NO warning, and the call reports it did not warn.
            var captured = new List<string>();
            GodotMcpDrainDiagnostics.Warn = captured.Add;

            bool warned = GodotMcpDrainDiagnostics.ReportDrainResult(drained: true, timeout: TimeSpan.FromSeconds(2));

            Assert.False(warned);
            Assert.Empty(captured);
        }

        [Fact]
        public void Timeout_WithNoSink_StillReportsTrue_AndDoesNotThrow()
        {
            // The sink is optional (null on a non-editor game build before any wiring). A timeout must still be
            // detected (reports true) and must NEVER throw — the seam runs on the ALC-unload teardown path.
            GodotMcpDrainDiagnostics.Warn = null;

            bool warned = false;
            var ex = Record.Exception(() =>
            {
                warned = GodotMcpDrainDiagnostics.ReportDrainResult(drained: false, timeout: TimeSpan.FromSeconds(1));
            });

            Assert.Null(ex);
            Assert.True(warned);
        }

        [Fact]
        public void Timeout_WithFaultingSink_DoesNotThrow()
        {
            // SAFETY CONTRACT: the warning runs inside the ALC-unloading teardown path, so a faulting sink (e.g.
            // GD unavailable mid editor-reload) must be swallowed — ReportDrainResult must never throw.
            GodotMcpDrainDiagnostics.Warn = _ => throw new InvalidOperationException("sink boom");

            var ex = Record.Exception(() =>
                GodotMcpDrainDiagnostics.ReportDrainResult(drained: false, timeout: TimeSpan.FromSeconds(1)));

            Assert.Null(ex);
        }

        [Fact]
        public void FormatTimeoutWarning_IncludesTheTimeout()
        {
            // The message names the bound that was applied (the timeout value is unchanged — observability only).
            string msg = GodotMcpDrainDiagnostics.FormatTimeoutWarning(TimeSpan.FromSeconds(3));

            Assert.Contains("00:00:03", msg);
            Assert.Contains("[Godot-MCP]", msg);
        }
    }
}
