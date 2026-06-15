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
    /// Configurator for Zoo Code. Project-local config at <c>&lt;projectRoot&gt;/.roo/mcp.json</c>, servers under
    /// <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class ZooCodeConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Zoo Code";
        public override string AgentId => "zoo-code";
        public override string? IconFileName => "zoo-code-64.png";
        public override string DownloadUrl => "https://www.zoocode.dev/";

        public override string? Description =>
            "Zoo Code reads MCP servers from a project-local '.roo/mcp.json'.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.roo/mcp.json' (or copy the snippet manually).",
            "Restart Zoo Code if it was running.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the JSON file has no syntax errors.",
            "- Verify Zoo Code has MCP support enabled.",
            "- The configuration file should be in your Godot project root (the folder with 'project.godot').",
            "- Restart Zoo Code after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.ZooCode(projectRoot);
    }
}
