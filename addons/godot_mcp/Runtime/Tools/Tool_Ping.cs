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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Lightweight readiness/connectivity probe. The single tool that proves the end-to-end
    /// MCP path (editor boot → SignalR connect → tool dispatch → structured result) is wired up,
    /// before any engine-driving tool families are added. The Godot analog of a plain echo/pong tool.
    ///
    /// <para>
    /// This is engine-runtime logic with no Godot editor API surface, so it intentionally lives
    /// OUTSIDE <c>#if TOOLS</c> — it is discovered by the McpPlugin assembly scanner and returns a
    /// pure-managed value, needing no main-thread marshalling.
    /// </para>
    ///
    /// <para>
    /// SYSTEM tool (<see cref="McpToolType.System"/>), matching the Unity-MCP reference. The owner ruling
    /// of 2026-07-25 is that liveness/skill tooling must be reachable on the SAME surface across all three
    /// engines: <c>McpPluginBuilder</c> partitions tools by <c>ToolType</c> into two DISJOINT registries —
    /// Standard tools go to the MCP tool manager (and are advertised to AI agents), System tools go to the
    /// system-tool manager and are reachable only over the HTTP <c>/api/system-tools/</c> surface. Leaving
    /// <c>ToolType</c> unset here defaulted <c>ping</c> to Standard, so a client probing the system surface
    /// got "tool not found" while the engine happily listed it on the other one.
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Ping
    {
        public const string PingToolId = "ping";

        [AiTool
        (
            PingToolId,
            Title = "Ping",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false,
            ToolType = McpToolType.System
        )]
        [Description("Lightweight readiness probe for the Godot-MCP connection. Returns the input " +
            "'message' echoed back, or 'pong' when omitted. Useful for verifying SignalR connectivity " +
            "end-to-end after the editor plugin connects to the MCP server.")]
        public string Ping
        (
            [Description("Optional message to echo back. When null or empty, the tool returns 'pong'.")]
            string? message = null
        )
        {
            return string.IsNullOrEmpty(message) ? "pong" : message;
        }
    }
}
