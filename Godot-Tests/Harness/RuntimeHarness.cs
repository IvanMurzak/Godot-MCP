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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Runtime;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using HubConnectionState = Microsoft.AspNetCore.SignalR.Client.HubConnectionState;

namespace com.IvanMurzak.Godot.MCP.Tests.Project.Harness
{
    /// <summary>
    /// CI runtime-integration harness (issue #186). Boots the in-game Godot-MCP <b>runtime</b> path inside a
    /// HEADLESS game (NOT the editor), connects it to a LOCAL <c>gamedev-mcp-server</c> over SignalR, raises
    /// a representative set of runtime errors, reads them back through
    /// <see cref="RuntimeErrorCollector.Current"/>, and writes a STRUCTURED result file (+ stdout) so a CI
    /// step can assert:
    /// <list type="number">
    ///   <item>the live transport connected (handshake) — proving the <c>#if TOOLS</c>-excluded connect path
    ///   actually works end-to-end (today CI only does an editor-load smoke);</item>
    ///   <item>a tool roundtrip works (the CI step POSTs <c>ping</c> to the server while this game holds the
    ///   connection — exercised CI-side, this harness only proves the connection is up);</item>
    ///   <item><c>runtime-errors-get</c>'s store shows the engine GDScript runtime error WITH the #163
    ///   multi-frame backtrace (Godot 4.5+), the <c>push_error</c>/<c>push_warning</c>, and the C# exception
    ///   with a stack — and on Godot 4.3/4.4 that the engine channel degrades gracefully (frames null).</item>
    /// </list>
    ///
    /// <para>
    /// <b>Env-guarded.</b> The whole harness only runs when <c>GODOT_MCP_HARNESS=1</c> — a normal editor load
    /// / a normal game run is unaffected (the autoload returns immediately). Connection is driven by the
    /// addon's own <c>GODOT_MCP_*</c> env contract (host/mode/auth) set by the CI step; when
    /// <c>GODOT_MCP_HARNESS_REQUIRE_CONNECT=0</c> the harness still captures + writes the result but does not
    /// FAIL on a missing connection (used only for a connection-free local capture smoke).
    /// </para>
    ///
    /// <para>
    /// This file lives under <c>Godot-Tests/</c> (the CI testbed project), NOT under <c>addons/godot_mcp/</c>
    /// — it is test/CI scaffolding, never shipped in the addon. It compiles into the testbed assembly
    /// alongside the copied-in addon sources, so it can call the addon's public runtime API directly.
    /// </para>
    /// </summary>
    public partial class RuntimeHarness : Node
    {
        // --- Env contract (read once at boot) ---------------------------------------------------------

        /// <summary>Master gate — the harness runs ONLY when this is exactly "1".</summary>
        const string EnvHarnessEnabled = "GODOT_MCP_HARNESS";

        /// <summary>Absolute path the structured JSON result is written to. Required when the harness runs.</summary>
        const string EnvResultPath = "GODOT_MCP_HARNESS_RESULT";

        /// <summary>When "0", a missing connection does NOT fail the harness (local capture-only smoke). Default "1".</summary>
        const string EnvRequireConnect = "GODOT_MCP_HARNESS_REQUIRE_CONNECT";

        /// <summary>Overall wall-clock budget (seconds) for the connect+capture phase before giving up. Default 60.</summary>
        const string EnvTimeoutSeconds = "GODOT_MCP_HARNESS_TIMEOUT_SECONDS";

        /// <summary>
        /// Explicit per-leg expectation, set by the CI workflow from the matrix Godot version: "1" when the
        /// engine 4.5+ logger channel should be present (Godot 4.5+), "0" when it must be absent (4.3 / 4.4 —
        /// graceful stub degradation). When UNSET the harness falls back to inferring it from the running
        /// editor version (major==4 && minor>=5). The explicit env is the authoritative signal because the
        /// REAL determinant is the build's Godot.NET.Sdk version (which defines GODOT4_5_OR_GREATER), and the
        /// workflow pins the build SDK to the matrix version — so the editor version and this flag always
        /// agree in CI, but the env makes the contract unambiguous and independently testable.
        /// </summary>
        const string EnvEngineLoggerExpected = "GODOT_MCP_HARNESS_ENGINE_LOGGER_EXPECTED";

        // --- State ------------------------------------------------------------------------------------

