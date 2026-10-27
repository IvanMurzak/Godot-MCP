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
using System.Threading;

namespace com.IvanMurzak.Godot.MCP.Reload
{
    /// <summary>
    /// Admission gate for MCP tool calls around a Godot C# assembly reload, without Godot APIs (the editor glue is
    /// <c>ReloadGateDriver</c>).
    /// <para>
    /// Godot's <c>HotReloadAssemblyWatcher</c> reloads the project assembly about 0.5 s after the built DLL changes, no
    /// matter what the plugin is doing. The unload only succeeds when nothing of this (collectible) assembly is still
    /// running or referenced; a tool call in flight is exactly that: a thread-pool thread blocked inside the tool method
    /// on <c>MainThread.Run</c>, and, once it finishes, ReflectorNet / System.Text.Json caching the result's types again
    /// after teardown cleared them. Either keeps the old assembly alive, Godot retries three times and gives up, and the
    /// plugin stays offline until the editor restarts.
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="GatePhase.Open"/>: calls run.</item>
    /// <item><see cref="GatePhase.Pending"/>: the DLL on disk is newer than the loaded one (<see cref="Observe"/>), so
    /// Godot reloads soon. New calls are refused with a retryable error; calls already running finish, and the driver
    /// defers Godot's reload while they do (<see cref="HoldReload"/>, at most <see cref="MaxHoldMs"/>).</item>
    /// <item><see cref="GatePhase.Unloading"/>: the assembly is being unloaded (plugin teardown). Everything is
    /// refused; when the last running call leaves, <see cref="Drained"/> runs (it clears the caches that call filled).</item>
    /// </list>
    /// Thread-safe: <see cref="TryEnter"/> / <see cref="Exit"/> run on tool-call threads, the rest on the main thread.
    /// </summary>
    public sealed class AssemblyReloadGate
    {
        /// <summary>Longest time Godot's reload is deferred for calls still running.</summary>
        public const long MaxHoldMs = 10_000;

        /// <summary>
        /// A pending reload that has not started this long after it was detected (Godot did not reload: hot reload is
        /// off, or Godot gave up on this assembly) reopens the gate, so calls are never refused for good.
        /// </summary>
        public const long PendingTimeoutMs = MaxHoldMs + 15_000;

        public const string PendingMessage =
            "Godot is about to reload the project's C# assembly (the built DLL changed), so this call was not run. " +
            "Retry in a few seconds, once the plugin has reconnected.";

        public const string UnloadingMessage =
            "Godot is reloading the project's C# assembly, so this call was not run. " +
            "Retry in a few seconds, once the plugin has reconnected.";

        public const string ShutdownMessage =
            "The Godot-MCP plugin is shutting down (the editor is closing or the plugin was disabled), so this call was not run.";

        public enum GatePhase { Open, Pending, Unloading }

        public enum Observation { None, BecamePending, PendingExpired }

        readonly object _lock = new object();
        GatePhase _phase = GatePhase.Open;
        long _pendingSinceMs;
        long _baselineSeconds = -1;
        int _inFlight;
        string _unloadMessage = UnloadingMessage;

        public GatePhase Phase { get { lock (_lock) return _phase; } }

        /// <summary>Calls admitted by <see cref="TryEnter"/> that have not left yet.</summary>
        public int InFlight => Volatile.Read(ref _inFlight);

        /// <summary>Runs (on the leaving call's thread) when the last running call leaves an unloading gate. Never throws out.</summary>
        public Action? Drained { get; set; }

        /// <summary>The message a refused call gets, or null while the gate is open.</summary>
        public string? Refusal { get { lock (_lock) return RefusalFor(_phase); } }

        string? RefusalFor(GatePhase phase) => phase switch
        {
            GatePhase.Pending => PendingMessage,
            GatePhase.Unloading => _unloadMessage,
            _ => null,
        };

        /// <summary>Admit a call (true; pair it with <see cref="Exit"/>) or refuse it with <paramref name="refusal"/>.</summary>
        public bool TryEnter(out string? refusal)
        {
            lock (_lock)
            {
                refusal = RefusalFor(_phase);
                if (refusal != null)
                    return false;
                _inFlight++;
                return true;
            }
        }

        /// <summary>The admitted call left (finished or failed).</summary>
        public void Exit()
        {
            bool drained;
            lock (_lock)
            {
                if (_inFlight > 0)
                    _inFlight--;
                drained = _inFlight == 0 && _phase == GatePhase.Unloading;
            }
            if (!drained)
                return;
            try { Drained?.Invoke(); }
            catch (Exception) { /* a cleanup hook must not fail the call that ran it */ }
        }

        /// <summary>
        /// Record the write time of the DLL this assembly was loaded from. Godot compares whole seconds
        /// (<c>FileAccess.get_modified_time</c>), so the gate does too: a rebuild within the same second never reloads.
        /// </summary>
        public void SetBaseline(DateTime? writeTimeUtc)
        {
            lock (_lock)
                _baselineSeconds = writeTimeUtc is { } t ? ToSeconds(t) : -1;
        }

        /// <summary>
        /// Feed the current write time of the built DLL (null = missing). Turns <see cref="GatePhase.Open"/> into
        /// <see cref="GatePhase.Pending"/> when the DLL is newer than the loaded one, and a pending gate that outlived
        /// <see cref="PendingTimeoutMs"/> back into <see cref="GatePhase.Open"/> with the new DLL as the baseline.
        /// </summary>
        public Observation Observe(DateTime? writeTimeUtc, long nowMs)
        {
            lock (_lock)
            {
                if (writeTimeUtc is not { } time)
                    return Observation.None;
                var seconds = ToSeconds(time);
                if (_baselineSeconds < 0)
                {
                    _baselineSeconds = seconds;
                    return Observation.None;
                }
                switch (_phase)
                {
                    case GatePhase.Open when seconds > _baselineSeconds:
                        _phase = GatePhase.Pending;
                        _pendingSinceMs = nowMs;
                        return Observation.BecamePending;
                    case GatePhase.Pending when nowMs - _pendingSinceMs >= PendingTimeoutMs:
                        _phase = GatePhase.Open;
                        _baselineSeconds = seconds;
                        return Observation.PendingExpired;
                    default:
                        return Observation.None;
                }
            }
        }

        /// <summary>
        /// Whether Godot's reload should wait: it is pending, calls admitted before it are still running, and they have
        /// not been waited for longer than <see cref="MaxHoldMs"/>.
        /// </summary>
        public bool HoldReload(long nowMs)
        {
            lock (_lock)
                return _phase == GatePhase.Pending && _inFlight > 0 && nowMs - _pendingSinceMs < MaxHoldMs;
        }

        /// <summary>The assembly is going away: refuse every call from now on, with <paramref name="message"/>.</summary>
        public void BeginUnload(string message = UnloadingMessage)
        {
            lock (_lock)
            {
                _phase = GatePhase.Unloading;
                _unloadMessage = message;
            }
        }

        /// <summary>Whole Unix seconds, the resolution Godot compares DLL write times at.</summary>
        public static long ToSeconds(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }
}
