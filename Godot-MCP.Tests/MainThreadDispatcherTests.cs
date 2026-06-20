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
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the early-boot buffering contract of <see cref="MainThreadDispatcher"/> (issue #169): a
    /// main-thread marshal issued in the window between <c>GodotMcpRuntime.Build()</c> returning and the
    /// deferred <c>AddChild</c> firing — i.e. before ANY dispatcher instance has entered the tree — must
    /// NOT throw; it is buffered and drained, FIFO, the moment the first dispatcher arrives. The
    /// post-teardown state (dispatcher booted then removed) still fails fast so a pending awaiter cannot
    /// hang on a queue nothing will drain.
    ///
    /// <para>
    /// These tests are <b>pure-managed</b> and never construct a <see cref="Godot.Node"/>: a real
    /// <see cref="MainThreadDispatcher"/> is a Godot Node whose construction P/Invokes into
    /// <c>godotsharp_*</c> and faults the binary-less xUnit host with <c>AccessViolationException</c>. The
    /// buffer/drain lifecycle lives entirely in the dispatcher's static members, so the boot/teardown edges
    /// are modelled via the type's internal <c>*ForTests</c> seams. The live Node lifecycle
    /// (<c>_EnterTree</c>/<c>_Process</c>/<c>_ExitTree</c> wiring) is covered by the headless Godot smoke
    /// (Suite 3), not here.
    /// </para>
    ///
    /// <para>
    /// <see cref="MainThreadDispatcher"/>'s lifecycle state is <c>static</c> and outlives a single test, so
    /// each test resets it via the ctor (<see cref="ResetState"/>) and <see cref="Dispose"/>. The
    /// <c>[Collection]</c> attribute serializes these tests against each other (and disables cross-class
    /// parallelism with any future test that touches the same statics) so a concurrent test never observes
    /// mid-mutation state.
    /// </para>
    /// </summary>
    [Collection(nameof(MainThreadDispatcherTests))]
    public class MainThreadDispatcherTests : IDisposable
    {
        public MainThreadDispatcherTests() => ResetState();

        public void Dispose() => ResetState();

        static void ResetState() => MainThreadDispatcher.ResetForTests();

        // ---- Early-boot buffering (the issue #169 fix) -----------------------------------------------------

        [Fact]
        public void Enqueue_BeforeAnyInstance_DoesNotThrow_AndBuffers()
        {
            // The exact #169 race: a tool handler marshals to the main thread before the deferred dispatcher
            // Node has entered the tree. Old behavior threw InvalidOperationException; new behavior buffers.
            var ex = Record.Exception(() => MainThreadDispatcher.Enqueue(() => { }));

            Assert.Null(ex);
            Assert.Equal(1, MainThreadDispatcher.PendingActionCountForTests);
            Assert.False(MainThreadDispatcher.HasEverEnteredForTests);
        }

        [Fact]
        public void Enqueue_BeforeInstance_RunsOnce_WhenInstanceArrives()
        {
            var runCount = 0;
            MainThreadDispatcher.Enqueue(() => runCount++);

            // Not run yet — nothing is in the tree to drain it.
            Assert.Equal(0, runCount);

            // First dispatcher enters the tree → the buffered action drains exactly once.
            MainThreadDispatcher.SimulateInstanceEnteredForTests();

            Assert.Equal(1, runCount);
            Assert.Equal(0, MainThreadDispatcher.PendingActionCountForTests);
        }

        [Fact]
        public void Enqueue_BeforeInstance_PreservesFifoOrdering_OnDrain()
        {
            var order = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                var captured = i;
                MainThreadDispatcher.Enqueue(() => order.Add(captured));
            }

            Assert.Empty(order); // still buffered

            MainThreadDispatcher.SimulateInstanceEnteredForTests();

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, order);
        }

        [Fact]
        public void Enqueue_BeforeInstance_DoesNotDoubleRun_AcrossBufferToInstanceHandoff()
        {
            var runCount = 0;
            MainThreadDispatcher.Enqueue(() => runCount++);

            // Instance arrives and drains the buffered action (count → 1).
            MainThreadDispatcher.SimulateInstanceEnteredForTests();
            Assert.Equal(1, runCount);

            // A subsequent drain (e.g. the next _Process tick) must NOT replay it.
            MainThreadDispatcher.DrainForTests();
            Assert.Equal(1, runCount);
        }

        [Fact]
        public void EnqueueOffThread_BeforeInstance_Buffers_ThenRunsOnDrain()
        {
            // Enqueue is contractually thread-safe; the #169 callers marshal FROM background threads. Prove an
            // off-main-thread enqueue before boot buffers without throwing, then drains when the instance lands.
            var ran = false;
            Exception? offThreadEx = null;

            var t = new Thread(() =>
            {
                try { MainThreadDispatcher.Enqueue(() => ran = true); }
                catch (Exception e) { offThreadEx = e; }
            });
            t.Start();
            t.Join();

            Assert.Null(offThreadEx);
            Assert.Equal(1, MainThreadDispatcher.PendingActionCountForTests);
            Assert.False(ran);

            MainThreadDispatcher.SimulateInstanceEnteredForTests();
            Assert.True(ran);
        }

        // ---- Normal post-ready dispatch (no regression) ----------------------------------------------------

        [Fact]
        public void Enqueue_WhileInstancePresent_BuffersForNextDrain()
        {
            MainThreadDispatcher.SimulateInstanceEnteredForTests(); // instance present, nothing buffered

            var runCount = 0;
            MainThreadDispatcher.Enqueue(() => runCount++);

            // Queued for the next tick, not run inline (matches the per-_Process-tick drain contract).
            Assert.Equal(0, runCount);
            Assert.Equal(1, MainThreadDispatcher.PendingActionCountForTests);

            MainThreadDispatcher.DrainForTests();
            Assert.Equal(1, runCount);
        }

        // ---- Post-teardown fail-fast (preserve the awaiter-cannot-hang guarantee) --------------------------

        [Fact]
        public void Enqueue_AfterTeardown_ThrowsInvalidOperation()
        {
            // Boot then tear down: now no instance AND it has booted before → the queue would never drain, so
            // a pending awaiter would hang. Enqueue must fail fast exactly as it did before the #169 change.
            MainThreadDispatcher.SimulateInstanceEnteredForTests();
            MainThreadDispatcher.SimulateInstanceExitedForTests();

            Assert.True(MainThreadDispatcher.HasEverEnteredForTests);
            Assert.Throws<InvalidOperationException>(() => MainThreadDispatcher.Enqueue(() => { }));
        }

        [Fact]
        public void Enqueue_BeforeBoot_Buffers_But_AfterTeardown_Throws()
        {
            // The disambiguation in one test: identical "no instance present" surface, opposite behavior,
            // decided solely by whether a dispatcher has ever entered the tree.
            var preBootEx = Record.Exception(() => MainThreadDispatcher.Enqueue(() => { }));
            Assert.Null(preBootEx); // pre-boot → buffered

            MainThreadDispatcher.SimulateInstanceEnteredForTests();   // drains the buffered action
            MainThreadDispatcher.SimulateInstanceExitedForTests();    // teardown

            Assert.Throws<InvalidOperationException>(() => MainThreadDispatcher.Enqueue(() => { }));
        }

        [Fact]
        public void Enqueue_NullAction_ThrowsArgumentNull_RegardlessOfLifecycle()
        {
            // Before boot.
            Assert.Throws<ArgumentNullException>(() => MainThreadDispatcher.Enqueue(null!));

            // After boot.
            MainThreadDispatcher.SimulateInstanceEnteredForTests();
            Assert.Throws<ArgumentNullException>(() => MainThreadDispatcher.Enqueue(null!));
        }

        // ---- End-to-end via GodotMainThread (the actual #169 caller path) ----------------------------------

        [Fact]
        public async Task GodotMainThread_RunAsync_BeforeInstance_DoesNotThrowSynchronously_AndCompletesOnDrain()
        {
            // GodotMainThread.Dispatch is the unguarded caller that the #169 throw bit: ReflectorNet's
            // MainThread.Instance.Run(...) from a tool handler. Off the main thread, with no instance yet, it
            // must return a pending task (not throw), and that task must complete once a dispatcher arrives.
            GodotMainThread.Install();
            var mt = com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance;
            Assert.IsType<GodotMainThread>(mt);

            // Run the dispatch from a background thread so IsMainThread is false and it takes the Dispatch path
            // (the main-thread captured by SimulateInstanceEntered is THIS test thread).
            Task<int>? task = null;
            Exception? syncEx = null;
            var t = new Thread(() =>
            {
                try { task = mt.RunAsync(() => 42); }
                catch (Exception e) { syncEx = e; }
            });
            t.Start();
            t.Join();

            Assert.Null(syncEx);            // did NOT throw synchronously (the #169 fix)
            Assert.NotNull(task);
            Assert.False(task!.IsCompleted); // pending — nothing has drained it yet

            MainThreadDispatcher.SimulateInstanceEnteredForTests(); // drains the buffered body

            Assert.True(task.IsCompletedSuccessfully);
            Assert.Equal(42, await task); // already completed — await just unwraps it without blocking
        }
    }
}
