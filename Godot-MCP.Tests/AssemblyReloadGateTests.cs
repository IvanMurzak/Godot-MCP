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
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.Godot.MCP.Reload;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The tool-call gate around Godot's C# assembly reload: a call in flight when Godot unloads the assembly kept it
    /// alive ("Failed to unload assemblies … Giving up", plugin offline until the editor restarts).
    /// </summary>
    public class AssemblyReloadGateTests
    {
        static readonly DateTime Loaded = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        static AssemblyReloadGate GateLoadedAt(DateTime loaded)
        {
            var gate = new AssemblyReloadGate();
            gate.SetBaseline(loaded);
            return gate;
        }

        [Fact]
        public void Open_AdmitsAndCountsCalls()
        {
            var gate = GateLoadedAt(Loaded);

            Assert.True(gate.TryEnter(out var refusal));
            Assert.Null(refusal);
            Assert.True(gate.TryEnter(out _));
            Assert.Equal(2, gate.InFlight);

            gate.Exit();
            gate.Exit();
            Assert.Equal(0, gate.InFlight);
        }

        [Fact]
        public void RebuiltDll_TurnsPending_AndRefusesNewCallsWithARetryableMessage()
        {
            var gate = GateLoadedAt(Loaded);
            Assert.True(gate.TryEnter(out _)); // admitted before the rebuild

            Assert.Equal(AssemblyReloadGate.Observation.BecamePending, gate.Observe(Loaded.AddSeconds(3), nowMs: 1_000));

            Assert.Equal(AssemblyReloadGate.GatePhase.Pending, gate.Phase);
            Assert.False(gate.TryEnter(out var refusal));
            Assert.Equal(AssemblyReloadGate.PendingMessage, refusal);
            Assert.Contains("Retry", refusal);
            Assert.Equal(1, gate.InFlight); // the refused call is not counted; the admitted one still runs
        }

        [Fact]
        public void SameSecondRewrite_IsNotAReload()
        {
            // Godot compares whole seconds: a DLL rewritten within the second it was loaded is never reloaded.
            var gate = GateLoadedAt(Loaded.AddMilliseconds(100));

            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(Loaded.AddMilliseconds(900), nowMs: 0));
            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(null, nowMs: 0));
            Assert.Equal(AssemblyReloadGate.GatePhase.Open, gate.Phase);
        }

        [Fact]
        public void UnknownBaseline_IsTakenFromTheFirstObservation()
        {
            var gate = new AssemblyReloadGate();

            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(Loaded, nowMs: 0));
            Assert.Equal(AssemblyReloadGate.Observation.BecamePending, gate.Observe(Loaded.AddSeconds(1), nowMs: 10));
        }

        [Fact]
        public void HoldReload_WhileAdmittedCallsRun_BoundedByMaxHold()
        {
            var gate = GateLoadedAt(Loaded);
            Assert.False(gate.HoldReload(0)); // open: nothing to hold

            gate.TryEnter(out _);
            gate.Observe(Loaded.AddSeconds(1), nowMs: 1_000);

            Assert.True(gate.HoldReload(1_000));
            Assert.True(gate.HoldReload(1_000 + AssemblyReloadGate.MaxHoldMs - 1));
            Assert.False(gate.HoldReload(1_000 + AssemblyReloadGate.MaxHoldMs)); // never defer Godot's reload for good

            gate.Exit();
            Assert.False(gate.HoldReload(1_100)); // drained: let Godot reload now
        }

        [Fact]
        public void PendingWithoutAReload_ReopensAfterTheTimeout_OnTheNewDll()
        {
            var gate = GateLoadedAt(Loaded);
            var rebuilt = Loaded.AddSeconds(2);
            gate.Observe(rebuilt, nowMs: 0);

            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(rebuilt, AssemblyReloadGate.PendingTimeoutMs - 1));
            Assert.Equal(AssemblyReloadGate.Observation.PendingExpired, gate.Observe(rebuilt, AssemblyReloadGate.PendingTimeoutMs));

            Assert.Equal(AssemblyReloadGate.GatePhase.Open, gate.Phase);
            Assert.True(gate.TryEnter(out _));
            // The DLL Godot did not reload is the new baseline: it does not turn the gate pending again.
            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(rebuilt, AssemblyReloadGate.PendingTimeoutMs + 100));
        }

        [Fact]
        public void Unloading_RefusesEverything_AndIsNeverReopened()
        {
            var gate = GateLoadedAt(Loaded);
            gate.BeginUnload();

            Assert.False(gate.TryEnter(out var refusal));
            Assert.Equal(AssemblyReloadGate.UnloadingMessage, refusal);
            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(Loaded.AddSeconds(5), nowMs: 0));
            Assert.Equal(AssemblyReloadGate.Observation.None, gate.Observe(Loaded.AddSeconds(5), nowMs: AssemblyReloadGate.PendingTimeoutMs * 2));
            Assert.Equal(AssemblyReloadGate.GatePhase.Unloading, gate.Phase);
        }

        [Fact]
        public void Unloading_UsesTheGivenMessage()
        {
            var gate = GateLoadedAt(Loaded);
            gate.BeginUnload(AssemblyReloadGate.ShutdownMessage);

            Assert.Equal(AssemblyReloadGate.ShutdownMessage, gate.Refusal);
        }

        [Fact]
        public void Drained_RunsWhenTheLastCallLeavesAnUnloadingGate_Only()
        {
            var gate = GateLoadedAt(Loaded);
            var drained = 0;
            gate.Drained = () => drained++;

            gate.TryEnter(out _);
            gate.Exit();
            Assert.Equal(0, drained); // open gate: nothing to clean up

            gate.TryEnter(out _);
            gate.TryEnter(out _);
            gate.BeginUnload();
            gate.Exit();
            Assert.Equal(0, drained); // one still running
            gate.Exit();
            Assert.Equal(1, drained);
        }

        [Fact]
        public void Drained_Throwing_DoesNotFailTheCall()
        {
            var gate = GateLoadedAt(Loaded);
            gate.Drained = () => throw new InvalidOperationException("boom");
            gate.TryEnter(out _);
            gate.BeginUnload();

            gate.Exit();
            Assert.Equal(0, gate.InFlight);
        }

        [Fact]
        public void Refusals_AreLoggedWithoutAnErrorBacktrace_OtherErrorsAreNot()
        {
            var captured = new List<(Data.GodotLogType Type, string Message)>();
            var logger = new Connection.GodotMcpLogger("McpToolManager", () => Connection.GodotMcpLogLevel.Trace, (t, m) => captured.Add((t, m)));
            Connection.GodotMcpLogger.IsExpectedError = AssemblyReloadGate.IsRefusal;
            try
            {
                Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, "Error Response to AI:\n" + AssemblyReloadGate.PendingMessage);
                Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, "Error Response to AI:\nno such file");
            }
            finally
            {
                Connection.GodotMcpLogger.IsExpectedError = null;
            }

            Assert.Equal(Data.GodotLogType.Log, captured[0].Type);
            Assert.Equal(Data.GodotLogType.Error, captured[1].Type);
            Assert.False(AssemblyReloadGate.IsRefusal(null));
            Assert.True(AssemblyReloadGate.IsRefusal("x " + AssemblyReloadGate.ShutdownMessage));
        }

        [Fact]
        public void ConcurrentCalls_AreCountedExactly()
        {
            var gate = GateLoadedAt(Loaded);
            Parallel.For(0, 1_000, i =>
            {
                if (gate.TryEnter(out _))
                    gate.Exit();
            });
            Assert.Equal(0, gate.InFlight);
        }
    }

    public class GatedRunToolTests
    {
        sealed class FakeTool : IRunTool
        {
            public FakeTool(string name) => Name = name;
            public int Calls;
            public TaskCompletionSource<ResponseCallTool>? Pending;
            public bool Enabled { get; set; } = true;
            public string Name { get; }
            public string? Title => Name + " title";
            public string? Description => Name + " description";
            public string? SkillDescription => null;
            public string? SkillBody => null;
            public JsonNode? InputSchema => new JsonObject { ["type"] = "object" };
            public JsonNode? OutputSchema => null;
            public bool? ReadOnlyHint => true;
            public bool? DestructiveHint => false;
            public bool? IdempotentHint => null;
            public bool? OpenWorldHint => null;
            public int TokenCount => 42;

            public Task<ResponseCallTool> Run(string requestId, IReadOnlyDictionary<string, JsonElement>? namedParameters, CancellationToken cancellationToken = default)
            {
                Calls++;
                return Pending?.Task ?? Task.FromResult(ResponseCallTool.Success("ran " + Name).SetRequestID(requestId));
            }
        }

        static AssemblyReloadGate OpenGate()
        {
            var gate = new AssemblyReloadGate();
            gate.SetBaseline(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            return gate;
        }

        [Fact]
        public async Task RefusedCall_IsAnUnavailableError_AndNeverReachesTheTool()
        {
            var gate = OpenGate();
            gate.BeginUnload();
            var inner = new FakeTool("script-create");

            var result = await new GatedRunTool(inner, gate).Run("req-1", null);

            Assert.Equal(0, inner.Calls);
            Assert.Equal(ResponseStatus.Error, result.Status);
            Assert.Equal(ResponseErrorKind.Unavailable, result.ErrorKind);
            Assert.Equal("req-1", result.RequestID);
            Assert.Contains(AssemblyReloadGate.UnloadingMessage, result.GetMessage());
            // Nothing ran: 503 lets the server re-send the call once the editor is back.
            Assert.Equal(503, result.HttpStatusCode);
        }

        [Fact]
        public async Task AdmittedCall_IsInFlightUntilTheToolReturns()
        {
            var gate = OpenGate();
            var inner = new FakeTool("script-create") { Pending = new TaskCompletionSource<ResponseCallTool>() };

            var run = new GatedRunTool(inner, gate).Run("req-2", null);
            Assert.Equal(1, gate.InFlight);

            inner.Pending.SetResult(ResponseCallTool.Success("done"));
            var result = await run;

            Assert.Equal(0, gate.InFlight);
            Assert.Equal(ResponseStatus.Success, result.Status);
        }

        [Fact]
        public async Task BodyRefusedDuringTheUnload_AnswersWithTheRefusalItself()
        {
            var gate = OpenGate();
            var inner = new FakeTool("script-read") { Pending = new TaskCompletionSource<ResponseCallTool>() };
            var run = new GatedRunTool(inner, gate).Run("req-5", null);

            gate.BeginUnload();
            inner.Pending.SetResult(ResponseCallTool.Error("Tool execution failed for 'Script / Read': One or more errors occurred. (" +
                                                           AssemblyReloadGate.UnloadingMessage + ")", ResponseErrorKind.Internal));
            var result = await run;

            Assert.Equal(ResponseErrorKind.Unavailable, result.ErrorKind);
            Assert.Contains(AssemblyReloadGate.UnloadingMessage, result.GetMessage());
            Assert.DoesNotContain("Tool execution failed", result.GetMessage());
            // The tool had started: no 503, so the server reports the refusal instead of re-sending a call that may have run.
            Assert.Null(result.HttpStatusCode);
        }

        [Fact]
        public async Task OrdinaryToolError_IsKept()
        {
            var gate = OpenGate();
            var inner = new FakeTool("script-read") { Pending = new TaskCompletionSource<ResponseCallTool>() };
            inner.Pending.SetResult(ResponseCallTool.Error("no such file", ResponseErrorKind.NotFound));

            var result = await new GatedRunTool(inner, gate).Run("req-6", null);

            Assert.Equal(ResponseErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task FailingTool_LeavesTheGate()
        {
            var gate = OpenGate();
            var inner = new FakeTool("boom") { Pending = new TaskCompletionSource<ResponseCallTool>() };
            inner.Pending.SetException(new InvalidOperationException("tool failed"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => new GatedRunTool(inner, gate).Run("req-3", null));
            Assert.Equal(0, gate.InFlight);
        }

        [Fact]
        public void Wrapper_ForwardsTheToolDefinition_AndEnabledState()
        {
            var inner = new FakeTool("script-read");
            var gated = new GatedRunTool(inner, OpenGate());

            Assert.Equal(inner.Name, gated.Name);
            Assert.Equal(inner.Title, gated.Title);
            Assert.Equal(inner.Description, gated.Description);
            Assert.Same(inner.InputSchema?.GetType(), gated.InputSchema?.GetType());
            Assert.Equal(inner.ReadOnlyHint, gated.ReadOnlyHint);
            Assert.Equal(inner.TokenCount, gated.TokenCount);
            Assert.Equal(McpToolType.Standard, gated.ToolType);

            gated.Enabled = false;
            Assert.False(inner.Enabled);
        }

        [Fact]
        public void WrapAll_WrapsEveryToolOnce_InTheSameOrder()
        {
            var reflector = new Reflector();
            var collection = new ToolRunnerCollection(reflector, null);
            foreach (var name in new[] { "b-tool", "a-tool", "c-tool" })
                collection[name] = new FakeTool(name);
            var tools = new McpToolManager(NullLogger<McpToolManager>.Instance, reflector, collection);
            var gate = OpenGate();

            Assert.Equal(3, GatedRunTool.WrapAll(tools, gate));
            Assert.Equal(0, GatedRunTool.WrapAll(tools, gate)); // already wrapped

            var all = tools.GetAllTools().ToList();
            Assert.Equal(new[] { "b-tool", "a-tool", "c-tool" }, all.Select(t => t.Name));
            Assert.All(all, t => Assert.IsType<FakeTool>(Assert.IsType<GatedRunTool>(t).Inner));
            Assert.Equal(0, GatedRunTool.WrapAll(null, gate));
        }

        [Fact]
        public async Task WrappedManager_RefusesCallsWhileAReloadIsPending()
        {
            var reflector = new Reflector();
            var collection = new ToolRunnerCollection(reflector, null) { ["script-create"] = new FakeTool("script-create") };
            var tools = new McpToolManager(NullLogger<McpToolManager>.Instance, reflector, collection);
            var gate = OpenGate();
            GatedRunTool.WrapAll(tools, gate);
            gate.Observe(new DateTime(2026, 1, 1, 0, 0, 5, DateTimeKind.Utc), nowMs: 0);

            var response = await tools.RunCallTool(new RequestCallTool("script-create", new Dictionary<string, JsonElement>()));

            Assert.Equal(ResponseStatus.Error, response.Value?.Status);
            Assert.Equal(ResponseErrorKind.Unavailable, response.Value?.ErrorKind);
        }
    }

    /// <summary>The dispatcher side of the gate: once the assembly unloads, queued tool bodies fail instead of running.</summary>
    [Collection(nameof(MainThreadDispatcherTests))]
    public class MainThreadDispatcherRefusalTests : IDisposable
    {
        public MainThreadDispatcherRefusalTests() => MainThreadDispatcher.ResetForTests();

        public void Dispose() => MainThreadDispatcher.ResetForTests();

        [Fact]
        public void Refusal_FailsQueuedAwaitersWithoutRunningTheirBodies_AndDropsTheRest()
        {
            var ran = new List<string>();
            Exception? refused = null;
            MainThreadDispatcher.Enqueue(() => ran.Add("awaited"), ex => refused = ex);
            MainThreadDispatcher.Enqueue(() => ran.Add("fire-and-forget"));

            MainThreadDispatcher.RefusalReason = AssemblyReloadGate.UnloadingMessage;
            MainThreadDispatcher.FlushQueued();

            Assert.Empty(ran);
            Assert.IsType<InvalidOperationException>(refused);
            Assert.Equal(AssemblyReloadGate.UnloadingMessage, refused!.Message);
            Assert.Equal(0, MainThreadDispatcher.PendingActionCountForTests);
        }

        [Fact]
        public void Refusal_RejectsNewAwaitedWork_AndDropsFireAndForgetWork()
        {
            MainThreadDispatcher.SimulateInstanceEnteredForTests();
            MainThreadDispatcher.RefusalReason = AssemblyReloadGate.UnloadingMessage;

            var ex = Assert.Throws<InvalidOperationException>(() => MainThreadDispatcher.Enqueue(() => { }, _ => { }));
            Assert.Equal(AssemblyReloadGate.UnloadingMessage, ex.Message);

            // Callers that checked "a dispatcher is live" before a fire-and-forget Enqueue must not get a throw (on a
            // thread-pool callback that would terminate the editor).
            var ran = false;
            MainThreadDispatcher.Enqueue(() => ran = true);
            MainThreadDispatcher.FlushQueued();
            Assert.False(ran);
            Assert.Equal(0, MainThreadDispatcher.PendingActionCountForTests);
        }

        [Fact]
        public void AwaitedWork_IsMarkedWhileItRuns()
        {
            bool? insideAwaited = null, insidePlain = null;
            MainThreadDispatcher.Enqueue(() => insideAwaited = MainThreadDispatcher.IsRunningAwaitedWork, _ => { });
            MainThreadDispatcher.Enqueue(() => insidePlain = MainThreadDispatcher.IsRunningAwaitedWork);

            MainThreadDispatcher.FlushQueued();

            Assert.True(insideAwaited);
            Assert.False(insidePlain);
            Assert.False(MainThreadDispatcher.IsRunningAwaitedWork);
        }

        [Fact]
        public async Task GodotMainThread_CallerBlockedOnAQueuedBody_ReturnsTheRefusal()
        {
            // The pin behind the reload failure: a tool thread blocked in MainThread.Run on a body queued for the main
            // thread. Refusing the body releases that thread with the gate's message instead of running it mid-unload.
            MainThreadDispatcher.SimulateInstanceEnteredForTests();
            var bodyRan = false;
            Func<int> body = () => { bodyRan = true; return 1; };
            Task<int>? pending = null;
            var caller = new Thread(() => pending = new GodotMainThread().RunAsync(body)); // off the "main" thread: queued
            caller.Start();
            caller.Join();
            Assert.NotNull(pending);
            Assert.Equal(1, MainThreadDispatcher.PendingActionCountForTests);

            MainThreadDispatcher.RefusalReason = AssemblyReloadGate.UnloadingMessage;
            MainThreadDispatcher.FlushQueued();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => pending!);
            Assert.Equal(AssemblyReloadGate.UnloadingMessage, ex.Message);
            Assert.False(bodyRan);
        }

        [Fact]
        public void NoRefusal_FlushRunsTheQueuedWork()
        {
            var ran = 0;
            MainThreadDispatcher.Enqueue(() => ran++, _ => { });
            MainThreadDispatcher.FlushQueued();
            Assert.Equal(1, ran);
        }
    }
}
