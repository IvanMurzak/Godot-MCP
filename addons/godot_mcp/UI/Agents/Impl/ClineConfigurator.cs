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
    /// Configurator for Cline (VS Code extension). Global config inside VS Code's globalStorage (per-OS:
    /// Windows <c>%APPDATA%/Code/...</c>, macOS <c>~/Library/Application Support/Code/...</c>, Linux
    /// <c>~/.config/Code/...</c>) → <c>cline_mcp_settings.json</c>, servers under <c>mcpServers</c>. Pure-managed —
    /// CI-unit-tested via the registry + path resolver.
    /// </summary>
    public sealed class ClineConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Cline";
        public override string AgentId => "cline";
        public override string? IconFileName => "cline-64.png";
        public override string DownloadUrl => "https://cline.bot/";

        public override string? Description =>
            "Cline is a VS Code extension that reads MCP servers from a global 'cline_mcp_settings.json'.";

        public override string? WarningText =>
            "IMPORTANT: Cline uses a global configuration shared across all projects.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into Cline's global 'cline_mcp_settings.json' (or copy the snippet manually).",
            "Restart VS Code or reload the Cline extension.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the configuration file has no JSON syntax errors.",
            "- Open Cline settings in VS Code, go to 'MCP Servers' to check the server status.",
            "- The configuration is global and shared across all VS Code projects.",
            "- Restart VS Code after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.Cline(os, home, appData);
    }
}
