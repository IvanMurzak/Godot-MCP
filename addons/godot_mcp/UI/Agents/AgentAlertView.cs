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

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) presentation logic for the dock's AI-agent
    /// "Setup Required" / "Reconfiguration Required" alert panel — the Godot analog of Unity-MCP's per-agent
    /// configure-status alert. It maps an <see cref="AgentConfigState"/> onto the alert's VISIBILITY + title +
    /// message + button text, so the editor-only <see cref="AgentConfiguratorsPanel"/> only decides whether to
    /// show the (reused <c>DockStyle.AlertPanel</c>) frame and which copy to put in it. Keeping these strings here
    /// makes every alert decision unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host without building a
    /// Godot <see cref="Godot.Control"/>.
    /// </summary>
    public static class AgentAlertView
    {
        /// <summary>Alert title shown when the agent's MCP config is missing entirely (first-time setup).</summary>
        public const string SetupRequiredTitle = "Setup Required";

        /// <summary>Alert title shown when the agent's MCP config exists on disk but is stale vs the current server URL.</summary>
        public const string ReconfigurationRequiredTitle = "Reconfiguration Required";

        /// <summary>The single bullet shown under the title (mirrors Unity's "• MCP Configuration" line).</summary>
        public const string McpConfigurationBullet = "• MCP Configuration";

        /// <summary>The alert action button label (writes / rewrites the addon's entry into the agent's config).</summary>
        public const string ButtonText = "Configure";

        /// <summary>
        /// True when the alert panel should be shown for the given <paramref name="state"/>: shown for
        /// <see cref="AgentConfigState.Missing"/> (Setup Required) and <see cref="AgentConfigState.Stale"/>
        /// (Reconfiguration Required), hidden for <see cref="AgentConfigState.UpToDate"/> (nothing to do). Only
        /// agents WITH a config-file path reach this — the caller suppresses the alert for the Custom snippet agent.
        /// </summary>
        public static bool ShowAlert(AgentConfigState state) => state != AgentConfigState.UpToDate;

        /// <summary>
        /// The alert title for the given <paramref name="state"/>: "Setup Required" when the config is
        /// <see cref="AgentConfigState.Missing"/>, "Reconfiguration Required" when it is
        /// <see cref="AgentConfigState.Stale"/>. Returns an empty string for <see cref="AgentConfigState.UpToDate"/>
        /// (the alert is hidden in that case; the caller checks <see cref="ShowAlert"/> first).
        /// </summary>
        public static string Title(AgentConfigState state) => state switch
        {
            AgentConfigState.Missing => SetupRequiredTitle,
            AgentConfigState.Stale => ReconfigurationRequiredTitle,
            _ => string.Empty
        };

        /// <summary>
        /// The alert message body for the given <paramref name="state"/> — the "• MCP Configuration" bullet for both
        /// shown states. Returns an empty string for <see cref="AgentConfigState.UpToDate"/>.
        /// </summary>
        public static string Message(AgentConfigState state) =>
            ShowAlert(state) ? McpConfigurationBullet : string.Empty;
    }
}
