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
using McpClientData = com.IvanMurzak.McpPlugin.Common.Model.McpClientData;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// PURE-MANAGED (no Godot native types, no <c>#if TOOLS</c>) presentation rules for the dock's "AI agent"
    /// timeline point when it reflects LIVE connected MCP clients (Copilot / Claude / Cursor / …) reported by the
    /// reused client's <c>IMcpManager.ActiveClients</c> / <c>OnClientsChanged</c>. Kept out of the editor-only
    /// <see cref="ConnectionPanel"/> so the dot-state / summary / per-agent label decisions are unit-tested in the
    /// plain-xUnit <c>Godot-MCP.Tests</c> host (mirrors <see cref="ConnectionPanelView"/>).
    /// </summary>
    public static class AgentSessionView
    {
        /// <summary>
        /// The AI-agent point's circle state from the connected-agent count: a filled green disc
        /// (<see cref="ConnectionPanelView.TimelinePointState.Online"/>) when ≥1 agent is connected, otherwise the
        /// neutral orange disc. (Unlike the Godot/MCP-server points there is no "connecting" ring — an agent is
        /// either present in the active-client list or not.)
        /// </summary>
        public static ConnectionPanelView.TimelinePointState DotState(int connectedCount) =>
            connectedCount > 0
                ? ConnectionPanelView.TimelinePointState.Online
                : ConnectionPanelView.TimelinePointState.Disconnected;

        /// <summary>
        /// The muted suffix shown beside the underlined "AI agent" label: "(connects on demand)" when none are
        /// connected, "(1 connected)" for one, "(N connected)" for many. A negative/zero count is treated as none.
        /// </summary>
        public static string Summary(int connectedCount) =>
            connectedCount <= 0 ? "(connects on demand)"
            : connectedCount == 1 ? "(1 connected)"
            : $"({connectedCount} connected)";

        /// <summary>
        /// A human-readable display name for one connected agent: prefer <see cref="McpClientData.ClientTitle"/>,
        /// then <see cref="McpClientData.ClientName"/>, then a short form of the <see cref="McpClientData.SessionId"/>,
        /// falling back to "AI agent". Never returns null/empty. Whitespace-only fields are skipped.
        /// </summary>
        public static string DisplayName(McpClientData? agent)
        {
            if (agent == null)
                return "AI agent";
            if (!string.IsNullOrWhiteSpace(agent.ClientTitle))
                return agent.ClientTitle!.Trim();
            if (!string.IsNullOrWhiteSpace(agent.ClientName))
                return agent.ClientName!.Trim();
            if (!string.IsNullOrWhiteSpace(agent.SessionId))
            {
                var id = agent.SessionId!.Trim();
                return "session " + (id.Length > 8 ? id.Substring(0, 8) : id);
            }
            return "AI agent";
        }

        /// <summary>
        /// One agent's list-row label: the <see cref="DisplayName"/> plus the version in parentheses when present —
        /// e.g. "GitHub Copilot (1.2.0)" or "Claude". Mirrors Unity-MCP's per-agent line.
        /// </summary>
        public static string RowLabel(McpClientData? agent)
        {
            var name = DisplayName(agent);
            return agent != null && !string.IsNullOrWhiteSpace(agent.ClientVersion)
                ? $"{name} ({agent.ClientVersion!.Trim()})"
                : name;
        }
    }
}
