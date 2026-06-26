/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Direct coverage for <see cref="ErrorCaptureManager"/> — the instance-based lifecycle coordinator that owns
    /// the engine-logger channel install/rebind/teardown and the statics-clearing policy for the error/log
    /// capture layer (PR-1, channel-unify). The manager's native touches (<c>OS.AddLogger</c>/<c>OS.RemoveLogger</c>
    /// via the bridge, <c>GD.Print</c>) are all behind delegate seams, so these tests drive it with PURE-MANAGED
    /// recording lambdas and assert install/teardown WITHOUT any Godot binary.
    ///
    /// <para>
    /// The manager's RUNTIME profile is ALSO exercised end-to-end by <see cref="RuntimeErrorCaptureRebindTests"/>
    /// (which now delegates through the manager) — those pin the #171 rebind decision via the
    /// <c>FakeEngineLoggerBridge</c>. This file pins what is unique to the manager facade: that the EDITOR profile
    /// wires a passive <see cref="ScriptErrorCapture.LogSink"/>, that the RUNTIME profile wires a structured
    /// <see cref="ScriptErrorCapture.ErrorSink"/> + publishes <see cref="RuntimeErrorCollector.Current"/>, that
    /// <see cref="ErrorCaptureManager.Teardown"/> is the single statics-clearing owner (nulls
    /// <see cref="ScriptErrorCapture.Current"/> via the bridge + <see cref="RuntimeErrorCollector.Current"/> for the
    /// runtime profile), and — critically — that it NEVER nulls <see cref="GodotLogCollector.Current"/> (issue #173).
    /// </para>
    ///
    /// <para>
    /// These mutate process-wide statics (<see cref="ScriptErrorCapture.Current"/>,
    /// <see cref="RuntimeErrorCollector.Current"/>, <see cref="GodotLogCollector.Current"/>), so this class joins the
    /// shared serial <see cref="RuntimeErrorCaptureSerialCollection"/> and every test snapshots + restores those in
    /// a finally.
    /// </para>
    /// </summary>
    [Collection(RuntimeErrorCaptureSerialCollection.Name)]
    public class ErrorCaptureManagerTests
    {
        // ── Editor profile ────────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InstallEditor_WiresPassiveLogSink_ToCollector_AndHandsCaptureToBridge()
        {
            WithCleanStatics(() =>
            {
                var collector = new GodotLogCollector();
                ScriptErrorCapture? handed = null;

                var manager = ErrorCaptureManager.InstallEditor(
                    collector,
                    bridgeInstall: c => { handed = c; return c; },
                    bridgeUninstall: () => { },
                    log: _ => { });

                Assert.True(manager.IsInstalled);
                Assert.NotNull(handed);
                // The editor profile wires a passive LogSink (the console-get-logs path), NOT a structured ErrorSink.
                Assert.NotNull(handed!.LogSink);
                Assert.Null(handed.ErrorSink);

                // Routing an engine error through the handed capture lands a flattened line in the collector.
                Assert.Equal(0, collector.Count);
                handed.Route(EngineErrorKind.Error, "res://x.gd", 12, message: null, rationale: "boom");
                Assert.Equal(1, collector.Count);
            });
        }

        [Fact]
        public void InstallEditor_Teardown_NullsScriptCaptureCurrent_ViaBridge_AndLeavesLogCollector()
        {
            WithCleanStatics(() =>
            {
                var collector = new GodotLogCollector();
                GodotLogCollector.Current = collector;          // the editor publishes the live buffer at boot
                ScriptErrorCapture.Current = new ScriptErrorCapture();
                RuntimeErrorCollector.Current = new RuntimeErrorCollector();
                var bridgeUninstallCalls = 0;

                var manager = ErrorCaptureManager.InstallEditor(
                    collector,
                    bridgeInstall: c => c,
                    // Mirror GodotScriptErrorLoggerBridge.Uninstall: removes the logger + nulls Current.
                    bridgeUninstall: () => { bridgeUninstallCalls++; ScriptErrorCapture.Current = null; },
                    log: _ => { });

                var runtimeBefore = RuntimeErrorCollector.Current;
                manager.Teardown();

                Assert.False(manager.IsInstalled);
                Assert.Equal(1, bridgeUninstallCalls);
                Assert.Null(ScriptErrorCapture.Current);                 // statics-clearing policy: bridge nulled it
                Assert.Same(collector, GodotLogCollector.Current);       // #173: log buffer left readable
                Assert.Same(runtimeBefore, RuntimeErrorCollector.Current); // editor teardown never touches it
            });
        }

        // ── Runtime profile ───────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InstallRuntime_PublishesCollector_AndWiresStructuredErrorSink()
        {
            WithCleanStatics(() =>
            {
                ScriptErrorCapture? handed = null;

                var manager = ErrorCaptureManager.InstallRuntime(
                    bridgeInstall: c => { handed = c; return c; },
                    bridgeUninstall: () => { },
                    log: _ => { });

                Assert.True(manager.IsInstalled);
                Assert.NotNull(manager.RuntimeCollector);
                Assert.Same(manager.RuntimeCollector, RuntimeErrorCollector.Current); // published

                // The runtime profile wires a structured ErrorSink (runtime-errors-get path), NOT a LogSink.
                Assert.NotNull(handed);
                Assert.NotNull(handed!.ErrorSink);
                Assert.Null(handed.LogSink);

                // A routed engine error lands as a structured row in the published collector.
                handed.Route(EngineErrorKind.Script, "res://p.gd", 5, message: null, rationale: "bad index",
                    function: "_process");
                Assert.Equal(1, manager.RuntimeCollector!.Count);
                var row = manager.RuntimeCollector.QuerySince(0, 10, out _).Single();
                Assert.Equal(RuntimeErrorSource.Engine, row.Source);
                Assert.Equal("bad index", row.Message);
                Assert.Equal("_process", row.Function);
            });
        }

        [Fact]
        public void InstallRuntime_FreshCapturePerInstall_BridgeReceivesDistinctRouters()
        {
            // The per-install capture freshness is load-bearing for the #171 rebind (a re-entrant install must
            // resolve to Rebind, which it can only do if a NEW capture instance is handed in). Two installs ⇒ two
            // distinct captures.
            WithCleanStatics(() =>
            {
                var handed = new List<ScriptErrorCapture>();

                var m1 = ErrorCaptureManager.InstallRuntime(
                    bridgeInstall: c => { handed.Add(c); return c; }, bridgeUninstall: () => { }, log: _ => { });
                m1.Teardown();
                var m2 = ErrorCaptureManager.InstallRuntime(
                    bridgeInstall: c => { handed.Add(c); return c; }, bridgeUninstall: () => { }, log: _ => { });
                m2.Teardown();

                Assert.Equal(2, handed.Count);
                Assert.NotSame(handed[0], handed[1]);
            });
        }

        [Fact]
        public void InstallRuntime_Teardown_NullsRuntimeCollectorCurrent_AndCallsBridge_AndLeavesLogCollector()
        {
            WithCleanStatics(() =>
            {
                var logBuffer = new GodotLogCollector();
                GodotLogCollector.Current = logBuffer;
                var bridgeUninstallCalls = 0;

                var manager = ErrorCaptureManager.InstallRuntime(
                    bridgeInstall: c => c,
                    bridgeUninstall: () => bridgeUninstallCalls++,
                    log: _ => { });

                Assert.NotNull(RuntimeErrorCollector.Current);
                manager.Teardown();

                Assert.False(manager.IsInstalled);
                Assert.Equal(1, bridgeUninstallCalls);
                Assert.Null(RuntimeErrorCollector.Current);          // statics-clearing policy: runtime collector cleared
                Assert.Same(logBuffer, GodotLogCollector.Current);   // #173: log buffer left readable
            });
        }

        // ── Idempotency + #173 ────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Teardown_IsIdempotent_SecondCallIsNoOp_AndDoesNotThrow()
        {
            WithCleanStatics(() =>
            {
                var bridgeUninstallCalls = 0;
                var manager = ErrorCaptureManager.InstallRuntime(
                    bridgeInstall: c => c, bridgeUninstall: () => bridgeUninstallCalls++, log: _ => { });

                manager.Teardown();
                var ex = Record.Exception(() => manager.Teardown()); // second call

                Assert.Null(ex);
                Assert.Equal(1, bridgeUninstallCalls); // the bridge teardown ran exactly once
                Assert.False(manager.IsInstalled);
            });
        }

        [Fact]
        public void Teardown_NeverNullsGodotLogCollectorCurrent_Issue173_BothProfiles()
        {
            WithCleanStatics(() =>
            {
                // Editor profile.
                var editorBuffer = new GodotLogCollector();
                GodotLogCollector.Current = editorBuffer;
                ErrorCaptureManager.InstallEditor(editorBuffer, c => c, () => { }, _ => { }).Teardown();
                Assert.Same(editorBuffer, GodotLogCollector.Current);

                // Runtime profile (independent buffer).
                var runtimeSessionBuffer = new GodotLogCollector();
                GodotLogCollector.Current = runtimeSessionBuffer;
                ErrorCaptureManager.InstallRuntime(c => c, () => { }, _ => { }).Teardown();
                Assert.Same(runtimeSessionBuffer, GodotLogCollector.Current);
            });
        }

        // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Snapshot every process-wide static this suite touches and restore it in a finally, so a failed
        /// assertion can never leak install state into a sibling test in the shared serial collection.</summary>
        static void WithCleanStatics(Action body)
        {
            var priorScript = ScriptErrorCapture.Current;
            var priorRuntime = RuntimeErrorCollector.Current;
            var priorLog = GodotLogCollector.Current;
            ScriptErrorCapture.Current = null;
            RuntimeErrorCollector.Current = null;
            GodotLogCollector.Current = null;
            try { body(); }
            finally
            {
                ScriptErrorCapture.Current = priorScript;
                RuntimeErrorCollector.Current = priorRuntime;
                GodotLogCollector.Current = priorLog;
            }
        }
    }
}
