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
    /// Configurator for Visual Studio Code (MCP). Project-local config at <c>&lt;projectRoot&gt;/.vscode/mcp.json</c>.
    /// UNLIKE the other agents, VS Code lists servers under <c>servers</c> (NOT <c>mcpServers</c>) — overridden via
    /// <see cref="BodyPath"/>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class VisualStudioCodeConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Visual Studio Code";
        public override string AgentId => "vscode";
        public override string DownloadUrl => "https://code.visualstudio.com/";

        /// <summary>VS Code's MCP config nests servers under <c>servers</c>, not the usual <c>mcpServers</c>.</summary>
        public override string BodyPath => "servers";

        public override string? Description =>
            "VS Code's GitHub Copilot operates as an AI agent in the IDE, reading MCP servers from '.vscode/mcp.json'.";

        public override string? WarningText =>
            "IMPORTANT: You must start the 'ai-game-developer' MCP server manually in VS Code each time after VS Code restarts.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.vscode/mcp.json' (or copy the snippet manually).",
            "Click 'Extensions' in VS Code.",
            "Open the 'MCP SERVERS - INSTALLED' category in the extensions list.",
            "Click the settings icon next to 'ai-game-developer'.",
            "Click 'Start Server'. Done — the MCP server is running and Godot should connect.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- The '.vscode/mcp.json' file must have no JSON syntax errors.",
            "- Restart VS Code after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.VisualStudioCode(projectRoot);
    }
}
