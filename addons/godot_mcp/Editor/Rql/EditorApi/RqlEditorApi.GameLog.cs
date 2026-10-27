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
using System.Collections.Generic;
using System.IO;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        /// <summary>Most entries the <c>gameLog</c> source returns (the last ones).</summary>
        public const int MaxGameLogEntries = 2000;

        /// <summary>Most bytes of the game's log file read (its end).</summary>
        private const long MaxGameLogBytes = 4 * 1024 * 1024;

        /// <summary>
        /// The <c>gameLog</c> source: what the game run from the editor (PlayScene, isPlaying) printed, its errors and warnings,
        /// read from the log file Godot's file logger writes for the game (<c>debug/file_logging/log_path</c>,
        /// <c>user://logs/godot.log</c> by default; a new run starts a new file). The editor's Output panel gets the same lines
        /// over the debugger, which the editor's own log (the <c>console</c> source) never sees.
        /// </summary>
        public static List<GameLogEntry> GameLog()
        {
            if (!FileLogging())
                throw new InvalidOperationException("the project's file logging is off (debug/file_logging/enable_file_logging), so the game writes no log; " +
                    "turn it on with project | set settings[\"debug/file_logging/enable_file_logging\"] = true and run the game again");
            var path = GameLogPath();
            if (!File.Exists(path)) return new List<GameLogEntry>();
            string text;
            // The running game keeps the file open for writing.
            using (var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                // A game that prints every frame writes megabytes: only the tail is read, from its first whole line.
                var cut = stream.Length > MaxGameLogBytes;
                if (cut) stream.Seek(-MaxGameLogBytes, SeekOrigin.End);
                if (cut) reader.ReadLine();
                text = reader.ReadToEnd();
            }
            var entries = GameLogText.Parse(text);
            return entries.Count > MaxGameLogEntries ? entries.GetRange(entries.Count - MaxGameLogEntries, MaxGameLogEntries) : entries;
        }

        // Whether the game writes a log on this desktop: the base setting or its .pc override (on by default), which the
        // editor process itself may not apply.
        private static bool FileLogging()
        {
            const string key = "debug/file_logging/enable_file_logging";
            return ProjectSettings.GetSettingWithOverride(key).AsBool()
                   || (ProjectSettings.HasSetting(key + ".pc") ? ProjectSettings.GetSetting(key + ".pc").AsBool() : ProjectSettings.GetSetting(key).AsBool());
        }

        /// <summary>The absolute path of the game's log file.</summary>
        public static string GameLogPath()
        {
            var setting = ProjectSettings.GetSettingWithOverride("debug/file_logging/log_path").AsString();
            return ProjectSettings.GlobalizePath(string.IsNullOrWhiteSpace(setting) ? "user://logs/godot.log" : setting);
        }
    }
}
#endif