        GodotMcpRuntimeHandle? _handle;
        bool _started;

        public override void _Ready()
        {
            // Default OFF: a normal editor/game load must be untouched. Only the explicit CI flag runs it.
            if (OS.GetEnvironment(EnvHarnessEnabled) != "1")
                return;

            if (_started)
                return;
            _started = true;

            // Run the async harness fire-and-forget; it owns its own SceneTree.Quit() with an exit code.
            _ = RunAsync();
        }

        async Task RunAsync()
        {
            var resultPath = OS.GetEnvironment(EnvResultPath);
            var requireConnect = OS.GetEnvironment(EnvRequireConnect) != "0"; // default: require
            var timeoutSeconds = ParseIntOr(OS.GetEnvironment(EnvTimeoutSeconds), 60);

            var report = new HarnessReport
            {
                GodotVersion = (string)Engine.GetVersionInfo()["string"],
                EngineLoggerExpected = ResolveEngineLoggerExpected(),
                RequireConnect = requireConnect,
            };

            GD.Print($"[harness] starting (godot={report.GodotVersion}, engineLoggerExpected={report.EngineLoggerExpected}, requireConnect={requireConnect}, timeout={timeoutSeconds}s).");

            // ONE shared wall-clock deadline for the WHOLE connect+capture phase. Both PollConnectedAsync
            // and PollCapturedAsync poll against this same instant — the capture phase inherits whatever is
            // LEFT after connect, it does NOT get a fresh full budget. (If each got its own
            // TimeSpan.FromSeconds(timeoutSeconds) the harness could run up to ~2x the intended budget and
            // outlive the workflow's wait-on-game window — issue #196.)
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);

            try
            {
                // 1) Build the in-game runtime: opt the addon's tools in (so the server's tool list — incl.
                //    ping + runtime-errors-get — is non-empty after connect) and enable runtime error capture
                //    (installs the engine 4.5+ logger + the C# AppDomain/TaskScheduler hooks). Host/mode/auth
                //    come from the addon's own GODOT_MCP_* env contract the CI step sets, so we do NOT hard-code
                //    them here — a WithConfig is unnecessary because GodotMcpConfig reads the env live.
                _handle = GodotMcpRuntime.Initialize(b =>
                {
                    b.WithToolsFromAssembly(typeof(Tool_Ping).Assembly);
                    b.WithRuntimeErrorCapture();
                }).Build();

                report.CaptureInstalled = RuntimeErrorCapture.IsInstalled;
                GD.Print($"[harness] runtime built. captureInstalled={report.CaptureInstalled}.");

                // 2) Let the deferred main-thread dispatcher AddChild land (GodotMcpRuntime schedules it via
                //    CallDeferred on the tree root), so tool handlers can marshal before we connect.
                await WaitFramesAsync(3);

                // 3) Connect to the local server (explicit — nothing auto-connects). KeepConnected drives the
                //    client's own reconnect loop; Connect() returns true on the FIRST successful connect.
                bool initialConnect = false;
                try
                {
                    initialConnect = await _handle.Connect();
                }
                catch (Exception ex)
                {
                    report.ConnectError = ex.Message;
                    GD.PushWarning($"[harness] Connect() threw: {ex.Message}");
                }
                report.InitialConnectReturned = initialConnect;

                // 4) Poll for the live SignalR state to reach Connected (robust — NOT a fixed sleep). The
                //    reused client may report Connected slightly after Connect() returns; poll the plugin's
                //    ConnectionState reactive property until Connected or the shared deadline expires. In
                //    capture-only mode (requireConnect=false) we do NOT burn the shared budget waiting for a
                //    connection we don't need — that would starve the capture phase of the budget it relies
                //    on to drive + re-seed the C# escalation (issue #196). We record the current state and
                //    move on, leaving the whole budget for PollCapturedAsync.
                report.Connected = await PollConnectedAsync(requireConnect, deadline);
                GD.Print($"[harness] connect phase done. initialConnect={initialConnect}, connected={report.Connected}.");

                // 5) Raise the three representative runtime errors.
                RaiseErrors(report);

                // 6) Poll the collector until all the expected rows have landed (capture is multi-threaded;
                //    the engine logger + AppDomain/TaskScheduler hooks append from arbitrary threads). Robust
                //    wait, capped by the SAME shared deadline (the remaining budget after connect, not a fresh
                //    full one). This loop also DRIVES the C# unobserved-Task finalizer escalation + re-seeds.
                await PollCapturedAsync(report, deadline);

                // 7) Snapshot the captured rows into the report.
                Snapshot(report);
            }
            catch (Exception ex)
            {
                report.FatalError = ex.ToString();
                GD.PushError($"[harness] FATAL: {ex}");
            }
            finally
            {
                try { _handle?.Disconnect().Wait(TimeSpan.FromSeconds(5)); } catch { /* best-effort */ }
            }

