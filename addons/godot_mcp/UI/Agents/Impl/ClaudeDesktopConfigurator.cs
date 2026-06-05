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
    /// Configurator for Claude Desktop. Per-OS user config (Windows <c>%APPDATA%/Claude/claude_desktop_config.json</c>,
    /// macOS <c>~/Library/Application Support/Claude/...</c>, Linux <c>~/.config/Claude/...</c>), servers under
    /// <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry + path resolver.
    /// </summary>
    public sealed class ClaudeDesktopConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Claude Desktop";
        public override string AgentId => "claude-desktop";
        public override string DownloadUrl => "https://claude.ai/download";

        public override string? WarningText =>
            "IMPORTANT: Highly recommended to use Claude Code instead — it shares the same subscription plan and is far more reliable with Godot.";

        public override string? Description =>
            "Claude Desktop is finicky and must be fully restarted (Quit from the tray) after the Godot connection becomes active.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into 'claude_desktop_config.json' (or copy the snippet manually).",
            "Restart Claude Desktop. You may need to click 'Quit' in the apps tray — simply closing the window is not enough.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Claude Desktop may not detect runtime updates to MCP tools; ensure it reads the MCP tools on startup.",
            "- Start Godot first; the connection status should read 'Connecting…' before you launch Claude Desktop.",
            "- Restart Claude Desktop after any configuration change.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.ClaudeDesktop(os, home, appData);
    }
}
