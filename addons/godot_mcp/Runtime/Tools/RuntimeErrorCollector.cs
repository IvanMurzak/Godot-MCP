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
using com.IvanMurzak.Godot.MCP.Data;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// In-memory, bounded ring-buffer of <see cref="RuntimeError"/> rows captured inside a RUNNING game —
    /// the in-game analog of <see cref="GodotLogCollector"/>, but holding the richer structured runtime-error
    /// shape (source / type / file / line / function / stack trace) rather than plain log lines. It is the
    /// store the <c>runtime-errors-get</c> tool reads.
    ///
    /// <para>
    /// <b>Monotonic sequence + since-marker poll.</b> Every appended error is stamped with a process-wide
    /// monotonically-increasing <see cref="RuntimeError.Sequence"/> (never reused, even after eviction or
    /// <see cref="Clear"/>). <see cref="QuerySince"/> returns only errors NEWER than a caller-supplied
    /// sequence, so an agent driving a "launch the game, keep fixing until no new errors" loop can poll with
    /// the largest sequence it has seen and get exactly the deltas — the cornerstone use-case from issue #160.
    /// </para>
    ///
    /// <para>
    /// Thread-safe (a single lock guards the buffer): <see cref="Append"/> is called from arbitrary threads
    /// (Godot's logger callback is multi-threaded; <c>AppDomain.UnhandledException</c> /
    /// <c>TaskScheduler.UnobservedTaskException</c> fire on whatever thread faulted), while
    /// <see cref="QuerySince"/> reads from the tool dispatch thread. Bounded by <see cref="Capacity"/> — once
    /// full the oldest error is evicted (FIFO), so memory never grows without bound during a long play
    /// session. <see cref="HighestSequence"/> is retained across eviction so a since-poll still advances even
    /// if the matching errors were evicted before the agent read them (the result then flags truncation).
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host and ships into a game build.
    /// </summary>
    public sealed class RuntimeErrorCollector
    {
        /// <summary>Maximum number of retained runtime errors before the oldest is evicted (FIFO).</summary>
        public const int Capacity = 1000;

        readonly object _gate = new();
        readonly Queue<RuntimeError> _entries = new(Capacity);
        long _nextSequence = 1;
        long _highestSequence = 0;

        /// <summary>
        /// Process-wide collector for the running game, set by <c>RuntimeErrorCapture.Install</c> when the
        /// in-game runtime is started with error capture enabled, and read by <c>runtime-errors-get</c>.
        /// Null when capture was never enabled (the default-OFF posture) — the tool reports
        /// <c>available:false</c> in that case so the agent does not read silence as health.
        /// </summary>
        public static RuntimeErrorCollector? Current { get; set; }

        /// <summary>
        /// Append a captured runtime error, stamping it with the next monotonic
        /// <see cref="RuntimeError.Sequence"/> and evicting the oldest when at <see cref="Capacity"/>.
        /// Returns the assigned sequence. Thread-safe. A null <paramref name="error"/> is ignored (returns 0).
        /// </summary>
        public long Append(RuntimeError error)
        {
            if (error == null)
                return 0;

            lock (_gate)
            {
                var seq = _nextSequence++;
                error.Sequence = seq;
                _highestSequence = seq;

                if (_entries.Count >= Capacity)
                    _entries.Dequeue();
                _entries.Enqueue(error);
                return seq;
            }
        }

        /// <summary>Drop all retained runtime errors. The monotonic sequence counter is NOT reset, so a
        /// post-clear since-poll with a pre-clear sequence still behaves (no duplicate/old rows reappear).</summary>
        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
            }
        }

        /// <summary>Current number of retained runtime errors.</summary>
        public int Count
        {
            get { lock (_gate) { return _entries.Count; } }
        }

        /// <summary>
        /// The highest sequence number ever assigned (retained across eviction and <see cref="Clear"/>). An
        /// agent passes this back as <c>sinceSequence</c> to poll only newer errors. 0 before any append.
        /// </summary>
        public long HighestSequence
        {
            get { lock (_gate) { return _highestSequence; } }
        }

        /// <summary>
        /// Return retained errors whose <see cref="RuntimeError.Sequence"/> is strictly greater than
        /// <paramref name="sinceSequence"/>, oldest-first (chronological), capped at <paramref name="maxEntries"/>.
        /// Pass <c>sinceSequence = 0</c> (the default) for every retained error. When more matching errors
        /// exist than the cap, the NEWEST are kept and <paramref name="truncated"/> is set true — so an agent
        /// reading deltas never silently misses the most recent fault. Returns fresh copies so the caller can
        /// serialize off the lock.
        /// <para>
        /// <paramref name="maxEntries"/> has a hard floor of 1: a value &lt; 1 is clamped up to 1 (this is the
        /// reusable buffer's defensive contract — it never returns an empty page for a non-positive cap). The
        /// <c>runtime-errors-get</c> tool surface is stricter and rejects <c>maxEntries &lt; 1</c> with an
        /// <see cref="ArgumentException"/> before reaching here, so the clamp is the inner-layer safety net for
        /// direct callers, not the validated tool path.
        /// </para>
        /// </summary>
        public RuntimeError[] QuerySince(long sinceSequence, int maxEntries, out bool truncated)
        {
            if (maxEntries < 1)
                maxEntries = 1; // floor; the tool layer rejects < 1 upstream — see the doc remark above.

            lock (_gate)
            {
                // Newer-than-marker, in stored (chronological) order.
                var matching = _entries.Where(e => e.Sequence > sinceSequence).ToList();
                truncated = matching.Count > maxEntries;

                // Keep the NEWEST page when capping: take the last `maxEntries` of the chronological list, so
                // the most-recent faults are never dropped in favor of older ones.
                IEnumerable<RuntimeError> page = truncated
                    ? matching.Skip(matching.Count - maxEntries)
                    : matching;

                return page.Select(Copy).ToArray();
            }
        }

        /// <summary>Defensive deep-ish copy so a returned row can be serialized/mutated off the lock without
        /// touching the buffered instance. The deep backtrace (issue #163) is copied into a FRESH frame list so
        /// the returned row never aliases the buffered row's frames; the frames themselves are immutable-by-use
        /// plain primitives, so a shallow element copy is sufficient.</summary>
        static RuntimeError Copy(RuntimeError e) => new(
            source: e.Source,
            message: e.Message,
            type: e.Type,
            file: e.File,
            line: e.Line,
            function: e.Function,
            stackTrace: e.StackTrace,
            timestamp: e.Timestamp,
            frames: e.Frames != null ? new List<RuntimeErrorFrame>(e.Frames) : null)
        {
            Sequence = e.Sequence,
        };
    }
}
