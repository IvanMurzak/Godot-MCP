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
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// The Godot-MCP view over the shared engine-agnostic AI-agent configurator registry
    /// (<c>com.IvanMurzak.McpPlugin.AgentConfig.AiAgentConfiguratorRegistry</c>). Godot retired its
    /// local <c>GodotAgentConfiguratorRegistry</c> + per-agent <c>Impl/*</c> copies (issue #142) and now
    /// consumes the SAME configurator set Unity-MCP consumes — except the Unity-only <c>unity-ai</c>
    /// agent, which Godot filters out (it configures the Unity Editor's built-in AI, irrelevant to a
    /// Godot project). This keeps the Godot agent set identical to the pre-#142 list (14 agents + Custom)
    /// while sourcing every configurator's logic from the shared module.
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the filtered list + lookups stay
    /// CI-unit-testable in the plain-xUnit host.
    /// </para>
    /// </summary>
    public static class GodotAgentConfigurators
    {
        /// <summary>The shared agent id that Godot does not surface (Unity-Editor-specific).</summary>
        public const string ExcludedAgentId = "unity-ai";

        // The Godot-visible configurators, in the shared registry's display order, minus the Unity-only
        // agent. Materialized once: the shared registry is static and immutable.
        static readonly IReadOnlyList<AgentConfig.AiAgentConfigurator> _configurators =
            AgentConfig.AiAgentConfiguratorRegistry.All
                .Where(c => c.AgentId != ExcludedAgentId)
                .ToList();

        /// <summary>Every Godot-visible configurator, in display order (Custom last, Unity AI excluded).</summary>
        public static IReadOnlyList<AgentConfig.AiAgentConfigurator> All => _configurators;

        /// <summary>The display names, in display order — populates the dock's agent dropdown.</summary>
        public static IReadOnlyList<string> AgentNames => _configurators.Select(c => c.AgentName).ToList();

        /// <summary>The configurator with the given <paramref name="agentId"/>, or null when absent / id is empty / excluded.</summary>
        public static AgentConfig.AiAgentConfigurator? GetByAgentId(string? agentId)
        {
            if (string.IsNullOrEmpty(agentId) || agentId == ExcludedAgentId)
                return null;

            return _configurators.FirstOrDefault(c => c.AgentId == agentId);
        }

        /// <summary>The index of <paramref name="agentId"/> in <see cref="All"/>, or -1 when absent / id is empty / excluded.</summary>
        public static int GetIndexByAgentId(string? agentId)
        {
            if (string.IsNullOrEmpty(agentId) || agentId == ExcludedAgentId)
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
