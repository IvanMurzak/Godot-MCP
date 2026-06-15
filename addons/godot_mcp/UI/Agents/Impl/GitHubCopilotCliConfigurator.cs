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
    /// Configurator for GitHub Copilot CLI. Project-local config at <c>&lt;projectRoot&gt;/.mcp.json</c> (the same
    /// file Claude Code uses — Copilot CLI v1.0.12+ discovers workspace-local MCP configs from the working
    /// directory up to the git root), servers under <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class GitHubCopilotCliConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "GitHub Copilot CLI";
        public override string AgentId => "github-copilot-cli";
        public override string? IconFileName => "github-copilot-64.png";
        public override string DownloadUrl => "https://github.com/features/copilot/cli";

        public override string? Description =>
            "GitHub Copilot CLI reads MCP servers from a project-local '.mcp.json' (shared with Claude Code).";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.mcp.json' at the project root (or copy the snippet manually).",
            "Open a terminal in the project root and launch GitHub Copilot CLI: copilot",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure Copilot CLI is launched from the Godot project root (the folder containing '.mcp.json' and 'project.godot').",
            "- Requires GitHub Copilot CLI v1.0.12+, which discovers '.mcp.json' at project level.",
            "- Ensure the MCP configuration file has no JSON syntax errors.",
            "- Restart Copilot CLI after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.GitHubCopilotCli(projectRoot);
    }
}
