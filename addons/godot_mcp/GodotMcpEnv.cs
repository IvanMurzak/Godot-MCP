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

        /// <summary>Forces the Custom-mode authorization option (<c>None</c> / <c>Required</c>).</summary>
        public const string AuthOption = "GODOT_MCP_AUTH_OPTION";

        /// <summary>Forces the plugin's log-verbosity threshold.</summary>
        public const string LogLevel = "GODOT_MCP_LOG_LEVEL";

        // --- Dev-only inject/control bridge (off unless set to "1"). ---

        /// <summary>Enables the 127.0.0.1 dev-control HTTP bridge when exactly <c>"1"</c>.</summary>
        public const string DevControl = "GODOT_MCP_DEV_CONTROL";

        /// <summary>Overrides the dev-control bridge port (defaults to <c>DevControlServer.DefaultPort</c>).</summary>
        public const string DevControlPort = "GODOT_MCP_DEV_CONTROL_PORT";
    }
}
