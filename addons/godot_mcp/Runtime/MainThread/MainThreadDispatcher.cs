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
using System.Collections.Concurrent;
using System.Threading;
using Godot;

namespace com.IvanMurzak.Godot.MCP.MainThreadDispatch
{
    /// <summary>
    /// A Godot <see cref="Node"/> that drains a queue of <see cref="Action"/>s on the editor
    /// main thread, once per <see cref="Node._Process"/> tick. This is the Godot analog of
    /// Unity's <c>MainThreadDispatcher</c> (an <c>Update()</c>-pumped <c>MonoBehaviour</c>):
    /// Godot has no <c>EditorApplication.update</c> static hook, so the work is pumped from a
    /// long-lived editor Node added to the <see cref="SceneTree"/> by <c>GodotMcpPlugin</c>.
    ///
    /// Off-thread callers enqueue via <see cref="Enqueue"/>; the action runs on the next
    /// <see cref="_Process"/> tick on the main thread. <see cref="GodotMainThread"/> wraps this
    /// with a <see cref="System.Threading.Tasks.TaskCompletionSource{T}"/> so callers get the
    /// delegate's value/exception back as an awaitable, matching the ergonomics of Unity's
    /// <c>MainThread.Instance.Run</c>.
    /// </summary>
    public partial class MainThreadDispatcher : Node
    {
        /// <summary>
        /// The managed thread id captured when this dispatcher entered the tree. Godot calls
        /// <see cref="_EnterTree"/> on the engine main thread, so this is the main-thread id.
        /// Defaults to a sentinel (<c>-1</c>, which no real <see cref="Thread.ManagedThreadId"/>
        /// takes) until <see cref="_EnterTree"/> runs, so a pre-boot caller is correctly treated
        /// as off-main-thread rather than capturing a wrong id from whatever thread first touches
        /// this type.
        /// </summary>
        public static int MainThreadId { get; private set; } = -1;

        /// <summary>True when the calling thread is the captured Godot main thread.</summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;

        static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        /// <summary>
        /// The currently-installed dispatcher, or <c>null</c> when none is in the tree. <b>Marked
        /// <c>volatile</c></b> so its store/load is ordered relative to the <see cref="_hasEverEntered"/>
        /// volatile publish: <see cref="_EnterTree"/> writes <c>_instance = this</c> BEFORE
        /// <c>_hasEverEntered = true</c>, and a background-thread <see cref="Enqueue"/> reads <c>_hasEverEntered</c>
        /// then <c>_instance</c>. Without the acquire/release semantics a reader could observe a fresh
        /// <c>_hasEverEntered==true</c> against a stale <c>_instance==null</c> at the trailing boot edge and throw
        /// even though a live dispatcher is already in the tree — reintroducing the issue #169 symptom one frame
        /// later. The two volatile accesses keep the "is there an instance / has one ever entered" decision
        /// reading a coherent pair.
        /// </summary>
        static volatile MainThreadDispatcher? _instance;

        /// <summary>
        /// Test-only override of "a dispatcher is currently in the tree." Production NEVER sets this — it is
        /// always <c>false</c> there and <see cref="InstancePresent"/> falls through to the real
        /// <see cref="_instance"/>. The unit tests cannot construct a Godot <see cref="Node"/> (it faults the
        /// binary-less host), so they flip this flag via the simulate-* seams to model boot/teardown edges
        /// while keeping the throw-vs-buffer decision in <see cref="Enqueue"/> byte-for-byte identical to prod.
        /// </summary>
        static bool _instancePresentForTests;

        /// <summary>
        /// True when a dispatcher is currently installed and able to drain the queue. In production this is
        /// just "<see cref="_instance"/> is non-null"; the test-only <see cref="_instancePresentForTests"/>
        /// lets the Node-less unit tests model the same condition without a live instance.
        /// </summary>
        static bool InstancePresent => _instance != null || _instancePresentForTests;

