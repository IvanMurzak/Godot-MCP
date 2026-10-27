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
using System.Linq;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The outcome of <c>editor | call BuildProject()</c> from <c>dotnet build</c>'s exit code and output: success with the
    /// warning count, or the build's distinct errors (MSBuild prints each twice, inline and in the summary), at most
    /// <see cref="MaxErrors"/>, with the project folder cut from their paths. Pure (unit-tested).
    /// </summary>
    public static class BuildOutputText
    {
        public const int MaxErrors = 20;

        private static readonly Regex Diagnostic = new Regex(@":\s+(error|warning)\s+[A-Z]+\d+\s*:", RegexOptions.Compiled);

        public static string Summarize(int exitCode, string? output, string projectDir = "")
        {
            var lines = (output ?? "").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var errors = Distinct(lines.Where(l => Diagnostic.Match(l) is { Success: true } m && m.Groups[1].Value == "error"), projectDir);
            var warnings = Distinct(lines.Where(l => Diagnostic.Match(l) is { Success: true } m && m.Groups[1].Value == "warning"), projectDir);
            if (exitCode == 0)
                return $"build succeeded ({warnings.Count} warning{(warnings.Count == 1 ? "" : "s")}); the editor reloads the C# assembly next — " +
                       "new or changed C# scripts and [GlobalClass] types are usable a few seconds later";
            if (errors.Count == 0)
            {
                // No compiler diagnostic: MSBuild / SDK failures print "error MSB…" or plain lines; show the last ones.
                var tail = lines.Skip(Math.Max(0, lines.Count - 10));
                throw new InvalidOperationException($"build failed (exit code {exitCode}):\n" + string.Join("\n", tail));
            }
            var shown = errors.Take(MaxErrors).ToList();
            var more = errors.Count > shown.Count ? $"\n… and {errors.Count - shown.Count} more" : "";
            throw new InvalidOperationException($"build failed with {errors.Count} error{(errors.Count == 1 ? "" : "s")}:\n" + string.Join("\n", shown) + more);
        }

        // "C:\proj\Player.cs(3,5): error CS1002: ; expected [C:\proj\Game.csproj]" -> "Player.cs(3,5): error CS1002: ; expected"
        private static List<string> Distinct(IEnumerable<string> lines, string projectDir)
        {
            var dir = projectDir.Replace('\\', '/').TrimEnd('/');
            var seen = new List<string>();
            foreach (var raw in lines)
            {
                var line = Regex.Replace(raw, @"\s*\[[^\[\]]+\.csproj\]$", "").Replace('\\', '/');
                if (dir.Length > 0 && line.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase)) line = line.Substring(dir.Length + 1);
                if (!seen.Contains(line)) seen.Add(line);
            }
            return seen;
        }
    }
}
#endif
