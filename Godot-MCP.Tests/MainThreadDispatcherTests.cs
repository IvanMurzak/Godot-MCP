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

        // ---- Bounded teardown drain (#179: terminate against re-enqueueing bodies) --------------------------

        [Fact]
        public void TeardownDrain_IsBounded_TerminatesWhenDrainedBodyReEnqueues()
        {
            // Boot, then queue an action whose body re-enqueues a fresh action every time it runs. On the
            // PRE-FIX code _ExitTree used the unbounded DrainQueue (while (TryDequeue)), so this body would
            // re-feed the loop forever and teardown would hang. The bounded teardown drain snapshots the
            // current count first, so it runs only the items present at entry and the re-enqueued item is
            // left in the queue — the drain terminates.
            MainThreadDispatcher.SimulateInstanceEnteredForTests();

            var runs = 0;
            void ReEnqueueingBody()
            {
                runs++;
                if (runs < 1_000_000) // would loop a million times under the unbounded drain; bounded runs once
                    MainThreadDispatcher.Enqueue(ReEnqueueingBody);
            }
            MainThreadDispatcher.Enqueue(ReEnqueueingBody);
            Assert.Equal(1, MainThreadDispatcher.PendingActionCountForTests);

            // Bounded teardown drain: snapshot is 1, so the body runs exactly once. It re-enqueues one item,
            // which is NOT pulled into this same pass. Termination is the assertion — no hang/no overflow.
            MainThreadDispatcher.DrainBoundedForTests();

            Assert.Equal(1, runs); // ran exactly the one snapshotted item, not the re-enqueued follow-on
            Assert.Equal(1, MainThreadDispatcher.PendingActionCountForTests); // the re-enqueue is left queued
        }

        [Fact]
        public void TeardownViaExit_DrainsPendingOnce_ThenFailsFast()
        {
            // The bounded drain through the real teardown seam (SimulateInstanceExited → DrainQueueBounded).
            // Two non-re-enqueueing bodies are pending at teardown; the bounded drain runs BOTH (the snapshot
            // is 2) and terminates, and once torn down a later Enqueue fails fast so a pending awaiter cannot
            // hang on a queue nothing will drain.
            //
            // NOTE: this test deliberately does NOT re-enqueue from a drained body. During the real teardown
            // seam _instancePresentForTests is already false, so a body that re-enqueued would take Enqueue's
            // post-teardown throw branch; that InvalidOperationException would be caught by the drain's
            // InvokeDrained and routed to GD.PushError, which P/Invokes into the (absent) Godot native binary
            // and faults the binary-less xUnit host. That throw→GD.PushError path is correct in a real editor;
            // the snapshot-and-bound TERMINATION guarantee against a re-enqueueing body is asserted separately
            // by TeardownDrain_IsBounded_TerminatesWhenDrainedBodyReEnqueues (which keeps the instance present
            // so the re-enqueue succeeds and never reaches GD.PushError).
            MainThreadDispatcher.SimulateInstanceEnteredForTests();

            var runs = 0;
            MainThreadDispatcher.Enqueue(() => runs++);
            MainThreadDispatcher.Enqueue(() => runs++);
            Assert.Equal(2, MainThreadDispatcher.PendingActionCountForTests);

            MainThreadDispatcher.SimulateInstanceExitedForTests(); // bounded teardown drain — must terminate

            Assert.Equal(2, runs);
            Assert.Equal(0, MainThreadDispatcher.PendingActionCountForTests);
            Assert.Throws<InvalidOperationException>(() => MainThreadDispatcher.Enqueue(() => { }));
        }

        // ---- Continuations don't run inline on the pump/drain thread (#179) ---------------------------------

        [Fact]
        public async Task GodotMainThread_Dispatch_ContinuationDoesNotRunInlineOnDrainThread()
        {
            // GodotMainThread.Dispatch builds its TaskCompletionSource with RunContinuationsAsynchronously, so
            // an awaiter's continuation is posted to the thread pool — NOT executed inline on whatever thread
            // completes the TCS. The completing thread here is the drain thread (DrainForTests below = the
            // _Process / _ExitTree pump in production). On the PRE-FIX default-ctor TCS, SetResult from the
            // drain thread would run the continuation synchronously ON the drain thread, which mid-teardown is
            // the SceneTree-touch / re-entrancy hazard #179 fixes.
            GodotMainThread.Install();
            var mt = com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance;

            // Dispatch from a background thread (so it is not the captured main thread and takes the Dispatch
            // → Enqueue path), leaving a pending task buffered until we drain.
            Task<int>? task = null;
            var bg = new Thread(() => { task = mt.RunAsync(() => 7); });
            bg.Start();
            bg.Join();
            Assert.NotNull(task);
            Assert.False(task!.IsCompleted);

            // Attach a continuation that records WHICH thread it runs on. ExecuteSynchronously asks the runtime
            // to run it inline on the completing thread IF the TCS allows synchronous continuations — which is
            // exactly what RunContinuationsAsynchronously forbids. So with the fix this continuation can never
            // observe the drain thread.
            var continuationThreadId = 0;
            var continuationRan = new ManualResetEventSlim(false);
            var cont = task.ContinueWith(_ =>
                {
                    continuationThreadId = Thread.CurrentThread.ManagedThreadId;
                    continuationRan.Set();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            // Drain on THIS thread — models the main-thread pump completing the buffered body.
            var drainThreadId = Thread.CurrentThread.ManagedThreadId;
            MainThreadDispatcher.DrainForTests();

            // The body completed the task, but with RunContinuationsAsynchronously the continuation was posted
            // off-thread; it must NOT have run inline during the drain on this thread.
            Assert.True(task.IsCompletedSuccessfully);
            Assert.False(continuationRan.IsSet); // not run inline on the drain thread

            // It does still run — just asynchronously, on a thread that is NOT the drain thread.
            Assert.True(continuationRan.Wait(TimeSpan.FromSeconds(5)), "continuation never ran asynchronously");
            Assert.NotEqual(drainThreadId, continuationThreadId);
            Assert.Equal(7, await task);
            await cont;
        }
    }
}
