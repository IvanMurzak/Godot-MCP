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
    /// Configurator for Antigravity. Global (user-level) config at <c>&lt;home&gt;/.gemini/config/mcp_config.json</c>,
    /// servers under <c>mcpServers</c>. Antigravity's HTTP entry keys the server URL under <c>serverUrl</c> (not the
    /// standard <c>url</c>) that the addon's shared writer emits, so this configurator is SNIPPET-ONLY
    /// (<see cref="ConfigFilePath"/> returns null) — the dock shows the copyable snippet for the user to adapt
    /// rather than one-click writing an entry Antigravity would not read. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class AntigravityConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Antigravity";
        public override string AgentId => "antigravity";
        public override string? IconFileName => "antigravity-64.png";
        public override string DownloadUrl => "https://antigravity.google/download";

        public override string? Description =>
            "Antigravity reads MCP servers from a global '~/.gemini/config/mcp_config.json' shared across all projects. Copy the snippet below — Antigravity keys the server URL under 'serverUrl', so AI Game Developer cannot auto-write it.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Open (or create) the file '~/.gemini/config/mcp_config.json'.",
            "Copy and paste the JSON snippet above into it, under the 'mcpServers' section. Antigravity uses 'serverUrl' instead of 'url' for the HTTP endpoint — adapt the entry accordingly.",
            "Restart Antigravity to apply the configuration.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the MCP configuration file has no JSON syntax errors.",
            "- The configuration is global and shared across all projects.",
            "- Restart Antigravity after configuration changes.",
        };

        /// <summary>Null = snippet-only; Antigravity keys the URL under 'serverUrl', not the addon's standard 'url'.</summary>
        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) => null;
    }
}