            // 8) Decide pass/fail and write the result.
            var ok = Evaluate(report);
            report.Ok = ok;
            WriteResult(resultPath, report);

            GD.Print($"[harness] DONE ok={ok}. summary: connected={report.Connected} engineErr={report.HasEngineRuntimeError} engineFrames={report.EngineRuntimeErrorFrameCount} pushErr={report.HasPushError} pushWarn={report.HasPushWarning} csharpEx={report.HasCSharpException}.");

            // Dispose the handle (uninstalls capture) before quitting.
            try { _handle?.Dispose(); } catch { /* best-effort */ }

            GetTree().Quit(ok ? 0 : 1);
        }

        // --- Error generation -------------------------------------------------------------------------

        void RaiseErrors(HarnessReport report)
        {
            // (a) A genuine GDScript RUNTIME error with a deep multi-frame backtrace (#163). Load the harness
            //     .gd, instantiate it, and call its entry point — the innermost frame faults at runtime. On
            //     Godot 4.5+ the engine logger captures it WITH frames; on < 4.5 the channel is absent.
            try
            {
                var script = GD.Load<GDScript>("res://Harness/Faulty.gd");
                if (script != null)
                {
                    var obj = (GodotObject)script.New();
                    // A real runtime fault: nonexistent-method call deep in a call chain. Swallow any managed
                    // surfacing — the point is the ENGINE error stream captures it, not that WE observe it.
                    try { obj.Call("raise_runtime_fault"); }
                    catch (Exception ex) { GD.Print($"[harness] gdscript fault surfaced to C# (expected-ish): {ex.Message}"); }
                    report.RaisedGdScriptFault = true;
                }
                else
                {
                    GD.PushWarning("[harness] could not load res://Harness/Faulty.gd — GDScript fault not raised.");
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[harness] raising the GDScript fault failed: {ex.Message}");
            }

            // (b) push_error + push_warning — captured by the engine logger on 4.5+ (absent on < 4.5).
            GD.PushError("[harness] deliberate push_error for runtime-error capture (issue #186).");
            GD.PushWarning("[harness] deliberate push_warning for runtime-error capture (issue #186).");
            report.RaisedPushDiagnostics = true;

            // (c) A C# exception via TaskScheduler.UnobservedTaskException: start a Task that throws, drop the
            //     reference without awaiting, then force GC + finalizers so the unobserved-exception event
            //     fires (that is exactly when the runtime raises it). Pure-managed channel — works on EVERY
            //     Godot version, including < 4.5.
            RaiseUnobservedTaskException();
            report.RaisedCSharpException = true;
        }

        static void RaiseUnobservedTaskException()
        {
            // Create + abandon a faulted Task inside a non-inlined local so the JIT cannot keep it rooted.
            FaultAndAbandon();
            // SEED ONLY: one immediate fast GC pass to try to fire the finalizer escalation now. The OLD
            // fixed 4-pass loop is intentionally gone — a fixed pass count is exactly the GC-timing race
            // that intermittently failed runtime-harness-4-4 (slow/contended runner => 4 passes not enough,
            // and nothing re-drove it). The budget-bounded GC-drive + re-seed now lives in PollCapturedAsync,
            // which retries until the row lands or the SHARED wall-clock deadline expires. A future reader
            // MUST NOT restore the fixed loop here (issue #196).
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void FaultAndAbandon()
        {
            // Task.Run schedules work that throws; we never await/observe it, so its exception goes unobserved.
            _ = Task.Run(() =>
                throw new InvalidOperationException(
                    "[harness] deliberate unobserved Task exception for runtime-error capture (issue #186)."));
        }

        // --- Robust polling (no blind sleeps) ---------------------------------------------------------

        async Task<bool> PollConnectedAsync(bool requireConnect, DateTime deadline)
        {
            var plugin = _handle?.Plugin;
            if (plugin == null)
                return false;

            // Capture-only smoke (requireConnect=false): do NOT spend the shared budget waiting for a
            // connection we don't require — snapshot the current state and return immediately so the WHOLE
            // budget is left for PollCapturedAsync to drive the C# escalation. When connect IS required (CI),
            // poll up to the shared deadline as before; in CI the local server is already up, so this returns
            // fast and the capture phase still inherits the bulk of the budget.
            if (!requireConnect)
                return plugin.ConnectionState.CurrentValue == HubConnectionState.Connected;

            while (DateTime.UtcNow < deadline)
            {
                if (plugin.ConnectionState.CurrentValue == HubConnectionState.Connected)
                    return true;
                await WaitFramesAsync(5);
            }
            return plugin.ConnectionState.CurrentValue == HubConnectionState.Connected;
        }

        // Polls the collector until every expected row has landed OR the SHARED deadline expires. Unlike the
        // old version (which only frame-waited), this DRIVES the C# unobserved-Task finalizer escalation each
        // iteration while the C# row is still missing, and RE-SEEDS the faulted Task on a ~2s backoff so a
        // first fault that was swallowed (slow/contended runner — the runtime-harness-4-4 GC-timing flake of
        // issue #196) self-heals within the budget. The GC-drive + re-seed are gated on `!haveCSharp`, so the
        // engine-row wait on Godot 4.5+ is NOT regressed (no extra GC churn once the C# row is present).
        //
        // NOTE — scope: only the C# unobserved-Task escalation is timing-fragile (it depends on a finalizer
        // GC pass). The engine GDScript runtime error / push_error / push_warning rows are pushed
        // synchronously by the engine 4.5+ logger channel and need no re-raise here; re-raising them is
        // intentionally OUT OF SCOPE for this de-flake (issue #196).
        async Task PollCapturedAsync(HarnessReport report, DateTime deadline)
        {
            var lastReseed = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                var collector = RuntimeErrorCollector.Current;
                if (collector != null)
                {
                    var rows = collector.QuerySince(0, RuntimeErrorCollector.Capacity, out _);
                    // The C# unobserved-Task exception is the version-independent guaranteed row; once it AND
                    // the push diagnostics are present we have everything the < 4.5 path can produce. On 4.5+
                    // we additionally wait for the engine GDScript error with frames.
                    var haveCSharp = rows.Any(IsCSharpException);
                    var havePushErr = rows.Any(r => IsEnginePushError(r));
                    var havePushWarn = rows.Any(r => IsEnginePushWarning(r));
                    var haveEngineRuntime = rows.Any(IsEngineScriptRuntimeError);

                    var enginePartDone = !report.EngineLoggerExpected || (haveEngineRuntime && havePushErr && havePushWarn);
                    if (haveCSharp && enginePartDone)
                        return;

                    // C# row still missing — actively drive the finalizer escalation (budget-bounded
                    // replacement for the removed fixed 4-pass loop in RaiseUnobservedTaskException), and
                    // re-seed the faulted Task on a ~2s backoff so a swallowed first fault self-heals.
                    if (!haveCSharp)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        if (DateTime.UtcNow - lastReseed > TimeSpan.FromSeconds(2))
                        {
                            FaultAndAbandon();
                            lastReseed = DateTime.UtcNow;
                        }
                    }
                }
                await WaitFramesAsync(5);
            }
        }

