/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Console
    {
        public const string ConsoleClearLogsToolId = "console-clear-logs";

        [AiTool
        (
            ConsoleClearLogsToolId,
            Title = "Console / Clear Logs",
            DestructiveHint = true,
            IdempotentHint = true
        )]
        [Description("Clear the captured Godot-MCP log cache (read by 'console-get-logs'). Useful for " +
            "isolating logs to a specific action by clearing the slate first. NOTE: Godot's C# API exposes " +
            "no managed hook to clear the editor's own Output panel, so (unlike Unity's analog) this clears " +
            "only the MCP-side cache, not the Godot editor console window.")]
        public void ClearLogs(string? nothing = null)
        {
            GodotLogCollector.GetOrCreate().Clear();
        }
    }
}
