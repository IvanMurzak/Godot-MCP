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
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;
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
    /// <b>Default OFF / opt-in.</b> Nothing here runs unless the game explicitly enables it via
    /// <c>GodotMcpRuntime.Initialize(b =&gt; b.WithRuntimeErrorCapture())</c> (or by passing
    /// <c>captureRuntimeErrors: true</c>). No behavior change for a game that does not opt in. Idempotent:
    /// a second <see cref="Install"/> is a no-op; <see cref="Uninstall"/> is safe to call when nothing is
    /// installed and is invoked on handle dispose.
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
        static bool _installed;
        static RuntimeErrorCollector? _collector;
        static UnhandledExceptionEventHandler? _domainHandler;
        static EventHandler<UnobservedTaskExceptionEventArgs>? _taskHandler;

        // ── Engine-logger bridge + log seams (issue #171) ─────────────────────────────────────────────────
        //
        // The real engine logger registers through GodotScriptErrorLoggerBridge.TryInstall / .Uninstall, which
        // call OS.AddLogger / OS.RemoveLogger — native Godot APIs that FAULT (AccessViolationException) in the
        // binary-less xUnit host. The install summary also calls GD.Print (native). These delegate seams let a
        // unit test substitute a PURE-MANAGED fake bridge + log so the re-entrant rebind path (install →
        // independent bridge teardown → re-install) can be exercised and asserted without a Godot binary. In
        // production all are null and the real static bridge / GD.Print run. Mirrors the
        // GodotMcpRuntime._installForTests / _uninstallForTests pattern. Internal: test-only.
        internal static Func<ScriptErrorCapture, ScriptErrorCapture?>? _bridgeInstallForTests;
        internal static Action? _bridgeUninstallForTests;
        internal static Action<string>? _logForTests;

        /// <summary>True while capture is installed (an engine logger and/or the C# fault hooks are live).</summary>
        public static bool IsInstalled
        {
            get { lock (_gate) { return _installed; } }
        }

        /// <summary>
        /// Install all available capture channels and publish the backing <see cref="RuntimeErrorCollector"/>
        /// as <see cref="RuntimeErrorCollector.Current"/> so <c>runtime-errors-get</c> can read it. Idempotent
        /// (a second call while installed is a no-op and returns the live collector). Each channel is wired
        /// defensively — a failure to register one (e.g. the engine logger on an unexpected host) does not
        /// abort the others. Returns the collector capturing runtime errors.
        /// </summary>
        public static RuntimeErrorCollector Install()
        {
            lock (_gate)
            {
                if (_installed && _collector != null)
                {
                    // Idempotent re-entry. The engine logger lives in the bridge's SEPARATE static
                    // (GodotScriptErrorLoggerBridge._installed), not in our _installed flag, so it could have
                    // been torn down independently (e.g. an editor-side Uninstall) while ours stayed true.
                    // Re-assert it via a FRESH capture bound to _collector: if the bridge logger is gone the
                    // bridge registers anew; if it is still live but on a STALE capture (issue #171), the bridge
                    // REBINDS it to this fresh capture so engine errors route to _collector — the live collector
                    // this handle reads — instead of a collector left orphaned by a prior install/uninstall cycle.
                    TryInstallEngineLogger(_collector);
                    return _collector;
                }

                var collector = new RuntimeErrorCollector();

                // Publish state BEFORE wiring the fault hooks so a throw mid-subscribe still leaves the capture
                // discoverable AND uninstallable: Uninstall() keys on _installed, and unsubscribes only the
                // handlers it finds assigned — so a partially-installed pass never leaks a handler for the
                // process lifetime.
                RuntimeErrorCollector.Current = collector;
                _collector = collector;
                _installed = true;

                // 1) Engine error stream (Godot 4.5+). Wire a structured ErrorSink so engine errors land as
                //    full RuntimeError rows (file/line/function/type). On < 4.5 the bridge stub returns null —
                //    this channel is absent; the C# channels below still capture managed faults.
                var engineInstalled = TryInstallEngineLogger(collector);

                // 2) C# unhandled exceptions — full managed stack trace. Field assigned first, then subscribed,
                //    so even a throw between the two leaves Uninstall() with a handler reference to detach.
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

                // 3) C# unobserved faulted-Task exceptions — full managed stack trace. We intentionally do NOT
                //    call args.SetObserved(): that would change the game's behavior (suppressing the escalation
                //    a game may rely on), violating the "no behavior change unless opted in beyond capture"
                //    posture. We only OBSERVE-FOR-LOGGING here, leaving the runtime's own handling intact.
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
                if (_logForTests != null)
                    _logForTests(summary);
                else
                    GD.Print(summary);

                return collector;
            }
        }

        /// <summary>
        /// Test-only installer that wires the PURE-MANAGED channels exactly as <see cref="Install"/> does — the
        /// AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException hooks — and publishes the backing
        /// <see cref="RuntimeErrorCollector"/> as <see cref="RuntimeErrorCollector.Current"/>, but SKIPS the engine
        /// 4.5+ logger registration and the <c>GD.Print</c> summary line. Both of those call into native Godot,
        /// which faults (<c>AccessViolationException</c>, aborting the runner) in the binary-less xUnit host — so a
        /// unit test cannot exercise the real <see cref="Install"/>. After this call <see cref="IsInstalled"/> is
        /// true and <see cref="RuntimeErrorCollector.Current"/> is non-null, and <see cref="Uninstall"/> (which
        /// touches no native Godot) tears it back down — exactly the install/uninstall lifecycle the issue #165
        /// leak-guard test needs to assert on the REAL static state. Not part of the production API.
        /// </summary>
        internal static RuntimeErrorCollector InstallForTestsWithoutEngineHooks()
        {
            lock (_gate)
            {
                if (_installed && _collector != null)
                    return _collector;

                var collector = new RuntimeErrorCollector();
                RuntimeErrorCollector.Current = collector;
                _collector = collector;
                _installed = true;

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

                return collector;
            }
        }

        /// <summary>
        /// Register the Godot 4.5+ engine-error logger feeding <paramref name="collector"/> via the bridge,
        /// returning true when the engine channel is live. Best-effort: a registration failure (e.g. an
        /// unexpected host) is swallowed to a warning so the C# fault channels still install. Pre-&lt; 4.5 the
        /// bridge stub returns null and this returns false (documented degradation). Call under <c>_gate</c>.
        /// </summary>
        static bool TryInstallEngineLogger(RuntimeErrorCollector collector)
        {
            try
            {
                // Build a FRESH capture each call whose ErrorSink targets the CURRENT collector, then hand it to
                // the bridge. On a re-entrant install (issue #171) the bridge REBINDS its single live logger to
                // this new capture (see GodotScriptErrorLoggerBridge.TryInstall), so engine errors always route
                // to the collector the live handle reads — never a stale one left over from a prior install.
                var capture = new ScriptErrorCapture
                {
                    ErrorSink = record => collector.Append(RuntimeErrorFactory.FromEngine(record)),
                };
                var install = _bridgeInstallForTests ?? GodotScriptErrorLoggerBridge.TryInstall;
                return install(capture) != null;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Godot-MCP] runtime engine-error capture not installed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reverse <see cref="Install"/>: remove the engine logger (via the bridge — a no-op on &lt; 4.5),
        /// unsubscribe the AppDomain / TaskScheduler fault hooks, and clear
        /// <see cref="RuntimeErrorCollector.Current"/>. Idempotent and defensive (each step swallows its own
        /// failure) so it is safe on game shutdown / handle dispose, even mid-teardown.
        /// </summary>
        public static void Uninstall()
        {
            lock (_gate)
            {
                if (!_installed)
                    return;

                try { (_bridgeUninstallForTests ?? GodotScriptErrorLoggerBridge.Uninstall)(); }
                catch { /* swallow: a logger-removal failure must not break teardown */ }

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

                try { RuntimeErrorCollector.Current = null; } catch { /* swallow */ }
                _collector = null;
                _installed = false;
            }
        }
    }
}
