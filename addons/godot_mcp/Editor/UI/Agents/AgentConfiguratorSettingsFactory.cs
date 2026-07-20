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
        /// configurators emitted. The <c>port</c> is the ProjectIdentity-derived local-server port
        /// (<see cref="ResolveLocalServerPort"/> — the same one the local server binds and the loopback
        /// config URL carries, mcp-authorize g3), so the shared Custom configurator's Docker hints agree
        /// with the written URL and the running server instead of showing the stale fixed 8080; the
        /// executable field is unused (the HTTP-config write path reads neither).
        ///
        /// <para>
        /// The <paramref name="credentialMode"/> (mcp-authorize e1 · PR 5) governs how the token surfaces:
        /// <see cref="AgentConfig.HttpCredentialMode.Oauth"/> (the DEFAULT) yields URL-only settings — empty
        /// token + <c>authOption=none</c> — so the written config AND the shared <c>Describe()</c>
        /// "Manual Configuration Steps" command are URL-only; <see cref="AgentConfig.HttpCredentialMode.AccessToken"/>
        /// carries the live token + <c>required</c> so the escape-hatch path writes the legacy Bearer-header shape.
        /// The mapping is centralized in the pure-managed, unit-tested
        /// <see cref="AgentConfiguratorCredentialPolicy"/>.
        /// </para>
        /// </summary>
        public static AgentConfig.AgentConfiguratorSettings Create(
            GodotMcpConfig config,
            AgentConfig.HttpCredentialMode credentialMode = AgentConfig.HttpCredentialMode.Oauth)
        {
            var token = AgentConfiguratorCredentialPolicy.ResolveSettingsToken(credentialMode, config.Token);
            var authOption = AgentConfiguratorCredentialPolicy.ResolveSettingsAuthOption(credentialMode);

            return AgentConfig.AgentConfiguratorSettings.CreateForHost(
                projectRootPath: ProjectRootPath,
                executableFullPath: string.Empty,
                port: ResolveLocalServerPort(config),
                timeoutMs: DefaultTimeoutMs,
                host: GodotMcpConfig.ResolveMcpClientUrl(config),
                token: token,
                connectionMode: MapConnectionMode(config.ActiveMode),
                authOption: authOption,
                serverExecutableName: GodotMcpServerView.ExecutableName,
                serverVersion: GodotMcpServerView.ServerVersion,
                dockerImage: DockerImage);
        }

        /// <summary>The Docker Hub image the shared server publishes (mirrors Unity-MCP's literal).</summary>
        const string DockerImage = "aigamedeveloper/mcp-server";

        /// <summary>Default client timeout (ms) for the Custom configurator's Docker hints — matches Unity's 10s.</summary>
        const int DefaultTimeoutMs = 10000;

        /// <summary>
        /// The local server port for the shared Custom configurator's Docker hints
        /// (<c>-p &lt;port&gt;:&lt;port&gt;</c>, <c>-e PORT=&lt;port&gt;</c>, container name) — the SAME
        /// port the local server binds (<see cref="GodotMcpConnection.ResolveLocalServerPort"/>) and the
        /// written config URL carries (<see cref="AgentConfig.AgentConfiguratorSettings.PinnedHttpUrl"/>),
        /// under the shared three-level precedence (marker <c>portOverride</c> → a port the user typed
        /// into the host → the deterministic derived port), so the "Manual Configuration Steps" Docker
        /// command can never show the stale fixed 8080 — nor a derived port while the server binds the
        /// port the user typed (mcp-authorize g3, extended by the 2026-07-19 owner ruling; design 06 ·
        /// D15). Resolved from the live custom host + project marker via the pure,
        /// unit-tested <see cref="GodotProjectIdentity.ResolveLocalServerBindPort"/>; a malformed marker
        /// degrades to "no override" so config rendering never faults.
        /// </summary>
        static int ResolveLocalServerPort(GodotMcpConfig config)
        {
            AgentConfig.ProjectMarker? marker = null;
            try
            {
                marker = AgentConfig.ProjectMarker.Read(ProjectRootPath);
            }
            catch
            {
                // A malformed marker degrades to no override — never break config rendering.
            }

            return GodotProjectIdentity.ResolveLocalServerBindPort(config.ResolveCustomHost(), ProjectRootPath, marker);
        }

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
    }
}
#endif