        async Task WaitFramesAsync(int frames)
        {
            for (var i = 0; i < frames; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // --- Snapshot + classification ----------------------------------------------------------------

        void Snapshot(HarnessReport report)
        {
            var collector = RuntimeErrorCollector.Current;
            report.CaptureAvailable = collector != null;
            if (collector == null)
                return;

            var rows = collector.QuerySince(0, RuntimeErrorCollector.Capacity, out _);
            report.TotalCaptured = rows.Length;

            var engineRuntime = rows.FirstOrDefault(IsEngineScriptRuntimeError);
            if (engineRuntime != null)
            {
                report.HasEngineRuntimeError = true;
                report.EngineRuntimeErrorMessage = engineRuntime.Message;
                report.EngineRuntimeErrorType = engineRuntime.Type;
                report.EngineRuntimeErrorFile = engineRuntime.File;
                report.EngineRuntimeErrorFunction = engineRuntime.Function;
                report.EngineRuntimeErrorFrameCount = engineRuntime.Frames?.Count ?? 0;
                report.EngineRuntimeErrorHasStackTrace = !string.IsNullOrEmpty(engineRuntime.StackTrace);
                report.EngineRuntimeErrorFrameFunctions = engineRuntime.Frames?
                    .Select(f => f.Function).Where(f => !string.IsNullOrEmpty(f)).ToList() ?? new List<string>();
            }

            report.HasPushError = rows.Any(IsEnginePushError);
            report.HasPushWarning = rows.Any(IsEnginePushWarning);

            var csharp = rows.FirstOrDefault(IsCSharpException);
            if (csharp != null)
            {
                report.HasCSharpException = true;
                report.CSharpExceptionType = csharp.Type;
                report.CSharpExceptionHasStackTrace = !string.IsNullOrEmpty(csharp.StackTrace);
            }
        }

        // A GDScript RUNTIME error is an Engine row of kind Script (or generic Error) that carries an engine
        // origin and — on 4.5+ — frames. We additionally require it not to be the push_error/push_warning we
        // raised (those are matched separately by their marker text).
        static bool IsEngineScriptRuntimeError(RuntimeError r) =>
            r.Source == RuntimeErrorSource.Engine
            && (string.Equals(r.Type, nameof(EngineErrorKind.Script), StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Type, nameof(EngineErrorKind.Error), StringComparison.OrdinalIgnoreCase))
            && !r.Message.Contains("deliberate push_error", StringComparison.OrdinalIgnoreCase)
            && !r.Message.Contains("deliberate push_warning", StringComparison.OrdinalIgnoreCase);

        static bool IsEnginePushError(RuntimeError r) =>
            r.Source == RuntimeErrorSource.Engine
            && r.Message.Contains("deliberate push_error", StringComparison.OrdinalIgnoreCase);

        static bool IsEnginePushWarning(RuntimeError r) =>
            r.Source == RuntimeErrorSource.Engine
            && r.Message.Contains("deliberate push_warning", StringComparison.OrdinalIgnoreCase);

        static bool IsCSharpException(RuntimeError r) =>
            r.Source == RuntimeErrorSource.UnobservedTaskException
            || r.Source == RuntimeErrorSource.UnhandledException;

        // --- Pass/fail evaluation ---------------------------------------------------------------------

        bool Evaluate(HarnessReport report)
        {
            if (!string.IsNullOrEmpty(report.FatalError))
                return false;

            // Capture must have installed and be available regardless of version.
            if (!report.CaptureInstalled || !report.CaptureAvailable)
                return false;

            // The C# unobserved-Task exception is the version-independent guaranteed row.
            if (!report.HasCSharpException || !report.CSharpExceptionHasStackTrace)
                return false;

            // Connection: required unless explicitly waived (local capture-only smoke).
            if (report.RequireConnect && !report.Connected)
                return false;

            if (report.EngineLoggerExpected)
            {
                // Godot 4.5+: the engine GDScript runtime error must be captured WITH a multi-frame backtrace
                // (#163), and the push_error/push_warning must be captured.
                if (!report.HasEngineRuntimeError) return false;
                if (report.EngineRuntimeErrorFrameCount < 2) return false; // deep chain => >= 2 frames
                if (!report.HasPushError) return false;
                if (!report.HasPushWarning) return false;
            }
            else
            {
                // Godot < 4.5: the engine logger channel is ABSENT (graceful stub degradation). The engine
                // rows must NOT appear and no engine frames can exist. The C# channel (asserted above) is the
                // only one that captures. This is the per-version assertion the issue calls for.
                if (report.HasEngineRuntimeError) return false;
                if (report.HasPushError || report.HasPushWarning) return false;
                if (report.EngineRuntimeErrorFrameCount != 0) return false;
            }

            return true;
        }

        // --- Result serialization ---------------------------------------------------------------------

        void WriteResult(string resultPath, HarnessReport report)
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

            // Always echo to stdout so the CI log carries it even if the file write fails.
            GD.Print("[harness-result-begin]");
            GD.Print(json);
            GD.Print("[harness-result-end]");

            if (string.IsNullOrEmpty(resultPath))
            {
                GD.PushWarning($"[harness] {EnvResultPath} not set — result not written to a file (stdout only).");
                return;
            }

            try
            {
                System.IO.File.WriteAllText(resultPath, json);
                GD.Print($"[harness] result written to {resultPath}.");
            }
            catch (Exception ex)
            {
                GD.PushError($"[harness] failed to write result to {resultPath}: {ex.Message}");
            }
        }

        static int ParseIntOr(string? raw, int fallback) =>
            int.TryParse(raw, out var v) && v > 0 ? v : fallback;

        /// <summary>
        /// Whether the engine 4.5+ logger channel is expected for THIS leg: the explicit
        /// <see cref="EnvEngineLoggerExpected"/> env ("1"/"0") when set, else inferred from the running
        /// editor version (major==4 &amp;&amp; minor&gt;=5). See the env's doc for why the explicit signal is preferred.
        /// </summary>
        static bool ResolveEngineLoggerExpected()
        {
            var raw = OS.GetEnvironment(EnvEngineLoggerExpected);
            if (raw == "1") return true;
            if (raw == "0") return false;

            var info = Engine.GetVersionInfo();
            return info["major"].AsInt32() == 4 && info["minor"].AsInt32() >= 5;
        }

        /// <summary>
        /// The structured harness outcome — serialized to the result file the CI asserter parses, and echoed
        /// to stdout. Field names are camelCased in JSON to match the addon's data-model convention.
        /// </summary>
        sealed class HarnessReport
        {
            [JsonPropertyName("ok")] public bool Ok { get; set; }
            [JsonPropertyName("godotVersion")] public string GodotVersion { get; set; } = string.Empty;
            [JsonPropertyName("engineLoggerExpected")] public bool EngineLoggerExpected { get; set; }
            [JsonPropertyName("requireConnect")] public bool RequireConnect { get; set; }

            [JsonPropertyName("captureInstalled")] public bool CaptureInstalled { get; set; }
            [JsonPropertyName("captureAvailable")] public bool CaptureAvailable { get; set; }

            [JsonPropertyName("initialConnectReturned")] public bool InitialConnectReturned { get; set; }
            [JsonPropertyName("connected")] public bool Connected { get; set; }
            [JsonPropertyName("connectError")] public string? ConnectError { get; set; }

            [JsonPropertyName("raisedGdScriptFault")] public bool RaisedGdScriptFault { get; set; }
            [JsonPropertyName("raisedPushDiagnostics")] public bool RaisedPushDiagnostics { get; set; }
            [JsonPropertyName("raisedCSharpException")] public bool RaisedCSharpException { get; set; }

            [JsonPropertyName("totalCaptured")] public int TotalCaptured { get; set; }

            [JsonPropertyName("hasEngineRuntimeError")] public bool HasEngineRuntimeError { get; set; }
            [JsonPropertyName("engineRuntimeErrorMessage")] public string? EngineRuntimeErrorMessage { get; set; }
            [JsonPropertyName("engineRuntimeErrorType")] public string? EngineRuntimeErrorType { get; set; }
            [JsonPropertyName("engineRuntimeErrorFile")] public string? EngineRuntimeErrorFile { get; set; }
            [JsonPropertyName("engineRuntimeErrorFunction")] public string? EngineRuntimeErrorFunction { get; set; }
            [JsonPropertyName("engineRuntimeErrorFrameCount")] public int EngineRuntimeErrorFrameCount { get; set; }
            [JsonPropertyName("engineRuntimeErrorHasStackTrace")] public bool EngineRuntimeErrorHasStackTrace { get; set; }
            [JsonPropertyName("engineRuntimeErrorFrameFunctions")] public List<string> EngineRuntimeErrorFrameFunctions { get; set; } = new();

            [JsonPropertyName("hasPushError")] public bool HasPushError { get; set; }
            [JsonPropertyName("hasPushWarning")] public bool HasPushWarning { get; set; }

            [JsonPropertyName("hasCSharpException")] public bool HasCSharpException { get; set; }
            [JsonPropertyName("cSharpExceptionType")] public string? CSharpExceptionType { get; set; }
            [JsonPropertyName("cSharpExceptionHasStackTrace")] public bool CSharpExceptionHasStackTrace { get; set; }

            [JsonPropertyName("fatalError")] public string? FatalError { get; set; }
        }
    }
}
