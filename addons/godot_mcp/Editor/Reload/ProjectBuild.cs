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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Reload
{
    /// <summary>
    /// A <c>script-build</c> in progress, kept in a file under <c>.godot/</c> because its result is usually reported by
    /// the NEXT load of this assembly (the build changes the DLL, Godot reloads it). Plain <c>key=value</c> lines, so no
    /// serializer caches this assembly's types. Pure (unit-tested).
    /// </summary>
    public sealed class BuildRecord
    {
        public string RequestId { get; set; } = "";
        public int ProcessId { get; set; }
        public string LogPath { get; set; } = "";
        public string ProjectDir { get; set; } = "";
        /// <summary>Write time (UTC ticks) of the project DLL when the build started; 0 = there was none.</summary>
        public long DllWriteTicksAtStart { get; set; }
        public long StartedUtcTicks { get; set; }
        /// <summary>Which load of the assembly started the build (<see cref="ProjectBuildTracker"/>'s per-load id).</summary>
        public string LoadId { get; set; } = "";

        public string Serialize()
        {
            var b = new StringBuilder();
            void Line(string key, string value) => b.Append(key).Append('=').Append(value.Replace("\r", "").Replace("\n", " ")).Append('\n');
            Line(nameof(RequestId), RequestId);
            Line(nameof(ProcessId), ProcessId.ToString(CultureInfo.InvariantCulture));
            Line(nameof(LogPath), LogPath);
            Line(nameof(ProjectDir), ProjectDir);
            Line(nameof(DllWriteTicksAtStart), DllWriteTicksAtStart.ToString(CultureInfo.InvariantCulture));
            Line(nameof(StartedUtcTicks), StartedUtcTicks.ToString(CultureInfo.InvariantCulture));
            Line(nameof(LoadId), LoadId);
            return b.ToString();
        }

        /// <summary>The record in <paramref name="text"/>, or null when it is missing or unreadable (no request id).</summary>
        public static BuildRecord? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var values = new Dictionary<string, string>();
            foreach (var line in text!.Split('\n'))
                if (line.TrimEnd('\r').Split(new[] { '=' }, 2) is { Length: 2 } pair && pair[0].Length > 0)
                    values[pair[0]] = pair[1];
            string Get(string key) => values.TryGetValue(key, out var v) ? v : "";
            long Long(string key) => long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
            var record = new BuildRecord
            {
                RequestId = Get(nameof(RequestId)),
                ProcessId = (int)Long(nameof(ProcessId)),
                LogPath = Get(nameof(LogPath)),
                ProjectDir = Get(nameof(ProjectDir)),
                DllWriteTicksAtStart = Long(nameof(DllWriteTicksAtStart)),
                StartedUtcTicks = Long(nameof(StartedUtcTicks)),
                LoadId = Get(nameof(LoadId)),
            };
            return record.RequestId.Length == 0 ? null : record;
        }
    }

    /// <summary>The outcome of a finished <c>dotnet build</c>, read from its MSBuild file log.</summary>
    public sealed class BuildOutcome
    {
        BuildOutcome(bool succeeded, IReadOnlyList<string> warnings, IReadOnlyList<string> errors, string? tail)
        {
            Succeeded = succeeded;
            WarningList = warnings;
            Errors = errors;
            Tail = tail;
        }

        public const int MaxErrors = 20;

        /// <summary>The most warnings a result lists (the count covers all of them).</summary>
        public const int MaxWarnings = 20;

        public bool Succeeded { get; }
        public int Warnings => WarningList.Count;
        /// <summary>The distinct compiler warnings, project folder cut from their paths (an agent asked to fix warnings needs them).</summary>
        public IReadOnlyList<string> WarningList { get; }
        /// <summary>The distinct compiler errors (MSBuild prints each twice), project folder cut from their paths.</summary>
        public IReadOnlyList<string> Errors { get; }
        /// <summary>The log's last lines when the build failed without a compiler error (an MSBuild / SDK failure).</summary>
        public string? Tail { get; }

        static readonly Regex Diagnostic = new Regex(@":\s+(error|warning)\s+[A-Z]+\d+\s*:", RegexOptions.Compiled);
        static readonly Regex ProjectSuffix = new Regex(@"\s*\[[^\[\]]+\.csproj\]$", RegexOptions.Compiled);

        /// <summary>
        /// The outcome of the build that wrote <paramref name="log"/> (MSBuild's file logger with <c>Summary</c>), or null
        /// while it has not finished writing it (no "Build succeeded." / "Build FAILED." line yet).
        /// </summary>
        public static BuildOutcome? FromLog(string? log, string projectDir = "")
        {
            var lines = (log ?? "").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var succeeded = lines.Contains("Build succeeded.");
            if (!succeeded && !lines.Contains("Build FAILED."))
                return null;
            var errors = Diagnostics(lines, "error", projectDir);
            var tail = !succeeded && errors.Count == 0 ? string.Join("\n", lines.Skip(Math.Max(0, lines.Count - 10))) : null;
            return new BuildOutcome(succeeded, Diagnostics(lines, "warning", projectDir), errors, tail);
        }

        /// <summary>The text an agent gets for a failed build: the errors (at most <see cref="MaxErrors"/>) or the log tail.</summary>
        public string FailureText()
        {
            if (Errors.Count == 0)
                return "C# build failed:\n" + (Tail ?? "(no output)");
            return $"C# build failed with {Count(Errors.Count, "error")} (the editor keeps the previous assembly):\n" + Listed(Errors, MaxErrors);
        }

        public string WarningsText => Count(Warnings, "warning");

        /// <summary>The warnings (at most <see cref="MaxWarnings"/>) as lines to append to a success text; empty when there are none.</summary>
        public string WarningLines => Warnings == 0 ? "" : "\nWarnings:\n" + Listed(WarningList, MaxWarnings);

        /// <summary>The text an agent gets for a successful build: the count, <paramref name="rest"/>, then the warnings.</summary>
        public string SuccessText(string rest) => $"C# build succeeded ({WarningsText}){rest}" + WarningLines;

        static string Listed(IReadOnlyList<string> items, int max)
        {
            var shown = items.Take(max).ToList();
            var more = items.Count > shown.Count ? $"\n… and {items.Count - shown.Count} more" : "";
            return string.Join("\n", shown) + more;
        }

        static string Count(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";

        // "C:\proj\Player.cs(3,5): error CS1002: ; expected [C:\proj\Game.csproj]" -> "Player.cs(3,5): error CS1002: ; expected".
        static List<string> Diagnostics(List<string> lines, string kind, string projectDir)
        {
            var dir = projectDir.Replace('\\', '/').TrimEnd('/');
            return lines.Where(l => Diagnostic.Match(l) is { Success: true } m && m.Groups[1].Value == kind)
                .Select(raw =>
                {
                    var line = ProjectSuffix.Replace(raw, "").Replace('\\', '/');
                    return dir.Length > 0 && line.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase) ? line.Substring(dir.Length + 1) : line;
                })
                .Distinct().ToList();
        }
    }
}