        /// <summary>
        /// True once ANY dispatcher instance has entered the tree at least once. Combined with
        /// <see cref="_instance"/> being <c>null</c>, this disambiguates the two reasons no dispatcher is
        /// currently installed: <c>false</c> = "never booted yet" (the early-boot window — buffer and drain
        /// when one arrives), <c>true</c> = "booted then torn down" (plugin unloaded / between editor reloads —
        /// fail fast so a pending awaiter does not hang on a queue nothing will drain). Written only on the
        /// editor main thread (<see cref="_EnterTree"/>); read in the otherwise-thread-safe <see cref="Enqueue"/>.
        /// Both this flag and <see cref="_instance"/> are <c>volatile</c>, so the <see cref="_EnterTree"/> publish
        /// order (write <c>_instance</c> first, then this flag) is preserved for a concurrent reader: a fresh
        /// <c>_hasEverEntered==true</c> can never be paired with a stale <c>_instance==null</c>, so a live, just-booted
        /// dispatcher is never mistaken for a torn-down one (which would wrongly throw). The remaining boot-edge
        /// straddle is benign and resolves the same either way: it either buffers an action a live dispatcher will
        /// drain next tick, or it throws only on a genuinely torn-down dispatcher — both correct.
        /// </summary>
        static volatile bool _hasEverEntered;

        /// <summary>
        /// Per-tick callbacks invoked on every main-thread <see cref="_Process"/> tick (after the queued
        /// actions are drained), each receiving the frame <c>delta</c> in seconds. Unlike a dock Control's
        /// own <see cref="Node._Process"/> — which Godot skips while the dock tab is hidden — the dispatcher
        /// is a non-dock editor Node added under the <c>EditorPlugin</c>, so it ticks for the whole plugin
        /// lifetime regardless of which dock tab is active. This is what makes the connection panel's periodic
        /// status re-sync reliable even when its tab is not the foreground one (issue #42). Thread-affinity:
        /// register/unregister + invocation all happen on the editor main thread.
        /// </summary>
        static event Action<double>? _onProcess;

        /// <summary>
        /// Register a per-frame <paramref name="callback"/> (receives the frame delta in seconds) invoked on
        /// every main-thread tick. Returns an <see cref="IDisposable"/> that unregisters it — store it and
        /// dispose on teardown so a freed subscriber stops being ticked. Safe to call before any dispatcher
        /// is in the tree (the callback simply starts firing once one is).
        /// </summary>
        public static IDisposable RegisterProcess(Action<double> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            _onProcess += callback;
            return new ProcessRegistration(callback);
        }

        sealed class ProcessRegistration : IDisposable
        {
            Action<double>? _callback;
            public ProcessRegistration(Action<double> callback) => _callback = callback;

            public void Dispose()
            {
                if (_callback == null)
                    return;
                _onProcess -= _callback;
                _callback = null;
            }
        }

        /// <summary>The currently-installed dispatcher instance, or <c>null</c> when none is in the tree.</summary>
        public static MainThreadDispatcher? Instance => _instance;

        /// <summary>
        /// Queue an action to run on the next main-thread <see cref="_Process"/> tick. Thread-safe.
        /// <para>
        /// <b>Early-boot buffering (issue #169).</b> The dispatcher Node is added to the tree via
        /// <c>CallDeferred(AddChild)</c>, so it lands a frame after <c>GodotMcpRuntime.Build()</c> returns.
        /// A tool handler that marshals to the main thread in that window — before any dispatcher has yet
        /// entered the tree — must NOT throw: the action is buffered in the static queue and drained the
        /// moment the first dispatcher enters the tree (<see cref="_EnterTree"/>), with FIFO ordering
        /// preserved. This turns the install race into a deferred run.
        /// </para>
        /// <para>
        /// <b>Post-teardown fail-fast.</b> Once a dispatcher HAS booted and then been removed (plugin
        /// unloaded / between editor reloads), nothing remains to drain the queue, so a pending awaiter
        /// would hang forever. In that state (<see cref="_instance"/> is <c>null</c> but
        /// <see cref="_hasEverEntered"/> is <c>true</c>) <see cref="Enqueue"/> throws
        /// <see cref="InvalidOperationException"/> immediately, exactly as before.
        /// </para>
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            // No dispatcher currently in the tree: buffer if one has never booted yet (it will arrive and
            // drain — the #169 early-boot window), but fail fast if one booted and was torn down (nothing
            // left to drain → the awaiter would hang). With a live instance, this is the normal enqueue path.
            if (!InstancePresent && _hasEverEntered)
                throw new InvalidOperationException(
                    $"No {nameof(MainThreadDispatcher)} is in the tree; cannot enqueue main-thread work. " +
                    "The dispatcher booted and was removed (plugin unloaded / editor reload); nothing would " +
                    "drain the queue. It is added by GodotMcpPlugin._EnterTree and removed on _ExitTree.");

