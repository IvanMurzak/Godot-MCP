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
        public static void Install()
        {
            if (Instance is GodotMainThread)
                return;

            Instance = new GodotMainThread();
        }

        public override bool IsMainThread => MainThreadDispatcher.IsMainThread;

        public override Task RunAsync(Task task)
            => MainThreadDispatcher.IsMainThread ? task : Dispatch(() => { task.Wait(); return true; });

        public override Task<T> RunAsync<T>(Task<T> task)
            => MainThreadDispatcher.IsMainThread ? task : Dispatch(() => task.Result);

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
        /// </summary>
        static Task<T> Dispatch<T>(Func<T> body)
        {
            var tcs = new TaskCompletionSource<T>();

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
#endif
