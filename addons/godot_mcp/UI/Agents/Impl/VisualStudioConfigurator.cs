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
    /// Configurator for Visual Studio (Copilot). Project-local config at <c>&lt;projectRoot&gt;/.vs/mcp.json</c>.
    /// Like VS Code, Visual Studio lists servers under <c>servers</c> (NOT <c>mcpServers</c>) — overridden via
    /// <see cref="BodyPath"/>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class VisualStudioConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Visual Studio (Copilot)";
        public override string AgentId => "vs-copilot";
        public override string? IconFileName => "visual-studio-64.png";
        public override string DownloadUrl => "https://visualstudio.microsoft.com/downloads/";
        public override string? TutorialUrl => "https://www.youtube.com/watch?v=RGdak4T69mc";

        /// <summary>Visual Studio's MCP config nests servers under <c>servers</c>, not the usual <c>mcpServers</c>.</summary>
        public override string BodyPath => "servers";

        public override string? Description =>
            "Visual Studio's GitHub Copilot operates as an AI agent in the IDE, reading MCP servers from '.vs/mcp.json'. Visual Studio starts the MCP server after the first prompt.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.vs/mcp.json' at the project root (or copy the snippet manually).",
            "Open Visual Studio in this project; it starts the MCP server after the first prompt is sent.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- The '.vs/mcp.json' file must have no JSON syntax errors.",
            "- Godot may stay 'Connecting…' until the first prompt is processed.",
            "- Restart Visual Studio after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.VisualStudio(projectRoot);
    }
}
