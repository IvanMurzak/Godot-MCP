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
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Script
    {
        public const string ScriptReadToolId = "script-read";

        [AiTool
        (
            ScriptReadToolId,
            Title = "Script / Read",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Read a Godot script file (C# '.cs' or GDScript '.gd') under res:// and return its " +
            "content + metadata (res:// path, language, line count). Supports an optional 1-based " +
            "'lineFrom'/'lineTo' slice for partial reads (indices are clamped — out-of-range values read " +
            "at-most the whole file). Pair with '" + ScriptUpdateToolId + "'/'" + ScriptCreateToolId +
            "' to write back. The file is read with Godot's FileAccess so it works on any res:// path " +
            "(imported or not). Returns a structured ScriptInfo.")]
        public ScriptInfo Read
        (
            [Description("res:// path of the script to read, e.g. 'res://scripts/player.gd' or " +
                "'res://scripts/Enemy.cs'. Must end in '.cs' or '.gd'.")]
            string scriptPath,
            [Description("1-based start line of the slice (default 1). Clamped into range.")]
            int lineFrom = 1,
            [Description("1-based inclusive end line of the slice (default -1 = end of file). Clamped into range.")]
            int lineTo = -1
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var resPath = ScriptLang_.RequireScriptResPath(scriptPath, nameof(scriptPath), out var lang);

                if (!FileAccess.FileExists(resPath))
                    throw new System.IO.FileNotFoundException($"No script file exists at '{resPath}'.", resPath);

                using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
                if (file == null)
                    throw new Exception($"Failed to open '{resPath}' for reading: {FileAccess.GetOpenError()}.");

                var text = file.GetAsText();

                // Slice [lineFrom..lineTo] (inclusive, 1-based), clamping like Unity-MCP's script-read so an
                // out-of-range request is forgiving (reads at-most the whole file). Split on '\n' after
                // normalizing CRLF so a Windows-authored file slices by logical line, not by raw bytes.
                var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

                var from = lineFrom;
                var to = lineTo;
                if (from < 1 || from > lines.Length)
                    from = 1;
                if (to == -1 || to > lines.Length)
                    to = lines.Length;
                if (to < 1)
                    to = lines.Length;
                if (from > to)
                    from = to;

                var startIndex = from - 1;
                var count = to - from + 1;
                var slice = new string[count];
                Array.Copy(lines, startIndex, slice, 0, count);
                var content = string.Join("\n", slice);

                return new ScriptInfo
                {
                    ResourcePath = resPath,
                    Language = lang.ToString(),
                    Content = content,
                    LineCount = count,
                    Status = "Script read.",
                };
            });
        }
    }
}
#endif
