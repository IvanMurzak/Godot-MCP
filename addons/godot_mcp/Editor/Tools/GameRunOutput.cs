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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using com.IvanMurzak.Godot.MCP.Editor.Rql;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The <c>game-run</c> result text (pure, unit-tested): a summary line, the distinct errors and warnings (each with
    /// where it happened and how often), then the last lines the game printed. Lines are parsed like the <c>gameLog</c>
    /// source (<see cref="GameLogText"/>), so both classify Godot's output the same way.
    /// </summary>
    /// <summary>A run's output lines, bounded: the first and the last <see cref="Keep"/> lines are kept, the middle counted.</summary>
    public sealed class GameRunLines
    {
        public const int Keep = 10_000;

        readonly List<string> _head = new List<string>();
        readonly Queue<string> _tail = new Queue<string>();

        public int Dropped { get; private set; }

        public void Add(string line)
        {
            if (_head.Count < Keep) { _head.Add(line); return; }
            _tail.Enqueue(line);
            if (_tail.Count > Keep) { _tail.Dequeue(); Dropped++; }
        }

        /// <summary>The kept lines in order (the head, then the tail).</summary>
        public List<string> Lines
        {
            get { var all = new List<string>(_head); all.AddRange(_tail); return all; }
        }
    }

    public static class GameRunOutput
    {
        public const int MaxPrintedLines = 40;
        public const int MaxIssues = 20;

        // What Godot prints at every exit of a run cut short by --quit-after: not the game's problems.
        static readonly string[] ExitNoise = { "leaked at exit", "ObjectDB instance", "RID allocations", "resources still in use",
            "Leaked instance dependency", "Pages in use exist at exit" };

        /// <param name="what">The scene that ran, e.g. "res://scenes/level.tscn" or "the main scene".</param>
        /// <param name="seconds">The game time it ran for.</param>
        /// <param name="exitCode">The process exit code; null when it was stopped after <paramref name="stoppedAfterSeconds"/>.</param>
        /// <param name="stoppedAfterSeconds">The real-time limit that stops a run still going.</param>
        /// <param name="lines">stdout and stderr, in arrival order.</param>
        /// <param name="droppedLines">Lines from the middle of a very long output that were not kept.</param>
        public static string Format(string what, double seconds, int? exitCode, double stoppedAfterSeconds,
            IReadOnlyList<string> lines, int droppedLines = 0)
        {
            var errors = new Issues();
            var warnings = new Issues();
            var printed = new List<string>();
            foreach (var e in GameLogText.Parse(string.Join("\n", lines)))
            {
                if (e.LogType == "Log")
                {
                    // The banner is stripped by Parse only as the first line (stderr may arrive first on a pipe).
                    if (e.Message.Trim().Length > 0 && !e.Message.StartsWith("Godot Engine v", StringComparison.Ordinal))
                        printed.Add(e.Message.TrimEnd());
                }
                else if (!ExitNoise.Any(n => e.Message.Contains(n, StringComparison.Ordinal)))
                    (e.LogType == "Error" ? errors : warnings).Add(e);
            }

            var sb = new StringBuilder();
            sb.Append("Ran ").Append(what).Append(" for ").Append(seconds.ToString(CultureInfo.InvariantCulture)).Append(" s of game time: ");
            sb.Append(exitCode is { } code
                ? "exit code " + code.ToString(CultureInfo.InvariantCulture)
                : "still running after " + stoppedAfterSeconds.ToString(CultureInfo.InvariantCulture) + " s of real time, stopped");
            sb.Append(", ").Append(errors.Total).Append(" error(s), ").Append(warnings.Total).Append(" warning(s).");
            if (droppedLines > 0) sb.Append(" Output cut: ").Append(droppedLines).Append(" line(s) in the middle were not kept.");
            sb.Append('\n');
            errors.AppendTo(sb, "Errors:");
            warnings.AppendTo(sb, "Warnings:");
            if (printed.Count > 0)
            {
                var skip = Math.Max(0, printed.Count - MaxPrintedLines);
                sb.Append(skip > 0 ? $"Printed (last {MaxPrintedLines} of {printed.Count} lines):\n" : "Printed:\n");
                foreach (var line in printed.Skip(skip)) sb.Append(line).Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Distinct issues in first-seen order: an error raised every frame is listed once, with its count.</summary>
        sealed class Issues
        {
            readonly List<(GameLogEntry entry, int count)> _distinct = new List<(GameLogEntry, int)>();
            readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

            public int Total { get; private set; }

            public void Add(GameLogEntry e)
            {
                Total++;
                var key = e.Message + "\n" + e.StackTrace;
                if (_index.TryGetValue(key, out var i)) _distinct[i] = (_distinct[i].entry, _distinct[i].count + 1);
                else { _index[key] = _distinct.Count; _distinct.Add((e, 1)); }
            }

            public void AppendTo(StringBuilder sb, string title)
            {
                if (_distinct.Count == 0) return;
                sb.Append(title).Append('\n');
                foreach (var (entry, count) in _distinct.Take(MaxIssues))
                {
                    sb.Append("- ").Append(entry.Message);
                    if (count > 1) sb.Append(" (x").Append(count).Append(')');
                    sb.Append('\n');
                    foreach (var line in entry.StackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        sb.Append("  ").Append(line).Append('\n');
                }
                if (_distinct.Count > MaxIssues) sb.Append("… ").Append(_distinct.Count - MaxIssues).Append(" more distinct\n");
            }
        }
    }
}
#endif
