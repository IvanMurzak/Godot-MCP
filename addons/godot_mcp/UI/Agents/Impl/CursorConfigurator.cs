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
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.UI.Agents.Impl
{
    /// <summary>
    /// Configurator for Cursor. Project-local config at <c>&lt;projectRoot&gt;/.cursor/mcp.json</c>, servers under
    /// <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class CursorConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Cursor";
        public override string AgentId => "cursor";
        public override string DownloadUrl => "https://www.cursor.com/";

        public override string? Description =>
            "Cursor reads MCP servers from a project-local '.cursor/mcp.json'.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.cursor/mcp.json' (or copy the snippet manually).",
            "Open Cursor in this project; it picks up the server automatically.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- The '.cursor/mcp.json' file must have no JSON syntax errors.",
            "- Open Cursor settings → 'MCP Servers' to restart ai-game-developer or inspect the available MCP tools and server status.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.Cursor(projectRoot);
    }
}
