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
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Runtime
{
    /// <summary>
    /// Installs in-game runtime error capture so errors raised inside a RUNNING game (not just editor-side
    /// script errors) are captured in-process and surfaced to the agent over the existing MCP/SignalR channel
    /// via the <c>runtime-errors-get</c> tool. Closes issue #160: previously an agent could launch the game,
    /// poll for logs, see silence, and wrongly conclude the game was healthy.
    ///
    /// <para>
    /// Three independent capture channels, each best-effort and gracefully degrading:
    /// <list type="number">
    /// <item><b>Engine error stream (Godot 4.5+)</b> — registers a <c>Godot.Logger</c> via
    /// <c>OS.AddLogger</c> (through <see cref="GodotScriptErrorLoggerBridge"/>) so GDScript runtime errors,
    /// <c>push_error</c>/<c>push_warning</c>, and shader errors are captured with their origin
    /// (file/line/function). On Godot &lt; 4.5 the bridge is a no-op stub — this channel is simply absent
    /// (documented degradation), and the C# channels below still work.</item>
    /// <item><b>C# unhandled exceptions</b> — <c>AppDomain.CurrentDomain.UnhandledException</c>, with the
    /// full managed stack trace.</item>
    /// <item><b>C# unobserved Task exceptions</b> — <c>TaskScheduler.UnobservedTaskException</c>, with the
    /// full managed stack trace.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>A thin runtime-profile facade over <see cref="ErrorCaptureManager"/>.</b> The lifecycle mechanics
    /// (publish the collector, wire the engine logger via a fresh capture per install — the #171 rebind freshness —
    /// subscribe/unsubscribe the C# fault hooks, the statics-clearing policy) live in
    /// <see cref="ErrorCaptureManager"/>'s runtime profile. This class keeps the public <see cref="Install"/> /
    /// <see cref="Uninstall"/> / <see cref="IsInstalled"/> surface its consumers (<c>GodotMcpRuntime</c>,
    /// <c>GodotMcpRuntimeHandle</c>, the #165 leak-guard) already call, and forwards its own test seams
    /// (<see cref="_bridgeInstallForTests"/> / <see cref="_bridgeUninstallForTests"/> / <see cref="_logForTests"/>)
    /// into the manager ctor so the existing <c>RuntimeErrorCaptureRebindTests</c> / <c>RuntimeErrorCaptureTests</c>
    /// drive the manager's runtime profile end-to-end unchanged.
    /// </para>
    ///
    /// <para>
    /// <b>Default OFF / opt-in.</b> Nothing here runs unless the game explicitly enables it via
    /// <c>GodotMcpRuntime.Initialize(b =&gt; b.WithRuntimeErrorCapture())</c> (or by passing
    /// <c>captureRuntimeErrors: true</c>). No behavior change for a game that does not opt in. Idempotent:
    /// a second <see cref="Install"/> is a no-op (re-asserts the engine logger to the live collector);
    /// <see cref="Uninstall"/> is safe to call when nothing is installed and is invoked on handle dispose.
    /// </para>
    ///
    /// <para>
    /// <b>Main-thread note.</b> <c>OS.AddLogger</c> is an engine call; the editor parallel registers it on the
    /// main thread. <see cref="Install"/> is called from <c>GodotMcpRuntime.Build()</c>, which the developer
    /// invokes from game code (typically an autoload <c>_Ready</c>, i.e. the main thread). The AppDomain /
    /// TaskScheduler subscriptions are pure-managed and thread-agnostic.
    /// </para>
    /// </summary>
    public static class RuntimeErrorCapture
    {
        static readonly object _gate = new();

        // The runtime-profile manager that owns the live channels (engine logger + C# fault hooks + the
        // published RuntimeErrorCollector). Null when nothing is installed.
        static ErrorCaptureManager? _manager;

        // ── Engine-logger bridge + log seams (issue #171) ─────────────────────────────────────────────────
        //
        // The real engine logger registers through GodotScriptErrorLoggerBridge.TryInstall / .Uninstall, which
        // call OS.AddLogger / OS.RemoveLogger — native Godot APIs that FAULT (AccessViolationException) in the
        // binary-less xUnit host. The install summary also calls GD.Print (native). These delegate seams let a
        // unit test substitute a PURE-MANAGED fake bridge + log so the re-entrant rebind path (install →
        // independent bridge teardown → re-install) can be exercised and asserted without a Godot binary. They
        // are read at Install()/Uninstall() time and forwarded into the ErrorCaptureManager ctor. In production
        // all are null and the real static bridge / GD.Print run. Mirrors the
        // GodotMcpRuntime._installForTests / _uninstallForTests pattern. Internal: test-only.
        //
        // CS0649 is suppressed here because these seams are assigned ONLY via reflection from
        // Godot-MCP.Tests; in the addon/testbed build (no test assembly) they are never assigned and the
        // compiler otherwise warns "field is never assigned to". Keeping the seams (read via `_x ?? <real>`)
        // is intentional, so suppress rather than remove.
#pragma warning disable CS0649
        internal static Func<ScriptErrorCapture, ScriptErrorCapture?>? _bridgeInstallForTests;
        internal static Action? _bridgeUninstallForTests;
        internal static Action<string>? _logForTests;
#pragma warning restore CS0649

        /// <summary>True while capture is installed (an engine logger and/or the C# fault hooks are live).</summary>
        public static bool IsInstalled
        {
            get { lock (_gate) { return _manager?.IsInstalled ?? false; } }
        }

        /// <summary>
        /// Install all available capture channels and publish the backing <see cref="RuntimeErrorCollector"/>
        /// as <see cref="RuntimeErrorCollector.Current"/> so <c>runtime-errors-get</c> can read it. Idempotent
        /// (a second call while installed re-asserts the engine logger to the live collector — the #171 rebind
        /// re-entry — and returns it). Each channel is wired defensively — a failure to register one (e.g. the
        /// engine logger on an unexpected host) does not abort the others. Returns the collector capturing
        /// runtime errors. Delegates the lifecycle to <see cref="ErrorCaptureManager"/>'s runtime profile,
        /// forwarding the current test seams (or the real bridge / <c>GD.Print</c> in production) into it.
        /// </summary>
        public static RuntimeErrorCollector Install()
        {
            lock (_gate)
            {
                if (_manager != null && _manager.IsInstalled)
                {
                    // Idempotent re-entry. The engine logger lives in the bridge's SEPARATE static, not in the
                    // manager's _installed flag, so it could have been torn down independently (e.g. an editor-side
                    // Uninstall) while ours stayed installed. Re-assert it via a FRESH capture bound to the live
                    // collector: if the bridge logger is gone it registers anew; if it is live but on a STALE
                    // capture (issue #171) it REBINDS to the fresh one so engine errors route to the live collector.
                    _manager.ReassertRuntimeEngineLogger();
                    return _manager.RuntimeCollector!;
                }

                // Two-phase install so the manager reference is published BEFORE its channels wire: a throw mid-
                // wiring then still leaves an uninstallable manager that Uninstall() can tear down (the issue #165
                // sibling — never leak a process-wide hook with no owner to remove it).
                var manager = ErrorCaptureManager.CreateRuntime(
                    bridgeInstall: _bridgeInstallForTests ?? GodotScriptErrorLoggerBridge.TryInstall,
                    bridgeUninstall: _bridgeUninstallForTests ?? GodotScriptErrorLoggerBridge.Uninstall,
                    log: _logForTests ?? GD.Print);
                _manager = manager;
                manager.InstallRuntimeProfile();
                return manager.RuntimeCollector!;
            }
        }

        /// <summary>
        /// Test-only installer that wires the PURE-MANAGED channels exactly as <see cref="Install"/> does — the
        /// AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException hooks — and publishes the backing
        /// <see cref="RuntimeErrorCollector"/> as <see cref="RuntimeErrorCollector.Current"/>, but SKIPS the engine
        /// 4.5+ logger registration and the <c>GD.Print</c> summary line (both call into native Godot, which faults
        /// — <c>AccessViolationException</c>, aborting the runner — in the binary-less xUnit host). It runs the
        /// manager's runtime profile with no-op bridge + log seams to achieve that. After this call
        /// <see cref="IsInstalled"/> is true and <see cref="RuntimeErrorCollector.Current"/> is non-null, and
        /// <see cref="Uninstall"/> (which touches no native Godot) tears it back down — exactly the install/uninstall
        /// lifecycle the issue #165 leak-guard test needs to assert on the REAL static state. Not part of the
        /// production API.
        /// </summary>
        internal static RuntimeErrorCollector InstallForTestsWithoutEngineHooks()
        {
            lock (_gate)
            {
                if (_manager != null && _manager.IsInstalled)
                    return _manager.RuntimeCollector!;

                var manager = ErrorCaptureManager.CreateRuntime(
                    bridgeInstall: static _ => null,    // SKIP the engine 4.5+ logger (OS.AddLogger faults in the host)
                    bridgeUninstall: static () => { },  // nothing native to remove
                    log: static _ => { });              // SKIP the GD.Print summary (native — faults in the host)
                _manager = manager;
                manager.InstallRuntimeProfile();
                return manager.RuntimeCollector!;
            }
        }

        /// <summary>
        /// Reverse <see cref="Install"/>: remove the engine logger (via the bridge — a no-op on &lt; 4.5),
        /// unsubscribe the AppDomain / TaskScheduler fault hooks, and clear
        /// <see cref="RuntimeErrorCollector.Current"/>. Idempotent and defensive (each step swallows its own
        /// failure inside <see cref="ErrorCaptureManager.Teardown"/>) so it is safe on game shutdown / handle
        /// dispose, even mid-teardown.
        /// </summary>
        public static void Uninstall()
        {
            lock (_gate)
            {
                _manager?.Teardown();
                _manager = null;
            }
        }
    }
}
