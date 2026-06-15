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
    /// Configurator for Kilo Code. Project-local config at <c>&lt;projectRoot&gt;/.kilocode/mcp.json</c>, servers
    /// under <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class KiloCodeConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Kilo Code";
        public override string AgentId => "kilo-code";
        public override string? IconFileName => "kilo-code-64.png";
        public override string DownloadUrl => "https://app.kilo.ai/get-started";

        public override string? Description =>
            "Kilo Code reads MCP servers from a project-local '.kilocode/mcp.json'.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.kilocode/mcp.json' (or copy the snippet manually).",
            "Restart Kilo Code if it was running.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the JSON file has no syntax errors.",
            "- Verify Kilo Code has MCP support enabled.",
            "- The configuration file should be in your Godot project root (the folder with 'project.godot').",
            "- Restart Kilo Code after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.KiloCode(projectRoot);
    }
}
