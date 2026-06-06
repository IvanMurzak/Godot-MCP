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
using System;
using System.Text.Json.Serialization;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Which MCP server the Godot plugin connects to.
    /// </summary>
    public enum GodotMcpConnectionMode
    {
        /// <summary>A user-supplied server URL (local dev server, self-hosted, etc.).</summary>
        Custom,

        /// <summary>The hosted ai-game.dev cloud server.</summary>
        Cloud
    }

    /// <summary>
    /// Whether the Custom-mode connection sends a bearer token. The Godot analog of Unity-MCP's
    /// <c>com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server.AuthOption</c> (<c>none</c>/<c>required</c>),
    /// condensed to the two states the Godot Custom-mode UI exposes. Only meaningful in
    /// <see cref="GodotMcpConnectionMode.Custom"/> — Cloud-mode auth is handled separately (device flow,
    /// a later task).
    /// </summary>
    public enum GodotMcpAuthOption
    {
        /// <summary>No bearer token is sent (the Custom server accepts anonymous connections).</summary>
        None,

        /// <summary>A bearer token is sent (the Custom server requires authorization).</summary>
        Required
    }

    /// <summary>
    /// Connection configuration for the Godot-MCP plugin. The Godot analog of Unity-MCP's
    /// <c>UnityMcpPlugin.UnityConnectionConfig</c> — it extends the reused
    /// <see cref="ConnectionConfig"/> from <c>com.IvanMurzak.McpPlugin</c> (so the SignalR client
    /// consumes <see cref="Host"/>/<see cref="Token"/>/<see cref="KeepConnected"/> unchanged) and
    /// layers Cloud/Custom mode selection plus environment-variable overrides on top.
    ///
    /// <para>
    /// The URL/token <em>resolution</em> logic lives in pure static helpers
    /// (<see cref="ResolveCloudBaseUrl"/>, <see cref="ResolveCloudUrl"/>,
    /// <see cref="ResolveActiveMode"/>) so it is unit-testable in the plain-xUnit
    /// <c>Godot-MCP.Tests</c> host without constructing any Godot native types or a live
    /// SignalR client.
    /// </para>
    /// </summary>
    public class GodotMcpConfig : ConnectionConfig
    {
        // --- Environment-variable names (Godot analog of UNITY_MCP_*). ---

        /// <summary>Overrides the cloud base URL (analogous to <c>UNITY_MCP_CLOUD_URL</c>).</summary>
        public const string EnvCloudUrl = "GODOT_MCP_CLOUD_URL";

        /// <summary>Overrides the custom-mode server host (used only in <see cref="GodotMcpConnectionMode.Custom"/>).</summary>
        public const string EnvHost = "GODOT_MCP_HOST";

        /// <summary>Supplies the bearer token (routed to cloud or custom token by active mode).</summary>
        public const string EnvToken = "GODOT_MCP_TOKEN";

        /// <summary>Forces the connection mode (<c>Cloud</c> / <c>Custom</c>, case-insensitive).</summary>
        public const string EnvConnectionMode = "GODOT_MCP_CONNECTION_MODE";

        /// <summary>
        /// Forces the Custom-mode authorization option (<c>None</c> / <c>Required</c>, case-insensitive).
        /// The Godot analog of Unity-MCP's <c>UNITY_MCP_AUTH_OPTION</c>.
        /// </summary>
        public const string EnvAuthOption = "GODOT_MCP_AUTH_OPTION";

        /// <summary>
        /// Forces the plugin's log-verbosity threshold (<c>Trace</c> / <c>Debug</c> / <c>Info</c> /
        /// <c>Warning</c> / <c>Error</c> / <c>None</c>, case-insensitive) — the Godot analog of a
        /// <c>UNITY_MCP_LOG_LEVEL</c>. Honored live by <see cref="ActiveLogLevel"/> so a smoke run can be
        /// driven at <c>Trace</c> without touching the persisted config.
        /// </summary>
        public const string EnvLogLevel = "GODOT_MCP_LOG_LEVEL";

        // --- Defaults. ---

        /// <summary>Base URL of the hosted cloud server. The <c>/mcp</c> hub path is appended by <see cref="ResolveCloudUrl"/>.</summary>
        public const string DefaultCloudBaseUrl = "https://ai-game.dev";

        /// <summary>The MCP hub path appended to the cloud base URL.</summary>
        public const string CloudHubPath = "/mcp";

        /// <summary>Default custom-mode host when nothing is configured (local dev server convention).</summary>
        public const string DefaultCustomHost = "http://localhost:8080";

        // --- Serialized backing fields. ---

        /// <summary>
        /// Backing field for the custom-mode server URL. Serialized as <c>host</c>.
        /// Use <see cref="Host"/> for the active connection URL (which routes through Cloud mode).
        /// </summary>
        [JsonPropertyName("host")]
        public string CustomHost { get; set; } = DefaultCustomHost;

        /// <summary>Backing field for the custom-mode bearer token. Serialized as <c>token</c>.</summary>
        [JsonPropertyName("token")]
        public string? CustomToken { get; set; }

        /// <summary>Backing field for the cloud-mode bearer token. Serialized as <c>cloudToken</c>.</summary>
        [JsonPropertyName("cloudToken")]
        public string? CloudToken { get; set; }

        /// <summary>The configured connection mode (overridable by <see cref="EnvConnectionMode"/> via <see cref="ResolveActiveMode"/>).</summary>
        [JsonPropertyName("connectionMode")]
        public GodotMcpConnectionMode ConnectionMode { get; set; } = GodotMcpConnectionMode.Cloud;

        /// <summary>
        /// The configured Custom-mode authorization option (overridable by <see cref="EnvAuthOption"/> via
        /// <see cref="ResolveActiveAuthOption"/>). Defaults to <see cref="GodotMcpAuthOption.None"/> — a
        /// fresh Custom connection is anonymous until the user opts into auth. Only consulted in Custom mode.
        /// </summary>
        [JsonPropertyName("authOption")]
        public GodotMcpAuthOption AuthOption { get; set; } = GodotMcpAuthOption.None;

        /// <summary>
        /// The configured log-verbosity threshold for the plugin's routing of the reused framework's
        /// <c>Microsoft.Extensions.Logging</c> output to the Godot Output (overridable by
        /// <see cref="EnvLogLevel"/> via <see cref="ResolveActiveLogLevel"/>). Defaults to
        /// <see cref="GodotMcpLogLevel.Info"/> — the framework's connection/handshake info lines are shown,
        /// but trace/debug noise is suppressed until the user opts in via the dock's Log Level dropdown.
        /// </summary>
        [JsonPropertyName("logLevel")]
        public GodotMcpLogLevel LogLevel { get; set; } = GodotMcpLogLevel.Info;

        /// <summary>
        /// Persisted per-feature enable/disable map for the dock's MCP-features section (tools / prompts /
        /// resources). Only user-touched items are stored; an empty map means "everything at its live default"
        /// (the McpPlugin managers default to enabled), so a fresh install disables nothing. Reapplied on plugin
        /// boot (see <c>GodotMcpConnection.ReapplyFeatureStates</c>) and updated when the user toggles an item in
        /// a feature list window. The merge/capture logic is pure-managed (<see cref="GodotMcpFeatureStateMerge"/>).
        /// </summary>
        [JsonPropertyName("features")]
        public GodotMcpFeatureMap Features { get; set; } = new();

        /// <summary>
        /// The AI agent the dock's "AI agent" section has selected (the <c>AgentId</c> of a
        /// <c>GodotAgentConfigurator</c>, e.g. <c>claude-code</c>). Persisted so the user's choice survives an editor
        /// restart. Defaults to <c>claude-code</c> — the first-listed configurator. This is pure presentation state
        /// (it does not affect the plugin's own connection), so it is never env-overridden.
        /// </summary>
        [JsonPropertyName("selectedAgentId")]
        public string SelectedAgentId { get; set; } = "claude-code";

        /// <summary>
        /// The effective connection mode after applying the <see cref="EnvConnectionMode"/> override.
        /// Never serialized — recomputed from the env each access so a process-level override always wins.
        /// </summary>
        [JsonIgnore]
        public GodotMcpConnectionMode ActiveMode => ResolveActiveMode(ConnectionMode);

        /// <summary>
        /// The effective Custom-mode authorization option after applying the <see cref="EnvAuthOption"/>
        /// override. Never serialized — recomputed from the env each access so a process-level override
        /// always wins (parity with <see cref="ActiveMode"/>).
        /// </summary>
        [JsonIgnore]
        public GodotMcpAuthOption ActiveAuthOption => ResolveActiveAuthOption(AuthOption);

        /// <summary>
        /// The effective log-verbosity threshold after applying the <see cref="EnvLogLevel"/> override.
        /// Never serialized — recomputed from the env each access so a process-level override always wins
        /// (parity with <see cref="ActiveMode"/> / <see cref="ActiveAuthOption"/>). The logger reads this
        /// LIVE on every log call, so the dock's Log Level dropdown takes effect without a rebuild.
        /// </summary>
        [JsonIgnore]
        public GodotMcpLogLevel ActiveLogLevel => ResolveActiveLogLevel(LogLevel);

        /// <summary>
        /// Active connection URL based on <see cref="ActiveMode"/>:
        /// the resolved cloud URL in Cloud mode, otherwise the custom host.
        /// The setter writes through to <see cref="CustomHost"/> (custom mode is the only writable host).
        /// </summary>
        [JsonIgnore]
        public override string Host
        {
            get => ActiveMode == GodotMcpConnectionMode.Cloud ? ResolveCloudUrl() : ResolveCustomHost();
            set => CustomHost = value;
        }

        /// <summary>
        /// Active bearer token based on <see cref="ActiveMode"/>. In Cloud mode the cloud token (env-overridable);
        /// in Custom mode the custom token (env-overridable). The setter mirrors the getter so a generic
        /// <c>Token = ...</c> assignment lands on the field that matches the active mode.
        /// </summary>
        [JsonIgnore]
        public override string? Token
        {
            get => ActiveMode == GodotMcpConnectionMode.Cloud ? ResolveCloudToken() : ResolveCustomToken();
            set
            {
                if (ActiveMode == GodotMcpConnectionMode.Cloud)
                    CloudToken = value;
                else
                    CustomToken = value;
            }
        }

        public GodotMcpConfig()
        {
            // Auto-reconnect / backoff are handled by the reused McpPlugin SignalR client when
            // KeepConnected is true (it drives a FixedRetryPolicy on the underlying HubConnection).
            // We do not reimplement transport — we just opt into staying connected.
            KeepConnected = true;

            // Auto-generate skills is ON by default (owner-approved v1 default): on a fresh install the addon
            // regenerates the skills-capable agent's SKILL.md files on boot via GenerateSkillFilesIfNeeded(), so the
            // AI agent sees up-to-date per-tool skills with no manual step. The dock's Skills card exposes a toggle
            // that persists an override (GenerateSkillFiles is a serialized field on the base ConnectionConfig).
            GenerateSkillFiles = true;
        }

        // --- Pure resolution helpers (unit-testable; no Godot / SignalR dependency). ---

        /// <summary>
        /// Resolve the active connection mode, letting <see cref="EnvConnectionMode"/> override the configured value.
        /// An unrecognized/empty env value falls through to <paramref name="configured"/>.
        /// </summary>
        public static GodotMcpConnectionMode ResolveActiveMode(GodotMcpConnectionMode configured)
        {
            var normalized = NormalizeEnv(ReadEnv(EnvConnectionMode));
            if (string.IsNullOrEmpty(normalized))
                return configured;

            // Only honor named enum values; reject numeric strings ("1") that Enum.TryParse
            // would otherwise accept and silently map to an arbitrary mode.
            if (Enum.TryParse<GodotMcpConnectionMode>(normalized, ignoreCase: true, out var parsed) &&
                !int.TryParse(normalized, out _))
                return parsed;

            return configured;
        }

        /// <summary>
        /// Resolve the cloud BASE url (no hub path), applying the <see cref="EnvCloudUrl"/> override.
        /// Invalid / non-http(s) overrides fall back to <see cref="DefaultCloudBaseUrl"/>. A trailing
        /// <c>/mcp</c> is stripped so <see cref="ResolveCloudUrl"/> never produces <c>/mcp/mcp</c>.
        /// </summary>
        public static string ResolveCloudBaseUrl()
        {
            var normalized = NormalizeUrl(ReadEnv(EnvCloudUrl));
            if (string.IsNullOrEmpty(normalized))
                return DefaultCloudBaseUrl;

            if (!IsValidHttpUrl(normalized))
                return DefaultCloudBaseUrl;

            if (normalized.EndsWith(CloudHubPath, StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^CloudHubPath.Length];

            return normalized;
        }

        /// <summary>Resolve the full cloud connection URL (base + <see cref="CloudHubPath"/>).</summary>
        public static string ResolveCloudUrl() => ResolveCloudBaseUrl() + CloudHubPath;

        /// <summary>
        /// Resolve the MCP-client endpoint URL an external AI client (Claude Code, Cursor, …) should POST to —
        /// the value the AI-agent configurators write into the user's MCP-client config. This is DISTINCT from
        /// <see cref="Host"/> (the URL the <em>plugin</em> connects to): the plugin connects to
        /// <c>&lt;host&gt;/hub/mcp-server</c> over SignalR, while an MCP client connects to <c>&lt;host&gt;/mcp</c>
        /// over streamable-HTTP.
        ///
        /// <list type="bullet">
        ///   <item><b>Cloud mode</b>: <see cref="ResolveCloudUrl"/> — already ends in <see cref="CloudHubPath"/>
        ///   (<c>/mcp</c>), so it is returned unchanged.</item>
        ///   <item><b>Custom mode</b>: the resolved custom host (trailing slash stripped) with
        ///   <see cref="CloudHubPath"/> appended — e.g. <c>http://localhost:8080</c> → <c>http://localhost:8080/mcp</c>.
        ///   A host that already ends in <c>/mcp</c> is not double-suffixed.</item>
        /// </list>
        ///
        /// Reads the active mode/host LIVE off <paramref name="config"/> (so an env override applies) but is a
        /// pure static — no Godot / SignalR dependency — so it is unit-testable in the plain-xUnit host.
        /// </summary>
        public static string ResolveMcpClientUrl(GodotMcpConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.ActiveMode == GodotMcpConnectionMode.Cloud)
                return ResolveCloudUrl();

            // Custom mode: the plugin connects to <host>/hub/mcp-server; the MCP client connects to <host>/mcp.
            var host = config.ResolveCustomHost().TrimEnd('/');
            if (host.EndsWith(CloudHubPath, StringComparison.OrdinalIgnoreCase))
                return host;

            return host + CloudHubPath;
        }

        /// <summary>
        /// Resolve the active custom-mode host, applying the <see cref="EnvHost"/> override.
        /// </summary>
        public string ResolveCustomHost()
        {
            // Prefer the env override, then the configured host; validate each as an http(s) URL
            // (mirroring ResolveCloudBaseUrl) and fall back to DefaultCustomHost on anything invalid.
            var envHost = NormalizeUrl(ReadEnv(EnvHost));
            if (!string.IsNullOrEmpty(envHost))
                return IsValidHttpUrl(envHost) ? envHost : DefaultCustomHost;

            var configuredHost = NormalizeUrl(CustomHost);
            if (!string.IsNullOrEmpty(configuredHost))
                return IsValidHttpUrl(configuredHost) ? configuredHost : DefaultCustomHost;

            return DefaultCustomHost;
        }

        /// <summary>Resolve the active cloud token, applying the <see cref="EnvToken"/> override.</summary>
        public string? ResolveCloudToken()
        {
            var envToken = NormalizeEnv(ReadEnv(EnvToken));
            return string.IsNullOrEmpty(envToken) ? CloudToken : envToken;
        }

        /// <summary>
        /// Resolve the active custom token, applying the <see cref="EnvToken"/> override. Returns
        /// <c>null</c> when the active auth option is <see cref="GodotMcpAuthOption.None"/> — an anonymous
        /// Custom connection sends no bearer, regardless of any token the user previously generated (so
        /// flipping auth back to <c>None</c> drops the token from the wire without discarding the stored
        /// value). When auth is <see cref="GodotMcpAuthOption.Required"/>, the env token wins over the
        /// persisted <see cref="CustomToken"/>, mirroring the other resolvers.
        /// </summary>
        public string? ResolveCustomToken()
        {
            if (ActiveAuthOption == GodotMcpAuthOption.None)
                return null;

            var envToken = NormalizeEnv(ReadEnv(EnvToken));
            return string.IsNullOrEmpty(envToken) ? CustomToken : envToken;
        }

        /// <summary>
        /// Resolve the active Custom-mode auth option, letting <see cref="EnvAuthOption"/> override the
        /// configured value. An unrecognized/empty/numeric env value falls through to
        /// <paramref name="configured"/> — identical discipline to <see cref="ResolveActiveMode"/>.
        /// </summary>
        public static GodotMcpAuthOption ResolveActiveAuthOption(GodotMcpAuthOption configured)
        {
            var normalized = NormalizeEnv(ReadEnv(EnvAuthOption));
            if (string.IsNullOrEmpty(normalized))
                return configured;

            if (Enum.TryParse<GodotMcpAuthOption>(normalized, ignoreCase: true, out var parsed) &&
                !int.TryParse(normalized, out _))
                return parsed;

            return configured;
        }

        /// <summary>
        /// Resolve the active log-verbosity threshold, letting <see cref="EnvLogLevel"/> override the
        /// configured value. An unrecognized/empty/numeric env value falls through to
        /// <paramref name="configured"/> — identical discipline to <see cref="ResolveActiveMode"/> /
        /// <see cref="ResolveActiveAuthOption"/>.
        /// </summary>
        public static GodotMcpLogLevel ResolveActiveLogLevel(GodotMcpLogLevel configured)
        {
            var normalized = NormalizeEnv(ReadEnv(EnvLogLevel));
            if (string.IsNullOrEmpty(normalized))
                return configured;

            if (Enum.TryParse<GodotMcpLogLevel>(normalized, ignoreCase: true, out var parsed) &&
                !int.TryParse(normalized, out _))
                return parsed;

            return configured;
        }

        static string? ReadEnv(string name) => Environment.GetEnvironmentVariable(name);

        /// <summary>
        /// Single normalization for env/config string values: trim surrounding whitespace and a single
        /// pair of wrapping double-quotes (so <c>GODOT_MCP_TOKEN="abc"</c> yields <c>abc</c>, not a
        /// bearer value with literal quotes). Returns <c>null</c> for null/blank input.
        ///
        /// <para>
        /// Exposed <c>internal</c> so the <c>.env</c> file layer
        /// (<see cref="GodotMcpEnvFile"/>) sanitizes file values identically to process-env values
        /// — see the precedence note on <see cref="GodotMcpEnvFile.Apply"/>.
        /// </para>
        /// </summary>
        internal static string? NormalizeEnv(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return raw.Trim().Trim('"');
        }

        /// <summary>
        /// URL-flavored normalization: <see cref="NormalizeEnv"/> plus a trailing-slash trim so URL
        /// resolvers compare/append cleanly. Returns <c>null</c> for null/blank input.
        /// </summary>
        internal static string? NormalizeUrl(string? raw)
        {
            var normalized = NormalizeEnv(raw);
            return string.IsNullOrEmpty(normalized) ? null : normalized.TrimEnd('/');
        }

        /// <summary>True when <paramref name="value"/> is an absolute http or https URL.</summary>
        internal static bool IsValidHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        /// <summary>
        /// True when <paramref name="url"/> targets a loopback host (<c>localhost</c>, an IPv4
        /// <c>127.0.0.0/8</c> address, or IPv6 <c>::1</c>). Mirrors Unity-MCP's
        /// <c>EnvironmentUtils.IsLoopbackUrl</c> — used to auto-select <see cref="GodotMcpConnectionMode.Custom"/>
        /// when a local-dev host is configured without an explicit mode. Pure-managed (no Godot types).
        /// </summary>
        internal static bool IsLoopbackUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                return true;
            if (System.Net.IPAddress.TryParse(host, out var ip))
                return System.Net.IPAddress.IsLoopback(ip);

            return false;
        }
    }
}
