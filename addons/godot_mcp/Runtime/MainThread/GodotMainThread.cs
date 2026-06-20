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
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.MainThreadDispatch
{
    /// <summary>
    /// Godot implementation of ReflectorNet's <see cref="MainThread"/>. Marshals delegates onto the
    /// Godot editor main thread via <see cref="MainThreadDispatcher"/> and returns the delegate's
    /// value/exception as an awaitable. This is the Godot analog of Unity-MCP's <c>UnityMainThread</c>
    /// (which dispatches through <c>EditorApplication.update</c>).
    ///
    /// Install once at editor boot via <see cref="Install"/> — <c>GodotMcpPlugin._EnterTree</c> does
    /// this after it adds the <see cref="MainThreadDispatcher"/> Node to the tree. After install, all
    /// tool handlers can call <c>MainThread.Instance.Run(() =&gt; /* Godot API */)</c> from any thread.
    /// </summary>
    public sealed class GodotMainThread : MainThread
    {
        /// <summary>
        /// Replace the global <see cref="MainThread.Instance"/> with the Godot dispatcher-backed
        /// implementation. Idempotent — safe to call again on a domain/editor reload.
        /// </summary>
        /// <remarks>
        /// Main-thread-boot only: this is intended to run once during editor startup
        /// (<c>GodotMcpPlugin._EnterTree</c>) on the Godot main thread, so the check-then-assign
        /// below is deliberately non-atomic — there is no concurrent installer to race against.
        /// </remarks>
        public static void Install()
        {
            if (Instance is GodotMainThread)
                return;

            Instance = new GodotMainThread();
        }

        public override bool IsMainThread => MainThreadDispatcher.IsMainThread;

        /// <summary>
        /// Run a pre-existing <paramref name="task"/> to completion, marshalled to the main thread
        /// when called off it. <c>GetAwaiter().GetResult()</c> blocks the pump tick until the task
        /// finishes and re-throws the task's original exception (not an <c>AggregateException</c>),
        /// matching the raw-exception propagation of the <see cref="RunAsync(Action)"/> /
        /// <see cref="RunAsync{T}(Func{T})"/> overloads.
        /// <para>
        /// WARNING: because the awaited task is blocked on the single-threaded pump, the passed
        /// <paramref name="task"/> MUST NOT itself need the main thread to make progress (e.g. it
        /// must not <c>await MainThread.Instance.Run(...)</c>), or it will dead-lock the pump tick.
        /// </para>
        /// </summary>
        public override Task RunAsync(Task task)
            => MainThreadDispatcher.IsMainThread ? task : Dispatch(() => { task.GetAwaiter().GetResult(); return true; });

        /// <inheritdoc cref="RunAsync(Task)"/>
        public override Task<T> RunAsync<T>(Task<T> task)
            => MainThreadDispatcher.IsMainThread ? task : Dispatch(() => task.GetAwaiter().GetResult());

        public override Task<T> RunAsync<T>(Func<T> func)
            => MainThreadDispatcher.IsMainThread ? Task.FromResult(func()) : Dispatch(func);

        public override Task RunAsync(Action action)
        {
            if (MainThreadDispatcher.IsMainThread)
            {
                action();
                return Task.CompletedTask;
            }
            return Dispatch(() => { action(); return true; });
        }

        /// <summary>
        /// Enqueue <paramref name="body"/> on the dispatcher and return a task that completes with the
        /// body's result, or faults with its exception. Mirrors the TaskCompletionSource pattern in
        /// Unity-MCP's <c>UnityMainThread.Dispatch</c>.
        /// <para>
        /// The TCS is created with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/> (the same
        /// option <c>DevControlServer.RunOnMainThread</c> already uses) so the awaiter's continuation is posted
        /// to the thread pool instead of running INLINE on whatever thread completes the TCS. The body is
        /// completed from <see cref="MainThreadDispatcher.DrainQueue"/>, which runs on the editor main thread —
        /// including during <c>MainThreadDispatcher._ExitTree</c> teardown. Without this option a continuation
        /// (e.g. more <c>await MainThread.Instance.Run(...)</c> work that touches the SceneTree) would execute
        /// synchronously on the pump thread mid-teardown, a re-entrancy / SceneTree-touch hazard; it would also
        /// let a re-enqueueing continuation extend the drain inline. Posting continuations off-thread keeps the
        /// pump tick (and the bounded teardown drain) free of caller-supplied continuation bodies.
        /// </para>
        /// </summary>
        static Task<T> Dispatch<T>(Func<T> body)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(body());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}
