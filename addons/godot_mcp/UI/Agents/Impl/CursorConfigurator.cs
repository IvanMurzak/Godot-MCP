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
    /// Configurator for Cursor. Project-local config at <c>&lt;projectRoot&gt;/.cursor/mcp.json</c>, servers under
    /// <c>mcpServers</c>. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class CursorConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Cursor";
        public override string AgentId => "cursor";
        public override string DownloadUrl => "https://www.cursor.com/";

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.Cursor(projectRoot);
    }
}
