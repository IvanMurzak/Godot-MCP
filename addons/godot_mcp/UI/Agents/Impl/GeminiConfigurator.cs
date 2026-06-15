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
    /// Configurator for Gemini. Project-local config at <c>&lt;projectRoot&gt;/.gemini/settings.json</c>, servers
    /// under <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class GeminiConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Gemini";
        public override string AgentId => "gemini";
        public override string? IconFileName => "gemini-64.png";
        public override string DownloadUrl => "https://geminicli.com/docs/get-started/installation/";

        public override string? Description =>
            "Gemini CLI reads MCP servers from a project-local '.gemini/settings.json'.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.gemini/settings.json' (or use the CLI command below).",
            "Alternatively, run this command in the folder of the Godot project to configure Gemini: gemini mcp add --transport http ai-game-developer <mcp-url>",
            "Start Gemini with the debug flag: gemini --debug",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the Gemini CLI is installed and accessible from the terminal.",
            "- Ensure the MCP configuration file has no JSON syntax errors.",
            "- Restart Gemini after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.Gemini(projectRoot);
    }
}
