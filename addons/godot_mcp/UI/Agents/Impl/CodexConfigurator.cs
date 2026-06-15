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
    /// Configurator for Codex. Codex's config file is TOML (<c>&lt;projectRoot&gt;/.codex/config.toml</c>, servers
    /// under <c>[mcp_servers.*]</c>), NOT JSON — the addon's shared READ-MERGE-WRITE writer only handles JSON, so
    /// this configurator is SNIPPET-ONLY (<see cref="ConfigFilePath"/> returns null). The dock shows the copyable
    /// JSON snippet for reference, but the recommended path is the <c>codex mcp add</c> CLI command in the manual
    /// steps. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class CodexConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Codex";
        public override string AgentId => "codex";
        public override string? IconFileName => "codex-64.png";
        public override string DownloadUrl => "https://openai.com/codex/";

        public override string? Description =>
            "Codex reads MCP servers from a TOML config ('.codex/config.toml'). Use the CLI command below — AI Game Developer cannot auto-write Codex's TOML format.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Open a terminal in the folder of the Godot project (the folder with 'project.godot').",
            "Run this command to configure Codex: codex mcp add ai-game-developer --url <mcp-url>",
            "If authorization is enabled, append: --bearer-token-env-var=GAME_DEV_AUTH_TOKEN  (and set that environment variable before starting Codex).",
            "Start Codex: codex",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the Codex CLI is installed and accessible from the terminal.",
            "- The Codex config file '.codex/config.toml' is TOML; the snippet above is JSON for reference only.",
            "- Restart Codex after configuration changes.",
        };

        /// <summary>Null = snippet-only; Codex's config is TOML, which the addon's JSON writer cannot produce.</summary>
        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) => null;
    }
}
