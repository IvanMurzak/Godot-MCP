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
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// CLI editor sessions. A windowless editor cannot receive the OS polite-quit request (WM_CLOSE), so
    /// <c>godot-cli close --owned-only</c> asks the editor itself to save and quit, as the idle shutdown does.
    /// SYSTEM tool (HTTP <c>/api/system-tools/</c> only, never advertised to agents), and it acts only on an editor
    /// the CLI started, for the caller that knows that session's lease nonce.
    /// </summary>
    [AiToolType]
    public partial class Tool_Session
    {
        public const string SessionCloseToolId = "session-close";

        /// <summary>Seconds between the answer and the quit, so the answer reaches the caller.</summary>
        const double QuitDelaySeconds = 0.5;

        [AiTool
        (
            SessionCloseToolId,
            Title = "Session / Close",
            DestructiveHint = true,
            OpenWorldHint = false,
            ToolType = McpToolType.System
        )]
        [Description("Save and quit an editor started by godot-cli (open scenes with a path and the resources they changed). " +
            "Only for that CLI session: 'nonce' must be the one in <project>/.ai-game-dev/session.json.")]
        public string Close
        (
            [Description("The session nonce from <project>/.ai-game-dev/session.json.")]
            string nonce
        )
        {
            var expected = OS.GetEnvironment(GodotMcpEnv.SessionNonce);
            if (string.IsNullOrEmpty(expected))
                throw new InvalidOperationException("This editor was not started by a godot-cli session; close it yourself.");
            if (!string.Equals(nonce, expected, StringComparison.Ordinal))
                throw new ArgumentException("'nonce' does not match this editor's session.");

            MainThreadDispatcher.Enqueue(() =>
                ((SceneTree)Engine.GetMainLoop()).CreateTimer(QuitDelaySeconds).Timeout += () => IdleShutdown.SaveAndQuit("session-close"));
            return "Saving and quitting.";
        }
    }
}
#endif
