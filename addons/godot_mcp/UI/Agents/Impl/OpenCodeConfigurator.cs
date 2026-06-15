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
    /// Configurator for Open Code. Project-local config at <c>&lt;projectRoot&gt;/opencode.json</c>, servers under
    /// the <c>mcp</c> key. Open Code's remote-server entry shape is structurally different from the addon's standard
    /// <c>{type:http,url,headers}</c> form — it expects <c>{type:"remote",enabled:true,url}</c> — so the shared
    /// READ-MERGE-WRITE writer cannot produce a valid Open Code entry. This configurator is therefore SNIPPET-ONLY
    /// (<see cref="ConfigFilePath"/> returns null): the dock shows the copyable snippet (under <c>mcp</c>) for the
    /// user to adapt, rather than one-click writing a config Open Code would reject. Pure-managed — CI-unit-tested
    /// via the registry.
    /// </summary>
    public sealed class OpenCodeConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Open Code";
        public override string AgentId => "open-code";
        public override string? IconFileName => "open-code-64.png";
        public override string DownloadUrl => "https://opencode.ai/download";

        /// <summary>Open Code nests MCP servers under <c>mcp</c>, not the usual <c>mcpServers</c>.</summary>
        public override string BodyPath => "mcp";

        public override string? Description =>
            "Open Code reads MCP servers from a project-local 'opencode.json' (under the 'mcp' key). Copy the snippet below — Open Code's remote-server shape differs, so AI Game Developer cannot auto-write it.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Open (or create) the file 'opencode.json' in your Godot project root (the folder with 'project.godot').",
            "Copy and paste the JSON snippet above into it, under the 'mcp' section. Open Code remote servers use {\"type\":\"remote\",\"enabled\":true,\"url\":\"<mcp-url>\"} — adapt the snippet's entry accordingly.",
            "Open a terminal in the project root and start Open Code: opencode",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the Open Code CLI is installed and accessible from the terminal.",
            "- Ensure Open Code is launched from the Godot project root (the folder with 'project.godot').",
            "- Restart Open Code after configuration changes.",
        };

        /// <summary>Null = snippet-only; Open Code's remote-server shape differs from the addon's standard http entry.</summary>
        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) => null;
    }
}
