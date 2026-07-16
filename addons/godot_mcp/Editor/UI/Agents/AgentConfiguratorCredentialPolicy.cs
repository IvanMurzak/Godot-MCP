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
using com.IvanMurzak.Godot.MCP.Connection;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// Pure-managed policy for HOW an AI-agent HTTP config surfaces credentials (mcp-authorize e1 · PR 5,
    /// design <c>06-engine-plugins.md</c> § "Config writers" / "Engine UI deltas"). The shared 7.0
    /// config-writer already defaults to URL-only OAuth output (native OAuth, no static bearer); this decides,
    /// per configurator + the user's "Advanced: use access token" opt-in, which
    /// <see cref="AgentConfig.HttpCredentialMode"/> to write AND what token / auth-option to feed the shared
    /// <see cref="AgentConfig.AgentConfiguratorSettings"/> so that:
    /// <list type="bullet">
    ///   <item>the DEFAULT path is URL-only — no token in the written config OR in the rendered
    ///   "Manual Configuration Steps" command (with the device-flow machine credential store from PR 2 the
    ///   token is no longer user-entered on the golden path — <c>AgentConfiguratorSettings.Token</c> is no
    ///   longer required input); and</item>
    ///   <item>the escape hatch writes the legacy Bearer-header shape (design "Flow C") for the few
    ///   configurators that can't do MCP OAuth (<see cref="AgentConfig.AiAgentConfigurator.SupportsOAuth"/>
    ///   == false, capability flag defaulting to true).</item>
    /// </list>
    ///
    /// <para>
    /// No Godot native types and no <c>#if TOOLS</c>, so every decision is unit-testable in the plain-xUnit
    /// host; the <c>#if TOOLS</c> <see cref="AgentConfiguratorSettingsFactory"/> / <c>AgentConfiguratorsPanel</c>
    /// (and the Cloud connection panel) are thin adapters over this.
    /// </para>
    /// </summary>
    public static class AgentConfiguratorCredentialPolicy
    {
        /// <summary>The label for the "Advanced: use access token" opt-in that reveals the legacy token field.</summary>
        public const string AdvancedUseAccessTokenLabel = "Advanced: use access token";

        /// <summary>
        /// The effective credential mode for writing an HTTP agent config. The default is
        /// <see cref="AgentConfig.HttpCredentialMode.Oauth"/> (URL-only, native OAuth). A configurator that
        /// cannot do MCP OAuth (<paramref name="supportsOAuth"/> == <c>false</c>) has no default path, so it
        /// FORCES <see cref="AgentConfig.HttpCredentialMode.AccessToken"/>; otherwise the user's
        /// <paramref name="useAccessToken"/> "Advanced" opt-in flips to AccessToken.
        /// </summary>
        public static AgentConfig.HttpCredentialMode ResolveCredentialMode(bool supportsOAuth, bool useAccessToken)
            => (!supportsOAuth || useAccessToken)
                ? AgentConfig.HttpCredentialMode.AccessToken
                : AgentConfig.HttpCredentialMode.Oauth;

        /// <summary>
        /// The effective credential mode when the launch-side auth mode also constrains it (mcp-authorize g5):
        /// a Custom-mode server running <see cref="AuthOption.token"/> auth is Bearer-gated, so the written
        /// AI-client config MUST carry <c>Authorization: Bearer &lt;local-secret&gt;</c> to reach it — this FORCES
        /// <see cref="AgentConfig.HttpCredentialMode.AccessToken"/> regardless of the (Cloud-oriented) OAuth
        /// <paramref name="useAccessToken"/> toggle, keeping the config-writer credential mode in agreement with
        /// the launch-side auth mode. Every other case (<c>none</c> anonymous, <c>oauth</c> native MCP OAuth, or
        /// any non-Custom mode) falls through to the base <see cref="ResolveCredentialMode(bool, bool)"/> policy.
        /// Pure over its enum/bool inputs (no Godot native types, no <c>#if TOOLS</c>) so the whole decision is
        /// unit-testable outside the panel — the <c>#if TOOLS</c> <c>AgentConfiguratorsPanel</c> is a thin adapter.
        /// </summary>
        public static AgentConfig.HttpCredentialMode ResolveCredentialMode(
            GodotMcpConnectionMode activeMode,
            AuthOption activeAuthOption,
            bool supportsOAuth,
            bool useAccessToken)
            => (activeMode == GodotMcpConnectionMode.Custom && activeAuthOption == AuthOption.token)
                ? AgentConfig.HttpCredentialMode.AccessToken
                : ResolveCredentialMode(supportsOAuth, useAccessToken);

        /// <summary>
        /// Whether the "Advanced: use access token" TOGGLE is offered for a configurator. Only OAuth-capable
        /// configurators get the toggle — a non-OAuth one has no default (OAuth) path, so its token is
        /// mandatory and there is nothing to toggle (the field is always shown for it via
        /// <see cref="ShowAccessTokenField"/>).
        /// </summary>
        public static bool ShowAdvancedToggle(bool supportsOAuth) => supportsOAuth;

        /// <summary>
        /// Whether the raw access-token field is surfaced for a configurator + preference — <c>true</c> exactly
        /// when the resolved mode is <see cref="AgentConfig.HttpCredentialMode.AccessToken"/> (the "Advanced"
        /// opt-in, or a non-OAuth configurator). The default OAuth path hides it.
        /// </summary>
        public static bool ShowAccessTokenField(bool supportsOAuth, bool useAccessToken)
            => ResolveCredentialMode(supportsOAuth, useAccessToken) == AgentConfig.HttpCredentialMode.AccessToken;

        /// <summary>
        /// The token fed into the shared <see cref="AgentConfig.AgentConfiguratorSettings"/> for a given
        /// <paramref name="mode"/>: only the AccessToken path carries the token; the OAuth default passes empty
        /// so the written config AND the shared <c>Describe()</c> manual-config command are URL-only (no
        /// <c>Authorization: Bearer</c>).
        /// </summary>
        public static string ResolveSettingsToken(AgentConfig.HttpCredentialMode mode, string? configToken)
            => mode == AgentConfig.HttpCredentialMode.AccessToken ? (configToken ?? string.Empty) : string.Empty;

        /// <summary>
        /// The authorization option fed into the shared <see cref="AgentConfig.AgentConfiguratorSettings"/> for
        /// a given <paramref name="mode"/>: <see cref="AuthOption.required"/> on the AccessToken path (so the
        /// shared <c>Describe()</c> renders the Bearer "Manual Configuration Steps" command) and
        /// <see cref="AuthOption.none"/> on the OAuth default (URL-only manual command). The written HTTP config
        /// body itself is driven by the explicit <c>credentialMode</c> passed to <c>GetHttpConfig</c>, not this.
        /// </summary>
        public static AuthOption ResolveSettingsAuthOption(AgentConfig.HttpCredentialMode mode)
            => mode == AgentConfig.HttpCredentialMode.AccessToken ? AuthOption.required : AuthOption.none;
    }
}
