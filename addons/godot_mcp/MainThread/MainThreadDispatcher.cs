/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
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
        static MainThreadDispatcher? _instance;

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
        /// Fails fast when no dispatcher is in the tree (plugin unloaded / between editor reloads):
        /// nothing would drain the queue, so the caller's awaiter would hang forever — surface that
        /// as an immediate <see cref="InvalidOperationException"/> instead.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (_instance == null)
                throw new InvalidOperationException(
                    $"No {nameof(MainThreadDispatcher)} is in the tree; cannot enqueue main-thread work. " +
                    "The dispatcher is added by GodotMcpPlugin._EnterTree and removed on _ExitTree.");

            _actions.Enqueue(action);
        }

        public override void _EnterTree()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            _instance = this;
        }

        public override void _ExitTree()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;

            // Drain anything left so pending awaiters do not hang forever. NOTE: this runs each
            // queued action's FULL body during teardown — those bodies may touch the SceneTree
            // while the editor is tearing the dispatcher down. Callers must not assume the tree is
            // fully alive in work that could still be pending at _ExitTree time.
            DrainQueue();
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
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    // The action is responsible for routing its own exceptions back to its awaiter
                    // (GodotMainThread wraps the body in a try/catch). A throw here would only mean a
                    // bug in the action wrapper itself; surface it without killing the pump loop.
                    GD.PushError($"[Godot-MCP] MainThreadDispatcher action threw: {ex}");
                }
            }
        }
    }
}
#endif
