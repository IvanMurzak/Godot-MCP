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
    /// Configurator for Claude Desktop. Per-OS user config (Windows <c>%APPDATA%/Claude/claude_desktop_config.json</c>,
    /// macOS <c>~/Library/Application Support/Claude/...</c>, Linux <c>~/.config/Claude/...</c>), servers under
    /// <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry + path resolver.
    /// </summary>
    public sealed class ClaudeDesktopConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Claude Desktop";
        public override string AgentId => "claude-desktop";
        public override string DownloadUrl => "https://claude.ai/download";

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.ClaudeDesktop(os, home, appData);
    }
}
