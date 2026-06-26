/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Single instance-based lifecycle coordinator that OWNS the engine-logger channel install/rebind/teardown
// and the statics-clearing policy for the error/log capture layer, with two install profiles (Editor /
// Runtime) and one idempotent teardown. It collapses the three previously-scattered teardown-clearing sites
// (GodotMcpPlugin.Teardown's OS.RemoveLogger + ScriptErrorCapture.Current re-clear, and
// RuntimeErrorCapture.Uninstall's own clears) into ONE owner, while leaving the four pure-managed cores
// (GodotLogCollector / ScriptErrorCapture / RuntimeErrorCollector / EngineErrorRecord) byte-for-byte
// unchanged. This is a facade, NOT a merge: every cross-channel invariant (#171 rebind via
// ScriptErrorCapture.PlanBridgeInstall, #163 deep backtrace, #173 collector-not-nulled, #165 leak-guard,
// #194 probe remap) is preserved because none of that code moves here.
//
// NOT gated by #if TOOLS (RuntimeErrorCapture, which delegates to this, ships into a game build) and NOT
// gated by #if GODOT4_5_OR_GREATER: this type references ONLY the version-agnostic GodotScriptErrorLoggerBridge
// (which has both the 4.5 impl and the < 4.5 stub), never Godot.Logger / OS.AddLogger directly, so it compiles
// on the 4.3 SDK floor that gates CI. It has NO Godot API surface of its own — every native touch (OS.AddLogger
// / OS.RemoveLogger via the bridge, GD.Print via the summary) is behind a delegate seam, so the manager stays
// unit-testable in the binary-less xUnit host (the seams are substituted with a pure-managed fake there).
#nullable enable
using System;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Owns the engine-logger channel lifecycle (install / rebind / teardown) and the statics-clearing policy
    /// for the error/log capture layer. An INSTANCE type (not a new static): the editor holds one for its
    /// passive-log channel; the in-game runtime holds one (via <c>RuntimeErrorCapture</c>) for its structured
    /// channel + C# fault hooks. Two install profiles, one teardown:
    /// <list type="bullet">
    /// <item><see cref="InstallEditor"/> — wires a passive <see cref="ScriptErrorCapture.LogSink"/> into the
    /// supplied <see cref="GodotLogCollector"/> so engine errors land in <c>console-get-logs</c> (the editor
    /// boot path that was <c>GodotScriptErrorLoggerBridge.TryInstall(GodotLogCollector)</c>).</item>
    /// <item><see cref="InstallRuntime"/> — publishes a fresh <see cref="RuntimeErrorCollector"/>, wires the
    /// AppDomain + TaskScheduler fault hooks, and installs the engine logger via a FRESH
    /// <see cref="ScriptErrorCapture.ErrorSink"/> per call (the in-game runtime path).</item>
    /// <item><see cref="Teardown"/> — idempotent; removes the engine logger (which also nulls
    /// <see cref="ScriptErrorCapture.Current"/>), unsubscribes the runtime fault hooks, and clears
    /// <see cref="RuntimeErrorCollector.Current"/> (runtime profile only). Deliberately does NOT null
    /// <see cref="GodotLogCollector.Current"/> (issue #173).</item>
    /// </list>
    ///
    /// <para>
    /// Every native touch is behind a delegate seam: <c>bridgeInstall</c> / <c>bridgeUninstall</c> for
    /// <c>OS.AddLogger</c> / <c>OS.RemoveLogger</c> (through <see cref="GodotScriptErrorLoggerBridge"/>), and
    /// <c>log</c> for <c>GD.Print</c>. In production the callers pass the real bridge + <c>GD.Print</c>; a unit
    /// test substitutes a pure-managed fake bridge + no-op log so install / rebind / teardown can be asserted
    /// with no Godot binary (the same seam pattern <c>RuntimeErrorCapture</c> already exposes).
    /// </para>
    /// </summary>
    public sealed class ErrorCaptureManager
    {
        readonly object _gate = new();

        // Editor profile: the console-get-logs buffer the passive LogSink feeds. Null for the runtime profile.
        readonly GodotLogCollector? _logCollector;
        // Runtime profile: the runtime-errors-get buffer the structured ErrorSink + C# fault hooks feed. Null
        // for the editor profile. Non-null is ALSO the runtime-profile discriminator used by Teardown.
        readonly RuntimeErrorCollector? _runtimeCollector;

        // Native-touch seams (see the class remarks). bridgeInstall mirrors
        // GodotScriptErrorLoggerBridge.TryInstall(ScriptErrorCapture) → ScriptErrorCapture.PlanBridgeInstall
        // (RegisterNew / Rebind / AlreadyCurrent); bridgeUninstall mirrors GodotScriptErrorLoggerBridge.Uninstall
        // (OS.RemoveLogger + nulls ScriptErrorCapture.Current); log mirrors GD.Print.
        readonly Func<ScriptErrorCapture, ScriptErrorCapture?> _bridgeInstall;
        readonly Action _bridgeUninstall;
        readonly Action<string> _log;

        bool _installed;

        // Runtime profile only: the AppDomain / TaskScheduler fault-hook handlers, assigned on install and
        // unsubscribed on teardown. Held so Teardown detaches exactly the handlers it subscribed.
        UnhandledExceptionEventHandler? _domainHandler;
        EventHandler<UnobservedTaskExceptionEventArgs>? _taskHandler;

        ErrorCaptureManager(
            GodotLogCollector? logCollector,
            RuntimeErrorCollector? runtimeCollector,
            Func<ScriptErrorCapture, ScriptErrorCapture?> bridgeInstall,
            Action bridgeUninstall,
            Action<string> log)
        {
            _logCollector = logCollector;
            _runtimeCollector = runtimeCollector;
            _bridgeInstall = bridgeInstall ?? throw new ArgumentNullException(nameof(bridgeInstall));
            _bridgeUninstall = bridgeUninstall ?? throw new ArgumentNullException(nameof(bridgeUninstall));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>True while this manager's channel(s) are installed. Idempotent <see cref="Teardown"/> keys on it.</summary>
        public bool IsInstalled
        {
            get { lock (_gate) { return _installed; } }
        }

        /// <summary>
        /// The runtime-profile collector this manager publishes as <see cref="RuntimeErrorCollector.Current"/>,
        /// or null for the editor profile. Used by <c>RuntimeErrorCapture</c>'s facade to return the live
        /// collector from <c>Install()</c> without re-reading the static.
        /// </summary>
        internal RuntimeErrorCollector? RuntimeCollector => _runtimeCollector;

        // ── Editor profile ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Install the EDITOR passive-log channel: build a fresh <see cref="ScriptErrorCapture"/> whose
        /// <see cref="ScriptErrorCapture.LogSink"/> appends every engine error/warning to
        /// <paramref name="collector"/> (the <c>console-get-logs</c> buffer), and hand it to
        /// <paramref name="bridgeInstall"/> (→ <see cref="ScriptErrorCapture.PlanBridgeInstall"/> →
        /// RegisterNew / Rebind / AlreadyCurrent). Behaviourally identical to the prior
        /// <c>GodotScriptErrorLoggerBridge.TryInstall(GodotLogCollector)</c> call in <c>GodotMcpPlugin.EnterTreeBoot</c>.
        /// Returns the manager so the caller can <see cref="Teardown"/> it on plugin unload.
        /// </summary>
        public static ErrorCaptureManager InstallEditor(
            GodotLogCollector collector,
            Func<ScriptErrorCapture, ScriptErrorCapture?> bridgeInstall,
            Action bridgeUninstall,
            Action<string> log)
        {
            if (collector == null)
                throw new ArgumentNullException(nameof(collector));

            var manager = new ErrorCaptureManager(
                logCollector: collector, runtimeCollector: null, bridgeInstall, bridgeUninstall, log);

            lock (manager._gate)
            {
                // Mirror GodotScriptErrorLoggerBridge.TryInstall(GodotLogCollector): a fresh router whose passive
                // LogSink flattens each engine error/warning into a console-get-logs line. Reads the stored
                // _logCollector field (== collector, assigned in the ctor above) so the editor profile's buffer is
                // a genuine read, symmetric with the runtime profile's _runtimeCollector.
                var capture = new ScriptErrorCapture
                {
                    LogSink = (logType, message) => manager._logCollector!.Append(logType, message),
                };
                manager._bridgeInstall(capture);
                manager._installed = true;
            }

            return manager;
        }

        // ── Runtime profile ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Construct a RUNTIME-profile manager and publish its fresh <see cref="RuntimeErrorCollector"/> as
        /// <see cref="RuntimeErrorCollector.Current"/> + mark it installed, WITHOUT yet wiring the engine logger
        /// or the C# fault hooks. Split from <see cref="InstallRuntimeProfile"/> so the caller
        /// (<c>RuntimeErrorCapture.Install</c>) can publish the manager reference it will later
        /// <see cref="Teardown"/> BEFORE the hooks are wired — so a throw mid-wiring still leaves an
        /// uninstallable manager (mirrors the prior "publish state before wiring the fault hooks" leak-safety,
        /// the issue #165 sibling). Most callers want the all-in-one <see cref="InstallRuntime"/>.
        /// </summary>
        internal static ErrorCaptureManager CreateRuntime(
            Func<ScriptErrorCapture, ScriptErrorCapture?> bridgeInstall,
            Action bridgeUninstall,
            Action<string> log)
        {
            var manager = new ErrorCaptureManager(
                logCollector: null, runtimeCollector: new RuntimeErrorCollector(), bridgeInstall, bridgeUninstall, log);

            lock (manager._gate)
            {
                // Publish state BEFORE wiring the fault hooks (below, in InstallRuntimeProfile) so a throw mid-
                // subscribe still leaves the capture discoverable AND uninstallable.
                RuntimeErrorCollector.Current = manager._runtimeCollector;
                manager._installed = true;
            }

            return manager;
        }

        /// <summary>
        /// Wire the RUNTIME profile's channels onto a manager produced by <see cref="CreateRuntime"/>: install
        /// the engine logger via a FRESH <see cref="ScriptErrorCapture.ErrorSink"/> (the per-install freshness is
        /// load-bearing for the #171 rebind — a re-entrant install resolves to Rebind), then subscribe the
        /// AppDomain + TaskScheduler fault hooks, then print the install summary through the <c>log</c> seam.
        /// Verbatim from the prior <c>RuntimeErrorCapture.Install</c> body.
        /// </summary>
        internal void InstallRuntimeProfile()
        {
            lock (_gate)
            {
                var collector = _runtimeCollector!;

                // 1) Engine error stream (Godot 4.5+). On < 4.5 the bridge stub returns null → channel absent.
                var engineInstalled = TryInstallRuntimeEngineLogger(collector);

                // 2) C# unhandled exceptions — full managed stack trace. Field assigned first, then subscribed,
                //    so even a throw between the two leaves Teardown() with a handler reference to detach.
                _domainHandler = (_, args) =>
                {
                    try
                    {
                        collector.Append(RuntimeErrorFactory.FromException(
                            RuntimeErrorSource.UnhandledException, args.ExceptionObject as Exception));
                    }
                    catch { /* a fault handler must never throw */ }
                };
                AppDomain.CurrentDomain.UnhandledException += _domainHandler;

                // 3) C# unobserved faulted-Task exceptions. We intentionally do NOT call args.SetObserved() —
                //    OBSERVE-FOR-LOGGING only, leaving the runtime's own escalation handling intact.
                _taskHandler = (_, args) =>
                {
                    try
                    {
                        collector.Append(RuntimeErrorFactory.FromException(
                            RuntimeErrorSource.UnobservedTaskException, args.Exception));
                    }
                    catch { /* a fault handler must never throw */ }
                };
                TaskScheduler.UnobservedTaskException += _taskHandler;

                var summary = engineInstalled
                    ? "[Godot-MCP] runtime error capture installed (engine 4.5+ logger + C# exception hooks)."
                    : "[Godot-MCP] runtime error capture installed (C# exception hooks only; engine logger " +
                      "requires Godot 4.5+).";
                _log(summary);
            }
        }

        /// <summary>
        /// Construct + fully install a RUNTIME-profile manager in one call (publish collector, wire engine logger,
        /// subscribe the C# fault hooks, print the summary). The named API the design specifies; also the path
        /// the <c>ErrorCaptureManagerTests</c> drive directly with a fake bridge. <c>RuntimeErrorCapture.Install</c>
        /// uses the two-phase <see cref="CreateRuntime"/> + <see cref="InstallRuntimeProfile"/> instead, so it can
        /// publish the manager reference before the hooks wire (leak-safety).
        /// </summary>
        public static ErrorCaptureManager InstallRuntime(
            Func<ScriptErrorCapture, ScriptErrorCapture?> bridgeInstall,
            Action bridgeUninstall,
            Action<string> log)
        {
            var manager = CreateRuntime(bridgeInstall, bridgeUninstall, log);
            manager.InstallRuntimeProfile();
            return manager;
        }

        /// <summary>
        /// Re-assert the engine logger for the runtime profile by handing the bridge a FRESH capture bound to the
        /// live <see cref="RuntimeCollector"/> — the idempotent re-entry path of <c>RuntimeErrorCapture.Install</c>
        /// (#171): if the bridge logger was torn down independently it registers anew; if it is live on a stale
        /// capture it rebinds to this fresh one so engine errors route to the current collector. No-op when not
        /// installed or not a runtime profile.
        /// </summary>
        internal void ReassertRuntimeEngineLogger()
        {
            lock (_gate)
            {
                if (!_installed || _runtimeCollector == null)
                    return;
                TryInstallRuntimeEngineLogger(_runtimeCollector);
            }
        }

        /// <summary>
        /// Build a FRESH <see cref="ScriptErrorCapture"/> whose <see cref="ScriptErrorCapture.ErrorSink"/> feeds
        /// <paramref name="collector"/> and hand it to the bridge seam, returning true when the engine channel is
        /// live. Best-effort: a registration failure is swallowed to a routed warning so the C# fault channels
        /// still install (and pre-&lt; 4.5 the stub returns null → false). Call under <c>_gate</c>.
        /// </summary>
        bool TryInstallRuntimeEngineLogger(RuntimeErrorCollector collector)
        {
            try
            {
                var capture = new ScriptErrorCapture
                {
                    ErrorSink = record => collector.Append(RuntimeErrorFactory.FromEngine(record)),
                };
                return _bridgeInstall(capture) != null;
            }
            catch (Exception ex)
            {
                // Routed through the log seam (not a direct GD.PushWarning) so the manager stays Godot-free and
                // unit-testable; reached only when the REAL 4.5 bridge throws during OS.AddLogger (rare).
                _log($"[Godot-MCP] runtime engine-error capture not installed: {ex.Message}");
                return false;
            }
        }

        // ── Teardown (single statics-clearing owner) ─────────────────────────────────────────────────────────

        /// <summary>
        /// Reverse the install: remove the engine logger (via the <c>bridgeUninstall</c> seam — which also nulls
        /// <see cref="ScriptErrorCapture.Current"/>), unsubscribe the runtime fault hooks, and clear
        /// <see cref="RuntimeErrorCollector.Current"/> (runtime profile only). Idempotent (no-op on a second call)
        /// and defensive (each step swallows its own failure) so it is safe on the editor's ALC-unload teardown
        /// and on game shutdown / handle dispose. The single owner of the statics-clearing policy that previously
        /// lived in three scattered sites.
        /// </summary>
        public void Teardown()
        {
            lock (_gate)
            {
                if (!_installed)
                    return;

                // Remove the engine logger + null ScriptErrorCapture.Current (the bridge does both). For the editor
                // this is OS.RemoveLogger (main-thread only — the caller invokes Teardown inside its main-thread
                // guard); for the runtime it is the bridge teardown. Swallowed so a removal failure never breaks
                // the rest of teardown.
                try { _bridgeUninstall(); }
                catch { /* a logger-removal failure must not break teardown */ }

                // Runtime profile only: unsubscribe the C# fault hooks (the editor profile never assigned them).
                if (_domainHandler != null)
                {
                    try { AppDomain.CurrentDomain.UnhandledException -= _domainHandler; }
                    catch { /* swallow */ }
                    _domainHandler = null;
                }

                if (_taskHandler != null)
                {
                    try { TaskScheduler.UnobservedTaskException -= _taskHandler; }
                    catch { /* swallow */ }
                    _taskHandler = null;
                }

                // Runtime profile only: clear the runtime collector. Gated on _runtimeCollector so the EDITOR
                // teardown never touches RuntimeErrorCollector.Current (it owns the editor's passive-log channel
                // only, an unrelated static).
                if (_runtimeCollector != null)
                {
                    try { RuntimeErrorCollector.Current = null; }
                    catch { /* swallow */ }
                }

                // GodotLogCollector.Current is DELIBERATELY NOT nulled here (issue #173): the bounded ring of plain
                // managed LogEntry rows must stay readable through the teardown / reload window — the background
                // framework log-routing path reads Current?.Append off arbitrary threads, so a null would silently
                // drop exactly the diagnostics an operator most wants. The next _EnterTree replaces it
                // (last-writer-wins). A future refactor must NOT re-add a GodotLogCollector.Current = null here.

                _installed = false;
            }
        }
    }
}
