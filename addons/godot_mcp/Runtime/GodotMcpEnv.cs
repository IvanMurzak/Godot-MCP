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

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Canonical home for every custom <c>GODOT_MCP_*</c> environment-variable NAME the addon reads.
    /// Always read env vars through these constants (e.g. <c>OS.GetEnvironment(GodotMcpEnv.Host)</c>) — never
    /// hard-code the string literal at the call site, so the full set of recognized variables is discoverable
    /// in one place and impossible to typo-drift across files. (The Godot analog of Unity-MCP's <c>UNITY_MCP_*</c>.)
    /// </summary>
    public static class GodotMcpEnv
    {
        // --- Connection / configuration (resolved in GodotMcpConfig). ---

        /// <summary>Overrides the cloud base URL.</summary>
        public const string CloudUrl = "GODOT_MCP_CLOUD_URL";

        /// <summary>Overrides the custom-mode server host.</summary>
        public const string Host = "GODOT_MCP_HOST";

        /// <summary>Supplies the bearer token (routed to cloud or custom token by the active mode).</summary>
        public const string Token = "GODOT_MCP_TOKEN";

        /// <summary>Forces the connection mode (<c>Cloud</c> / <c>Custom</c>, case-insensitive).</summary>
        public const string ConnectionMode = "GODOT_MCP_CONNECTION_MODE";

        /// <summary>Forces the Custom-mode authorization option (<c>none</c> / <c>oauth</c> / <c>token</c>).</summary>
        public const string AuthOption = "GODOT_MCP_AUTH_OPTION";

        /// <summary>Forces the plugin's log-verbosity threshold.</summary>
        public const string LogLevel = "GODOT_MCP_LOG_LEVEL";

        /// <summary>Forces the editor tools' data layer (<c>Rql</c> / <c>Json</c>).</summary>
        public const string ToolFormat = "GODOT_MCP_TOOL_FORMAT";

        // --- In-game runtime started from the editor (resolved in GodotMcpRuntimeHandle). ---

        /// <summary>
        /// Set by the editor plugin in the editor process (its process id), so every game the editor runs inherits it: a game
        /// started with Play also inherits <see cref="Host"/>, and its in-game runtime would connect to the editor's server.
        /// </summary>
        public const string EditorPid = "GODOT_MCP_EDITOR_PID";

        /// <summary>
        /// <c>true</c> / <c>1</c>: the in-game runtime connects even in a game started from the editor (default: it does not;
        /// same as <c>GodotMcpRuntimeBuilder.ConnectWhenRunFromEditor()</c>).
        /// </summary>
        public const string RuntimeFromEditor = "GODOT_MCP_RUNTIME_FROM_EDITOR";

        // --- Local server binary (resolved in GodotMcpServerManager; editor-only). ---

        /// <summary>
        /// Absolute path to a <c>gamedev-mcp-server(.exe)</c> the editor should launch INSTEAD of the release
        /// pinned by <c>GodotMcpServerView.ServerVersion</c>. When it names an existing file the download and
        /// the cached-version match are skipped, and orphaned-server cleanup is disabled while it is active.
        /// A value naming no existing file is ignored (normal download path). Dev / CI use; the Godot analog
        /// of <c>UNREAL_MCP_SERVER_PATH</c> / <c>UNITY_MCP_SERVER_PATH</c>.
        /// </summary>
        public const string ServerPath = "GODOT_MCP_SERVER_PATH";

        /// <summary>
        /// <c>true</c> / <c>1</c>: in Custom mode the editor starts its local MCP server on boot, as if the dock's
        /// Start button were pressed. Set by <c>godot-cli exec</c> / <c>open --headless</c>.
        /// </summary>
        public const string StartServer = "GODOT_MCP_START_SERVER";

        // --- CLI-owned editor sessions (shared names across engines; editor-only). ---

        /// <summary>
        /// Seconds without tool calls after which the editor saves and quits itself. Set by <c>godot-cli exec</c> /
        /// <c>open --idle-timeout</c>; unset = never (a user's editor is never closed).
        /// </summary>
        public const string SessionIdleSeconds = "AIGD_SESSION_IDLE_SECONDS";

        /// <summary>The nonce of the CLI's lease (<c>.ai-game-dev/session.json</c>): the editor removes only its own lease.</summary>
        public const string SessionNonce = "AIGD_SESSION_NONCE";

        // --- Dev-only inject/control bridge (off unless set to "1"). ---

        /// <summary>Enables the 127.0.0.1 dev-control HTTP bridge when exactly <c>"1"</c>.</summary>
        public const string DevControl = "GODOT_MCP_DEV_CONTROL";

        /// <summary>Overrides the dev-control bridge port (defaults to <c>DevControlServer.DefaultPort</c>).</summary>
        public const string DevControlPort = "GODOT_MCP_DEV_CONTROL_PORT";
    }
}
