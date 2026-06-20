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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Runtime;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Issue #171 regression coverage: an idempotent <see cref="RuntimeErrorCapture"/> re-install must NOT leave
    /// the live engine logger forwarding to a STALE capture's sink. Before the fix, the bridge's already-installed
    /// path returned the existing <see cref="ScriptErrorCapture.Current"/> without rebinding, so after
    /// "install A → independent engine-logger Uninstall → Install again" engine errors kept routing to the FIRST
    /// capture's sink — a collector the current handle no longer reads, so <c>runtime-errors-get</c> falls silently
    /// quiet (the #160 failure this whole feature prevents).
    ///
    /// <para>
    /// The real engine logger registers through native <c>OS.AddLogger</c>, which faults in the binary-less xUnit
    /// host. So these tests exercise the load-bearing decision two ways, both pure-managed:
    /// <list type="number">
    /// <item>directly against <see cref="ScriptErrorCapture.PlanBridgeInstall"/> — the SAME coordinator the real
    /// Godot 4.5+ bridge calls to choose RegisterNew / Rebind / AlreadyCurrent and republish
    /// <see cref="ScriptErrorCapture.Current"/>; and</item>
    /// <item>end-to-end through <see cref="RuntimeErrorCapture.Install"/>/<see cref="RuntimeErrorCapture.Uninstall"/>
    /// with a PURE-MANAGED fake bridge (driving <see cref="ScriptErrorCapture.PlanBridgeInstall"/> + a single live
    /// logger) injected via the test seams, so the full re-entrant scenario routes a real engine error and asserts
    /// it lands in the CURRENT collector.</item>
    /// </list>
    /// Both bind to production rebind logic — reverting the fix turns them red.
    /// </para>
    ///
    /// <para>
    /// These tests touch process-wide statics (<see cref="ScriptErrorCapture.Current"/>,
    /// <see cref="RuntimeErrorCollector.Current"/>, and <see cref="RuntimeErrorCapture"/>'s install state +
    /// bridge seams). They run in their own xUnit collection (serialized — no parallel sibling) and every test
    /// saves + restores that state in a finally, so a failed assertion can never leak into another test.
    /// </para>
    /// </summary>
    [Collection(nameof(RuntimeErrorCaptureRebindTests))]
    [CollectionDefinition(nameof(RuntimeErrorCaptureRebindTests), DisableParallelization = true)]
    public class RuntimeErrorCaptureRebindTests
    {
        // ── PlanBridgeInstall: the pure-managed decision the real bridge shares ─────────────────────────────

        [Fact]
        public void PlanBridgeInstall_NoLiveLogger_RegistersNew_AndPublishesIncoming()
        {
            WithCleanScriptCaptureCurrent(() =>
            {
                var incoming = new ScriptErrorCapture();

                var action = ScriptErrorCapture.PlanBridgeInstall(liveCapture: null, incoming);

                Assert.Equal(BridgeInstallAction.RegisterNew, action);
                Assert.Same(incoming, ScriptErrorCapture.Current); // published router tracks the live logger
            });
        }

        [Fact]
        public void PlanBridgeInstall_SameCapture_IsAlreadyCurrent_NoChurn()
        {
            WithCleanScriptCaptureCurrent(() =>
            {
                var live = new ScriptErrorCapture();

                var action = ScriptErrorCapture.PlanBridgeInstall(liveCapture: live, incoming: live);

                Assert.Equal(BridgeInstallAction.AlreadyCurrent, action);
                Assert.Same(live, ScriptErrorCapture.Current);
            });
        }

        [Fact]
        public void PlanBridgeInstall_DifferentCapture_RequestsRebind_AndRepublishesIncoming()
        {
            // The crux of #171: a live logger on capture A asked to install capture B must REBIND (not keep A),
            // and the published Current must advance to B so it never lags the live logger.
            WithCleanScriptCaptureCurrent(() =>
            {
                var stale = new ScriptErrorCapture();
                ScriptErrorCapture.Current = stale;
                var incoming = new ScriptErrorCapture();

                var action = ScriptErrorCapture.PlanBridgeInstall(liveCapture: stale, incoming);

                Assert.Equal(BridgeInstallAction.Rebind, action);
                Assert.Same(incoming, ScriptErrorCapture.Current); // NOT left on the stale capture
            });
        }

        [Fact]
        public void PlanBridgeInstall_NullIncoming_Throws()
        {
            WithCleanScriptCaptureCurrent(() =>
                Assert.Throws<ArgumentNullException>(() =>
                    ScriptErrorCapture.PlanBridgeInstall(liveCapture: null, incoming: null!)));
        }

        // ── End-to-end: RuntimeErrorCapture re-install rebinds the live logger to the current collector ──────

        [Fact]
        public void Install_WhenBridgeAlreadyLiveOnAnotherCapture_RebindsLiveLoggerToRuntimeCollector_NotStale()
        {
            // The exact #171 cross-consumer scenario, end-to-end with a pure-managed fake bridge whose single
            // live logger SURVIVES across the install (models the editor having registered the bridge first, or a
            // re-install where the prior logger was never removed):
            //   1. A logger is ALREADY live, forwarding to a STALE capture (e.g. the editor's log capture).
            //   2. RuntimeErrorCapture.Install() builds a NEW capture whose ErrorSink feeds the runtime collector B
            //      and hands it to the bridge. The bridge is already live → it MUST REBIND the live logger to the
            //      runtime capture (pre-fix it kept the stale one — the bug).
            //   3. A routed engine error must land in the runtime collector B (the CURRENT one), NOT the stale sink.
            var fakeBridge = new FakeEngineLoggerBridge();
            WithFakeBridge(fakeBridge, () =>
            {
                // 1) Pre-register a live logger on a STALE capture whose errors land in a collector NOBODY reads
                //    anymore (the orphaned sink the live handle no longer polls).
                var staleErrors = new List<EngineErrorRecord>();
                var staleCapture = new ScriptErrorCapture { ErrorSink = r => staleErrors.Add(r) };
                fakeBridge.TryInstall(staleCapture);
                Assert.True(fakeBridge.HasLiveLogger);
                Assert.Same(staleCapture, fakeBridge.LiveCapture);

                // 2) The runtime opts in — Install builds a fresh capture wired to collector B and hands it to the
                //    already-live bridge, which must rebind.
                var collectorB = RuntimeErrorCapture.Install();
                Assert.Same(collectorB, RuntimeErrorCollector.Current);
                Assert.NotSame(staleCapture, fakeBridge.LiveCapture); // REBOUND — no longer the stale capture

                // 3) The crux: a freshly-routed engine error lands in the runtime collector B, NOT the stale sink.
                fakeBridge.RaiseEngineError("res://b.gd", 2, "boom-B");

                Assert.Equal(1, collectorB.Count);          // current collector received it
                Assert.Empty(staleErrors);                  // the stale sink got NOTHING (pre-fix: it would have)
                var captured = collectorB.QuerySince(0, 100, out _).Single();
                Assert.Equal("boom-B", captured.Message);
                Assert.Equal(RuntimeErrorSource.Engine, captured.Source);
            });
        }

        [Fact]
        public void Reinstall_AfterFullCycle_RoutesEngineErrorsToTheNewCollector()
        {
            // install A → full RuntimeErrorCapture.Uninstall (drops the bridge logger) → Install B → route:
            // the new collector B receives the error and the dropped collector A does not. Complements the
            // surviving-logger test above with the clean teardown/re-register path.
            var fakeBridge = new FakeEngineLoggerBridge();
            WithFakeBridge(fakeBridge, () =>
            {
                var collectorA = RuntimeErrorCapture.Install();
                fakeBridge.RaiseEngineError("res://a.gd", 1, "boom-A");
                Assert.Equal(1, collectorA.Count);

                RuntimeErrorCapture.Uninstall();
                Assert.False(fakeBridge.HasLiveLogger);

                var collectorB = RuntimeErrorCapture.Install();
                Assert.NotSame(collectorA, collectorB);

                fakeBridge.RaiseEngineError("res://b.gd", 2, "boom-B");
                Assert.Equal(1, collectorB.Count);          // current collector received it
                Assert.Equal(1, collectorA.Count);          // dropped collector unchanged (still just boom-A)
            });
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A pure-managed stand-in for <c>GodotScriptErrorLoggerBridge</c> that exercises the REAL decision
        /// (<see cref="ScriptErrorCapture.PlanBridgeInstall"/>) and models the single live engine logger — without
        /// touching native <c>OS.AddLogger</c>. <see cref="RaiseEngineError"/> drives an error through the live
        /// logger exactly as Godot's <c>_LogError</c> callback would, into whatever capture the logger is currently
        /// bound to — so a missing rebind shows up as the error reaching the stale capture/collector.
        /// </summary>
        sealed class FakeEngineLoggerBridge
        {
            ScriptErrorCapture? _live; // the one registered logger's current capture (null when none registered)

            public bool HasLiveLogger => _live != null;
            public ScriptErrorCapture? LiveCapture => _live;

            public ScriptErrorCapture? TryInstall(ScriptErrorCapture capture)
            {
                if (capture == null)
                    return null;

                // SAME production decision the real Godot 4.5+ bridge makes (issue #171).
                var action = ScriptErrorCapture.PlanBridgeInstall(_live, capture);
                switch (action)
                {
                    case BridgeInstallAction.AlreadyCurrent:
                        break; // live logger already on this capture — nothing to do
                    case BridgeInstallAction.Rebind:
                    case BridgeInstallAction.RegisterNew:
                    default:
                        // Both register-new and rebind end with the single live logger forwarding to `capture`.
                        // (Real bridge: RegisterNew = OS.AddLogger(new logger); Rebind = logger.RebindCapture.)
                        _live = capture;
                        break;
                }
                return capture;
            }

            public void Uninstall()
            {
                // Models OS.RemoveLogger + clearing the router: the live logger is gone and Current is nulled.
                _live = null;
                ScriptErrorCapture.Current = null;
            }

            /// <summary>Route an engine error through the live logger's CURRENT capture (mirrors _LogError).</summary>
            public void RaiseEngineError(string file, int line, string message)
            {
                Assert.NotNull(_live); // an error can only arrive while a logger is registered
                _live!.Route(EngineErrorKind.Error, file, line, message: null, rationale: message);
            }
        }

        static void WithFakeBridge(FakeEngineLoggerBridge bridge, Action body)
        {
            var priorInstall = RuntimeErrorCapture._bridgeInstallForTests;
            var priorUninstall = RuntimeErrorCapture._bridgeUninstallForTests;
            var priorLog = RuntimeErrorCapture._logForTests;
            var priorCollector = RuntimeErrorCollector.Current;
            var priorScriptCurrent = ScriptErrorCapture.Current;

            // No prior install state may bleed in (the seams are not yet pointed at the fake, so this uses the
            // stub bridge — a no-op on the 4.3 test pin — which is fine: we only need RuntimeErrorCapture's own
            // static flags reset).
            RuntimeErrorCapture.Uninstall();

            RuntimeErrorCapture._bridgeInstallForTests = bridge.TryInstall;
            RuntimeErrorCapture._bridgeUninstallForTests = bridge.Uninstall;
            // Substitute the install-summary GD.Print (native — faults in the binary-less host) with a no-op.
            RuntimeErrorCapture._logForTests = _ => { };
            try
            {
                body();
            }
            finally
            {
                try { RuntimeErrorCapture.Uninstall(); } catch { /* best-effort cleanup */ }
                RuntimeErrorCapture._bridgeInstallForTests = priorInstall;
                RuntimeErrorCapture._bridgeUninstallForTests = priorUninstall;
                RuntimeErrorCapture._logForTests = priorLog;
                RuntimeErrorCollector.Current = priorCollector;
                ScriptErrorCapture.Current = priorScriptCurrent;
            }
        }

        static void WithCleanScriptCaptureCurrent(Action body)
        {
            var prior = ScriptErrorCapture.Current;
            ScriptErrorCapture.Current = null;
            try { body(); }
            finally { ScriptErrorCapture.Current = prior; }
        }
    }
}
