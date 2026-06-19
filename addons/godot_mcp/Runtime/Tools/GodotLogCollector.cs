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
    /// In-memory, bounded ring-buffer of captured editor log lines — the Godot analog of Unity-MCP's
    /// <c>UnityLogCollector</c>. Unity subscribes to <c>Application.logMessageReceivedThreaded</c> to
    /// capture EVERY engine log line; Godot's C# API exposes NO such global managed log hook (it is a
    /// long-standing engine gap — there is no managed <c>OS.add_logger</c> / log-received signal in 4.x).
    /// So this collector is fed explicitly by the plugin's own logging path (the <c>GD.Print</c> /
    /// <c>GD.PushWarning</c> / <c>GD.PushError</c> wrapper installed at editor boot), giving the
    /// <c>console-get-logs</c> tool a faithful, queryable record of the Godot-MCP plugin's own editor
    /// activity even though the broader editor console cannot be tapped from managed code.
    ///
    /// <para>
    /// Thread-safe (a single lock guards the buffer): <see cref="Append"/> may be called from any thread
    /// the plugin logs on, while <see cref="Query"/> reads from the tool dispatch thread. Bounded by
    /// <see cref="Capacity"/> — once full, the oldest line is dropped (FIFO), so memory never grows
    /// without bound during a long editor session.
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain
    /// xUnit host.
    /// </summary>
    public sealed class GodotLogCollector
    {
        /// <summary>Maximum number of retained log lines before the oldest is evicted (FIFO).</summary>
        public const int Capacity = 1000;

        readonly object _gate = new();
        readonly Queue<LogEntry> _entries = new(Capacity);

        /// <summary>
        /// Process-wide collector used by the plugin's logging path and read by the <c>console-*</c>
        /// tools. Assigned once at editor boot; tools fall back to a fresh empty collector via
        /// <see cref="GetOrCreate"/> when the plugin has not booted (e.g. a direct unit call), so the
        /// tool layer never hard-depends on the editor lifecycle.
        /// </summary>
        public static GodotLogCollector? Current { get; set; }

        /// <summary>Return <see cref="Current"/> when set, otherwise a fresh empty collector.</summary>
        public static GodotLogCollector GetOrCreate() => Current ??= new GodotLogCollector();

        /// <summary>Append a captured line, evicting the oldest when at <see cref="Capacity"/>.</summary>
        public void Append(LogEntry entry)
        {
            if (entry == null)
                return;

            lock (_gate)
            {
                if (_entries.Count >= Capacity)
                    _entries.Dequeue();
                _entries.Enqueue(entry);
            }
        }

        /// <summary>Convenience overload: capture a line from its parts (timestamp defaults to now-UTC).</summary>
        public void Append(GodotLogType logType, string message, string? stackTrace = null)
            => Append(new LogEntry(logType, message, DateTime.UtcNow, stackTrace));

        /// <summary>Drop all retained log lines.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
            }
        }

        /// <summary>Current number of retained log lines.</summary>
        public int Count
        {
            get { lock (_gate) { return _entries.Count; } }
        }

        /// <summary>
        /// Query the retained lines, newest-first, mirroring Unity-MCP's <c>LogCollector.Query</c>
        /// semantics: optional severity filter, optional last-N-minutes window, stack-trace strip, and a
        /// <paramref name="maxEntries"/> cap applied AFTER ordering (so the cap keeps the most recent
        /// lines). Returns a fresh array of copies so the caller can serialize off the lock.
        /// </summary>
        public LogEntry[] Query(
            int maxEntries = 100,
            GodotLogType? logTypeFilter = null,
            bool includeStackTrace = false,
            int lastMinutes = 0)
        {
            if (maxEntries < 1)
                maxEntries = 1;

            DateTime? cutoff = lastMinutes > 0 ? DateTime.UtcNow.AddMinutes(-lastMinutes) : null;

            lock (_gate)
            {
                IEnumerable<LogEntry> q = _entries;

                if (logTypeFilter.HasValue)
                    q = q.Where(e => e.LogType == logTypeFilter.Value);

                if (cutoff.HasValue)
                    q = q.Where(e => e.Timestamp >= cutoff.Value);

                // Newest-first, then cap. The buffer is FIFO (oldest at head), so reverse to get newest-first.
                return q
                    .Reverse()
                    .Take(maxEntries)
                    .Select(e => includeStackTrace
                        ? new LogEntry(e.LogType, e.Message, e.Timestamp, e.StackTrace)
                        : new LogEntry(e.LogType, e.Message, e.Timestamp, stackTrace: null))
                    .ToArray();
            }
        }
    }
}
