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
using System.Linq;
using com.IvanMurzak.Godot.MCP.UI.Agents.Impl;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// Static registry of every <see cref="GodotAgentConfigurator"/> the dock's "AI agent" section offers. The
    /// Godot analog of Unity-MCP's <c>AiAgentConfiguratorRegistry</c>. Pure-managed (no Godot native types, no
    /// <c>#if TOOLS</c>) so the list + lookups are CI-unit-tested.
    ///
    /// <para>
    /// <b>To add a new AI agent:</b> create a <see cref="GodotAgentConfigurator"/> subclass under <c>Impl/</c> and
    /// add ONE line to <see cref="_configurators"/> below (keep <see cref="Impl.CustomConfigurator"/> last). No
    /// other file needs to change — the dock panel binds to <see cref="AgentNames"/> / <see cref="All"/> generically.
    /// </para>
    /// </summary>
    public static class GodotAgentConfiguratorRegistry
    {
        // The configurator list. Custom is intentionally LAST (it is the snippet-only fallback). To add an agent,
        // add one line here.
        static readonly IReadOnlyList<GodotAgentConfigurator> _configurators = new GodotAgentConfigurator[]
        {
            new ClaudeCodeConfigurator(),
            new ClaudeDesktopConfigurator(),
            new CursorConfigurator(),
            new VisualStudioCodeConfigurator(),
            new CustomConfigurator(), // keep last
        };

        /// <summary>Every registered configurator, in display order (Custom last).</summary>
        public static IReadOnlyList<GodotAgentConfigurator> All => _configurators;

        /// <summary>The display names, in registry order — populates the dock's agent dropdown.</summary>
        public static IReadOnlyList<string> AgentNames => _configurators.Select(c => c.AgentName).ToList();

        /// <summary>The configurator with the given <paramref name="agentId"/>, or null when absent / id is empty.</summary>
        public static GodotAgentConfigurator? GetByAgentId(string? agentId)
        {
            if (string.IsNullOrEmpty(agentId))
                return null;

            return _configurators.FirstOrDefault(c => c.AgentId == agentId);
        }

        /// <summary>The registry index of <paramref name="agentId"/>, or -1 when absent / id is empty.</summary>
        public static int GetIndexByAgentId(string? agentId)
        {
            if (string.IsNullOrEmpty(agentId))
                return -1;

            for (int i = 0; i < _configurators.Count; i++)
            {
                if (_configurators[i].AgentId == agentId)
                    return i;
            }
            return -1;
        }
    }
}
