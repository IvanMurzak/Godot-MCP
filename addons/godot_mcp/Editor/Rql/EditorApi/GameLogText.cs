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
using System.Text;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>One entry of a game's log: what it printed, or an error / warning with the lines that locate it.</summary>
    public sealed class GameLogEntry
    {
        public GameLogEntry(int sequence, string logType, string message, string stackTrace)
        {
            Sequence = sequence; LogType = logType; Message = message; StackTrace = stackTrace;
        }

        [global::Rql.RqlDescription("position in the log, 1 = first entry")]
        public int Sequence { get; }

        [global::Rql.RqlDescription("Log (print), Error (ERROR, SCRIPT ERROR, USER ERROR, C# exceptions) or Warning")]
        public string LogType { get; }

        [global::Rql.RqlDescription("the printed text, or the error / warning message")]
        public string Message { get; }

        [global::Rql.RqlDescription("for an error or warning: where it happened (\"at: …\" and the GDScript backtrace), else empty")]
        public string StackTrace { get; }
    }

    /// <summary>
    /// The text Godot's file logger writes (<c>user://logs/godot.log</c> of the game, the run started from the editor
    /// included) as entries: each <c>ERROR:</c> / <c>SCRIPT ERROR:</c> / <c>USER ERROR:</c> / <c>WARNING:</c> line is one
    /// entry with its indented continuation lines (<c>at: …</c>, the GDScript backtrace) as its stack trace; every other line
    /// is a print. The engine banner (the first lines up to the first blank line) is skipped. Pure (unit-tested).
    /// </summary>
    public static class GameLogText
    {
        private static readonly (string prefix, string type)[] Kinds =
        {
            ("SCRIPT ERROR:", "Error"), ("USER SCRIPT ERROR:", "Error"), ("USER ERROR:", "Error"), ("ERROR:", "Error"),
            ("SHADER ERROR:", "Error"), ("USER WARNING:", "Warning"), ("USER SCRIPT WARNING:", "Warning"), ("SCRIPT WARNING:", "Warning"),
            ("WARNING:", "Warning"),
        };

        public static List<GameLogEntry> Parse(string? text)
        {
            var entries = new List<GameLogEntry>();
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            int i = 0;
            // The banner: "Godot Engine v4.5.1.stable…", the renderer line(s) ("Vulkan 1.4 - Forward+ - Using Device …"), a blank line.
            if (lines.Length > 0 && lines[0].StartsWith("Godot Engine v", StringComparison.Ordinal))
            {
                i = 1;
                while (i < lines.Length && IsRendererLine(lines[i])) i++;
                if (i < lines.Length && lines[i].Length == 0) i++;
            }
            string? type = null, message = null;
            var trace = new StringBuilder();
            void Flush()
            {
                if (message != null) entries.Add(new GameLogEntry(entries.Count + 1, type!, message, trace.ToString().TrimEnd('\n')));
                type = null; message = null; trace.Clear();
            }
            for (; i < lines.Length; i++)
            {
                var line = lines[i];
                if (type is "Error" or "Warning" && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    trace.Append(line.Trim()).Append('\n');
                    continue;
                }
                Flush();
                if (line.Length == 0 && i == lines.Length - 1) break; // the file's final newline
                var kind = Kind(line);
                if (kind != null)
                {
                    type = kind.Value.type;
                    message = line.Substring(kind.Value.prefix.Length).Trim();
                }
                else
                {
                    type = "Log";
                    message = line;
                }
            }
            Flush();
            return entries;
        }

        private static bool IsRendererLine(string line) =>
            System.Text.RegularExpressions.Regex.IsMatch(line, @"^(Vulkan|OpenGL|D3D12|Metal|Dummy)\b.* - ");

        private static (string prefix, string type)? Kind(string line)
        {
            foreach (var k in Kinds)
                if (line.StartsWith(k.prefix, StringComparison.Ordinal)) return k;
            return null;
        }
    }
}
#endif
