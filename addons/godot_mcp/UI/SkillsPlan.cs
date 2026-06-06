/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.UI.Agents;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) presentation model for the dock's Skills card — the
    /// Godot analog of the supported/path/state decisions Unity-MCP's <c>SetupSkillsUI</c> makes inline. Given the
    /// selected agent and the injected editor path values, it resolves whether skills are supported and the absolute
    /// destination directory, so the <c>#if TOOLS</c> <see cref="SkillsPanel"/> only renders the result. Resolving
    /// this here (rather than in the panel) keeps the supported/path logic CI-unit-testable in the plain-xUnit host.
    /// </summary>
    public readonly struct SkillsPlan
    {
        /// <summary>Whether the selected agent supports skills (the card shows its controls; otherwise a muted "not supported" line).</summary>
        public bool Supported { get; }

        /// <summary>
        /// The absolute skills directory the engine writes into, or <c>null</c> when the agent does not support skills
        /// (or no agent is selected). Non-null exactly when <see cref="Supported"/> is true.
        /// </summary>
        public string? SkillsDir { get; }

        SkillsPlan(bool supported, string? skillsDir)
        {
            Supported = supported;
            SkillsDir = skillsDir;
        }

        /// <summary>The "no skills" plan — agent absent or skills unsupported.</summary>
        public static readonly SkillsPlan Unsupported = new(false, null);

        /// <summary>
        /// Resolve the Skills plan for <paramref name="agent"/> against the injected editor path values (the same
        /// quadruple <see cref="GodotAgentConfigurator.SkillsDir"/> takes). A null agent, an agent that does not
        /// support skills, or one whose <see cref="GodotAgentConfigurator.SkillsDir"/> resolves null all yield
        /// <see cref="Unsupported"/>. Pure-managed: the OS family / home / appData / projectRoot are injected, so the
        /// editor's <c>Godot.OS</c> / <c>ProjectSettings</c> reads stay in the panel.
        /// </summary>
        public static SkillsPlan Resolve(GodotAgentConfigurator? agent, AgentOs os, string home, string appData, string projectRoot)
        {
            if (agent == null || !agent.SupportsSkills)
                return Unsupported;

            var dir = agent.SkillsDir(os, home, appData, projectRoot);
            if (string.IsNullOrEmpty(dir))
                return Unsupported;

            return new SkillsPlan(true, dir);
        }
    }
}
