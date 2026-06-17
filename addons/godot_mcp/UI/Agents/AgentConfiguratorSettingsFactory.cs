/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// Bridges Godot's editor/connection state into the engine-agnostic
    /// <see cref="AgentConfig.AgentConfiguratorSettings"/> consumed by the shared
    /// <c>com.IvanMurzak.McpPlugin.AgentConfig</c> module — the Godot analog of Unity-MCP's
    /// <c>AgentConfiguratorSettingsFactory</c>. This is the single place that maps Godot's live
    /// <see cref="GodotMcpConfig"/> (resolved MCP-client URL, token, connection mode, auth) and the
    /// editor's project root onto the shared settings record. The shared library detects the host OS at
    /// runtime (<c>CreateForHost</c>), so per-OS config-file paths work on Win/Mac/Linux without a
    /// compile-time branch here.
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it reads <c>Godot.OS</c>/<c>ProjectSettings</c>. The shared
    /// configurators are stateless — a fresh settings snapshot is built per render so the written config
    /// + the rendered snippet always reflect the current connection state.
    /// </para>
    /// </summary>
    internal static class AgentConfiguratorSettingsFactory
    {
        /// <summary>
        /// Build an <see cref="AgentConfig.AgentConfiguratorSettings"/> snapshot from the current Godot
        /// connection state, auto-detecting the host OS. The <c>host</c> is the resolved MCP-client URL
        /// (Cloud <c>/mcp</c> or Custom <c>&lt;host&gt;/mcp</c>) so every shared configurator points the AI
        /// client at the SAME endpoint the plugin connects to — exactly what the retired Godot-local
        /// configurators emitted. Godot is a pure HTTP client (no local-server lifecycle), so the port /
        /// executable / Docker fields are populated from the addon's authoritative server identity for the
        /// shared Custom configurator's Docker hints; the HTTP-config write path never reads them.
        /// </summary>
        public static AgentConfig.AgentConfiguratorSettings Create(GodotMcpConfig config)
        {
            var token = config.Token;

            return AgentConfig.AgentConfiguratorSettings.CreateForHost(
                projectRootPath: ProjectRootPath,
                executableFullPath: string.Empty,
                port: DefaultPort,
                timeoutMs: DefaultTimeoutMs,
                host: GodotMcpConfig.ResolveMcpClientUrl(config),
                token: token,
                connectionMode: MapConnectionMode(config.ActiveMode),
                authOption: MapAuthOption(config),
                serverExecutableName: GodotMcpServerView.ExecutableName,
                serverVersion: GodotMcpServerView.ServerVersion,
                dockerImage: DockerImage);
        }

        /// <summary>The Docker Hub image the shared server publishes (mirrors Unity-MCP's literal).</summary>
        const string DockerImage = "aigamedeveloper/mcp-server";

        /// <summary>Default client timeout (ms) for the Custom configurator's Docker hints — matches Unity's 10s.</summary>
        const int DefaultTimeoutMs = 10000;

        /// <summary>
        /// The port for the shared Custom configurator's Docker hints. Godot connects over HTTP to a host
        /// URL and has no editor-facing port field, so a single sensible default is used (the shared
        /// server's default port). The HTTP-config write path the dock actually uses is port-agnostic.
        /// </summary>
        const int DefaultPort = 8080;

        /// <summary>The absolute Godot project root (<c>res://</c> globalized, trailing slash stripped).</summary>
        static string ProjectRootPath => ProjectSettings.GlobalizePath("res://").TrimEnd('/');

        /// <summary>
        /// Map Godot's <see cref="GodotMcpConnectionMode"/> (<c>Custom</c> = local/self-hosted, <c>Cloud</c>)
        /// onto the shared <see cref="AgentConfig.ConnectionMode"/> (<c>Local</c> / <c>Cloud</c>).
        /// </summary>
        public static AgentConfig.ConnectionMode MapConnectionMode(GodotMcpConnectionMode mode)
            => mode == GodotMcpConnectionMode.Cloud
                ? AgentConfig.ConnectionMode.Cloud
                : AgentConfig.ConnectionMode.Local;

        /// <summary>
        /// Map Godot's effective authorization onto the shared <see cref="AuthOption"/>. Cloud mode always
        /// authorizes (a bearer token is sent); Custom mode follows the live
        /// <see cref="GodotMcpConfig.ActiveAuthOption"/>.
        /// </summary>
        public static AuthOption MapAuthOption(GodotMcpConfig config)
        {
            if (config.ActiveMode == GodotMcpConnectionMode.Cloud)
                return AuthOption.required;

            return config.ActiveAuthOption == GodotMcpAuthOption.Required
                ? AuthOption.required
                : AuthOption.none;
        }
    }
}
#endif