            _actions.Enqueue(action);
        }

        public override void _EnterTree()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            _instance = this;
            _hasEverEntered = true;

            // Drain anything buffered before this first dispatcher arrived (issue #169: tool handlers that
            // marshalled to the main thread in the Build()-returns → deferred-AddChild window). Running it
            // here — on the engine main thread, which is where _EnterTree fires — flushes the early-boot
            // backlog as soon as the dispatcher enters the tree rather than waiting for the first _Process
            // tick, and preserves FIFO order. (An action enqueued AFTER this loop observes the queue empty
            // is not lost — it is picked up on the next _Process tick, which runs exactly once per frame.)
            DrainQueue();
        }

        public override void _ExitTree()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;

            // Drain anything left so pending awaiters do not hang forever, but BOUND the drain to the
            // items already queued at this moment (see DrainQueueBounded). The unbounded DrainQueue used
            // on the normal _Process tick keeps draining until the queue empties; at teardown that is a
            // liveness hazard, because an action body (or, before GodotMainThread switched to
            // RunContinuationsAsynchronously, an awaiter continuation) that re-enqueues could make the
            // loop run forever while the editor is mid-teardown. Snapshotting the count first means a body
            // that re-enqueues lands its work in the queue but does NOT extend this drain — the teardown
            // terminates deterministically. (NOTE: a drained body still runs its FULL body here and may
            // touch the SceneTree while the editor tears the dispatcher down; callers must not assume the
            // tree is fully alive in work that could still be pending at _ExitTree time.)
            DrainQueueBounded();
        }

        public override void _Process(double delta)
        {
            DrainQueue();

            // Fire per-tick subscribers (e.g. the connection panel's status re-sync). Snapshot the delegate
            // so a callback that unregisters mid-invocation does not perturb the running invocation list.
            var onProcess = _onProcess;
            if (onProcess != null)
            {
                try
                {
                    onProcess(delta);
                }
                catch (Exception ex)
                {
                    GD.PushError($"[Godot-MCP] MainThreadDispatcher per-tick callback threw: {ex}");
                }
            }
        }

        static void DrainQueue()
        {
            while (_actions.TryDequeue(out var action))
            {
                InvokeDrained(action);
            }
        }

        /// <summary>
        /// Drain only the actions already queued at the moment this is called — at most a snapshot of the
        /// current <see cref="ConcurrentQueue{T}.Count"/> — so a drained body that re-enqueues cannot make
        /// the loop run unboundedly. Used at <see cref="_ExitTree"/> teardown, where the unbounded
        /// <see cref="DrainQueue"/> would be a liveness hazard against re-enqueueing work while the editor is
        /// tearing the dispatcher down. Re-enqueued items are simply left in the queue (no live dispatcher
        /// will drain them afterward; <see cref="Enqueue"/> then fails fast for any later caller because
        /// <see cref="_hasEverEntered"/> is set). FIFO order of the snapshot is preserved.
        /// </summary>
        static void DrainQueueBounded()
        {
            // Snapshot the bound BEFORE dequeuing. ConcurrentQueue.Count is a point-in-time read; capturing it
            // first caps the iteration at the items present now, so any item a drained body re-enqueues is not
            // pulled into this same drain pass.
            var budget = _actions.Count;
            for (var i = 0; i < budget && _actions.TryDequeue(out var action); i++)
            {
                InvokeDrained(action);
            }
        }

        /// <summary>
        /// Invoke one drained action, swallowing and surfacing any throw without killing the pump loop. The
        /// action is responsible for routing its own exceptions back to its awaiter (GodotMainThread wraps the
        /// body in a try/catch). A throw here would only mean a bug in the action wrapper itself.
        /// </summary>
        static void InvokeDrained(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] MainThreadDispatcher action threw: {ex}");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Pure-managed test seams (issue #169). The buffer/drain lifecycle lives entirely in the static
        // members above (_actions / _instance / _hasEverEntered / Enqueue / DrainQueue); none of it needs a
        // live Godot Node. But _EnterTree / _ExitTree — the only members that mutate _instance — are Node
        // lifecycle callbacks, and constructing a MainThreadDispatcher (a Godot Node) faults the binary-less
        // xUnit host with AccessViolationException. These seams let the unit tests model the boot/teardown
        // edges and drain the queue WITHOUT instantiating a Node, so the early-boot-buffering path is
        // CI-unit-testable. They are internal (same-assembly only) and never referenced by production code.

        /// <summary>Test-only: number of actions currently buffered (not yet drained).</summary>
        internal static int PendingActionCountForTests => _actions.Count;

        /// <summary>Test-only: true once any dispatcher has entered the tree (the fail-fast vs buffer edge).</summary>
        internal static bool HasEverEnteredForTests => _hasEverEntered;

        /// <summary>
        /// Test-only: model the first dispatcher entering the tree WITHOUT constructing a Godot Node.
        /// Mirrors <see cref="_EnterTree"/>'s pure-managed effects (capture the main-thread id, mark an
        /// instance present and ever-entered, drain the early-boot backlog) but never touches native Godot.
        /// </summary>
        internal static void SimulateInstanceEnteredForTests()
        {
            // Presence is faked via _instancePresentForTests — a real instance is a Node we cannot construct
            // in the binary-less host. _instance stays null here; production sets it from the real _EnterTree.
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            _hasEverEntered = true;
            _instancePresentForTests = true;
            DrainQueue();
        }

        /// <summary>
        /// Test-only: model the dispatcher leaving the tree (teardown). Mirrors <see cref="_ExitTree"/>'s
        /// pure-managed effects — mark no instance present and drain anything still pending via the SAME
        /// bounded drain teardown uses — so that the unit tests exercise the real termination guarantee and a
        /// subsequent <see cref="Enqueue"/> takes the post-teardown fail-fast branch.
        /// </summary>
        internal static void SimulateInstanceExitedForTests()
        {
            _instancePresentForTests = false;
            DrainQueueBounded();
        }

        /// <summary>Test-only: drain the buffered actions on the calling thread (no Node, no tick).</summary>
        internal static void DrainForTests() => DrainQueue();

        /// <summary>
        /// Test-only: model the bounded teardown drain (<see cref="_ExitTree"/>) directly, WITHOUT also
        /// flipping the instance-present flag. Lets a test assert the snapshot-and-bound termination guarantee
        /// against a re-enqueueing body in isolation from the fail-fast lifecycle transition.
        /// </summary>
        internal static void DrainBoundedForTests() => DrainQueueBounded();

        /// <summary>
        /// Test-only: reset ALL static lifecycle state to its pre-boot defaults. Static state outlives a
        /// single xUnit test, so a test exercising the boot/teardown edges MUST call this first to start from
        /// a clean "never booted, nothing buffered" baseline.
        /// </summary>
        internal static void ResetForTests()
        {
            while (_actions.TryDequeue(out _)) { }
            _instance = null;
            _instancePresentForTests = false;
            _hasEverEntered = false;
            MainThreadId = -1;
        }
    }
}
