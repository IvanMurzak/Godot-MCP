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
    /// Configurator for any "Other / Custom" MCP client. Has NO config file the addon can write
    /// (<see cref="ConfigFilePath"/> returns null) — the panel shows the copyable HTTP snippet only, with no
    /// Configure/Remove buttons. Always LAST in the registry. Pure-managed — CI-unit-tested via the registry.
    /// </summary>
    public sealed class CustomConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Other - Custom";
        public override string AgentId => "other-custom";
        public override string DownloadUrl => "https://modelcontextprotocol.io/";

        public override string? Description =>
            "Any other MCP client. Copy the HTTP snippet below into your client's own MCP server config — AI Game Developer cannot auto-write it.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Open (or create) your MCP client's server configuration.",
            "Copy and paste the JSON snippet above into it, under the client's MCP servers section.",
            "Restart or reload the client to pick up the new server.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- The snippet uses the HTTP transport (type: http) pointing at the same server this plugin connects to.",
            "- If your client requires a bearer token, the Authorization header carries it — keep it secret.",
        };

        /// <summary>Null = snippet-only; no on-disk config the addon can read/write for an arbitrary client.</summary>
        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) => null;
    }
}
