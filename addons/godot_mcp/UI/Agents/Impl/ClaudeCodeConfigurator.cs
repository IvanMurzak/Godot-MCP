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
    /// Configurator for Claude Code. Project-local config at <c>&lt;projectRoot&gt;/.mcp.json</c>, servers under
    /// <c>mcpServers</c>. Pure-managed (no Godot native types, no <c>#if TOOLS</c>) — CI-unit-tested via the registry.
    /// </summary>
    public sealed class ClaudeCodeConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Claude Code";
        public override string AgentId => "claude-code";
        public override string DownloadUrl => "https://docs.anthropic.com/en/docs/claude-code/overview";

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.ClaudeCode(projectRoot);
    }
}
