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
    /// Configurator for Rider (Junie). Project-local config at <c>&lt;projectRoot&gt;/.junie/mcp/mcp.json</c>,
    /// servers under <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class RiderConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Rider (Junie)";
        public override string AgentId => "rider-junie";
        public override string? IconFileName => "rider-64.png";
        public override string DownloadUrl => "https://www.jetbrains.com/rider/download/";

        public override string? Description =>
            "Rider's Junie agent reads MCP servers from a project-local '.junie/mcp/mcp.json'.";

        public override string? WarningText =>
            "After configuring, go to Rider Settings / Tools / Junie / MCP Settings and check 'ai-game-developer' to connect the AI agent.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.junie/mcp/mcp.json' (or copy the snippet manually).",
            "Open Rider settings: Settings / Tools / Junie / MCP Settings.",
            "Enable the 'ai-game-developer' server to connect the AI agent.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the MCP configuration file '.junie/mcp/mcp.json' has no JSON syntax errors.",
            "- Restart Rider after configuration changes.",
            "- Ensure the file is created in your Godot project root (the folder with 'project.godot').",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.Rider(projectRoot);
    }
}
