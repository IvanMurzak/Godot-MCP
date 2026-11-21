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
using System.Text;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The <c>game-run</c> result text (pure, unit-tested): a summary line, the errors and warnings (each with its indented
    /// <c>at:</c> / backtrace lines), then what the game printed. Godot prints the first two kinds on stderr, the rest on stdout.
    /// </summary>
    public static class GameRunOutput
    {
        public const int MaxPrintedLines = 40;
        public const int MaxIssueLines = 60;

        // Lines Godot prints at every exit of a run cut short by --quit-after: not the game's problems.
        static readonly string[] ExitNoise = { "leaked at exit", "ObjectDB instance", "RID allocations", "resources still in use",
            "Leaked instance dependency", "Pages in use exist at exit" };

        /// <param name="what">The scene that ran, e.g. "res://scenes/level.tscn" or "the main scene".</param>
        /// <param name="seconds">The game time it ran for.</param>
        /// <param name="exitCode">The process exit code; null when it was stopped after <paramref name="stoppedAfterSeconds"/>.</param>
        /// <param name="stoppedAfterSeconds">The real-time limit that stopped a run still going.</param>
        /// <param name="lines">stdout and stderr, in arrival order.</param>
        public static string Format(string what, double seconds, int? exitCode, double stoppedAfterSeconds, IReadOnlyList<string> lines)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var printed = new List<string>();
            int errorCount = 0, warningCount = 0;
            List<string>? current = null; // where the indented detail lines of the last issue go (null: printed output)
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i].TrimEnd();
                if (i == 0 && line.StartsWith("Godot Engine v", StringComparison.Ordinal)) continue;
                if (line.Length == 0) { current = null; continue; }
                if (char.IsWhiteSpace(line[0]) && current != null)
                {
                    current.Add(line);
                    continue;
                }
                switch (Kind(line))
                {
                    case IssueKind.None:
                        current = null;
                        printed.Add(line);
                        break;
                    case var _ when IsExitNoise(line):
                        current = new List<string>(); // swallow its detail lines too
                        break;
                    case IssueKind.Error:
                        errorCount++;
                        (current = errors).Add(line);
                        break;
                    default:
                        warningCount++;
                        (current = warnings).Add(line);
                        break;
                }
            }

            var sb = new StringBuilder();
            sb.Append("Ran ").Append(what).Append(" for ").Append(seconds.ToString(CultureInfo.InvariantCulture)).Append(" s of game time: ");
            sb.Append(exitCode is { } code
                ? "exit code " + code.ToString(CultureInfo.InvariantCulture)
                : "still running after " + stoppedAfterSeconds.ToString(CultureInfo.InvariantCulture) + " s of real time, stopped");
            sb.Append(", ").Append(errorCount).Append(" error(s), ").Append(warningCount).Append(" warning(s).\n");
            AppendBlock(sb, "Errors:", errors, 0, MaxIssueLines);
            AppendBlock(sb, "Warnings:", warnings, 0, MaxIssueLines);
            var skip = Math.Max(0, printed.Count - MaxPrintedLines);
            AppendBlock(sb, skip > 0 ? $"Printed (last {MaxPrintedLines} of {printed.Count} lines):" : "Printed:", printed, skip, MaxPrintedLines);
            return sb.ToString().TrimEnd();
        }

        enum IssueKind { None, Error, Warning }

        static IssueKind Kind(string line)
        {
            if (StartsWithAny(line, "ERROR:", "SCRIPT ERROR:", "USER ERROR:", "USER SCRIPT ERROR:", "SHADER ERROR:"))
                return IssueKind.Error;
            if (StartsWithAny(line, "WARNING:", "SCRIPT WARNING:", "USER WARNING:", "USER SCRIPT WARNING:"))
                return IssueKind.Warning;
            return IssueKind.None;
        }

        static bool StartsWithAny(string line, params string[] prefixes)
        {
            foreach (var p in prefixes)
                if (line.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        static bool IsExitNoise(string line)
        {
            foreach (var n in ExitNoise)
                if (line.Contains(n, StringComparison.Ordinal)) return true;
            return false;
        }

        static void AppendBlock(StringBuilder sb, string title, List<string> lines, int from, int max)
        {
            if (lines.Count == 0) return;
            sb.Append(title).Append('\n');
            var end = Math.Min(lines.Count, from + max);
            for (var i = from; i < end; i++) sb.Append(lines[i]).Append('\n');
            if (lines.Count > end) sb.Append("… ").Append(lines.Count - end).Append(" more line(s)\n");
        }
    }
}
#endif
