/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Threading;

namespace com.IvanMurzak.Godot.MCP.Reload
{
    /// <summary>
    /// Run a teardown step on its own thread and wait for it at most a bounded time, so the editor main thread (the thread
    /// Godot unloads the assembly on) cannot hang in it. The SignalR disconnect of a reload runs synchronous observers and
    /// cancellation callbacks inline, and a <c>Task.Wait(timeout)</c> on a not-yet-started task may run that task on the
    /// waiting thread itself: either way a step that looked bounded could block the main thread with no end (the reload
    /// smoke hung that way in CI, the editor silent after "WebSocket closed by the server"). A dedicated thread (not the
    /// pool, so nothing is inlined into the caller) plus <see cref="Thread.Join(TimeSpan)"/> bounds it. Pure managed, unit-tested.
    /// </summary>
    public static class BoundedCall
    {
        /// <summary>
        /// Run <paramref name="step"/> on a background thread and wait at most <paramref name="timeout"/>: true when it
        /// finished in time (an exception it threw is returned in <paramref name="error"/>, never rethrown), false when it is
        /// still running (it is left running; the caller proceeds).
        /// </summary>
        public static bool Run(Action step, TimeSpan timeout, out Exception? error)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { step(); }
                catch (Exception ex) { failure = ex; }
            })
            { IsBackground = true, Name = "Godot-MCP teardown step" };
            thread.Start();
            var finished = thread.Join(timeout);
            error = finished ? failure : null;
            return finished;
        }
    }
}
#endif
