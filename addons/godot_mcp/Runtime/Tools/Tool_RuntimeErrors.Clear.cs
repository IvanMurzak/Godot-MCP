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
    public partial class Tool_RuntimeErrors
    {
        public const string RuntimeErrorsClearToolId = "runtime-errors-clear";

        [AiTool
        (
            RuntimeErrorsClearToolId,
            Title = "Runtime Errors / Clear",
            DestructiveHint = true,
            IdempotentHint = true
        )]
        [Description("Clear the captured in-game runtime-error buffer (read by 'runtime-errors-get'). Useful " +
            "for isolating errors to a specific in-game action by clearing the slate first, then performing " +
            "the action, then polling 'runtime-errors-get'. NOTE: the monotonic sequence counter is NOT reset, " +
            "so a 'sinceSequence' poll from before the clear still behaves correctly (no old rows reappear). " +
            "A no-op when runtime error capture is not enabled in this game.")]
        // The unused 'nothing' parameter is the repo-wide idiom for a zero-input [AiTool] method (mirrors
        // Tool_Console.ClearLogs / Tool_Editor.GetState / Tool_Scene.ListOpened): the tool-schema generator
        // expects at least one parameter, so an optional defaulted no-op stands in for "takes no input".
        public void Clear(string? nothing = null)
        {
            RuntimeErrorCollector.Current?.Clear();
        }
    }
}
