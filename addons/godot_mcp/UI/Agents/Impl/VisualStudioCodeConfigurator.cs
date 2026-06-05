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

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.VisualStudioCode(projectRoot);
    }
}
