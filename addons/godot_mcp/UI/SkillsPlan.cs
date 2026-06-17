/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.IO;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) presentation model for the dock's Skills card — the
    /// Godot analog of the supported/path decisions Unity-MCP's <c>SetupSkillsUI</c> makes inline. Given the selected
    /// shared <see cref="AgentConfig.AiAgentConfigurator"/> and the project root, it resolves whether skills are
    /// supported and the absolute destination directory, so the <c>#if TOOLS</c> <see cref="SkillsPanel"/> only renders
    /// the result. Resolving this here (rather than in the panel) keeps the supported/path logic CI-unit-testable.
    ///
    /// <para>
    /// Post-#142 this reads the shared configurator's <see cref="AgentConfig.AiAgentConfigurator.SupportsSkills"/> +
    /// project-relative <see cref="AgentConfig.AiAgentConfigurator.SkillsPath"/> (e.g. <c>.claude/skills</c>), resolved
    /// against the injected project root — replacing the retired Godot-local <c>GodotAgentConfigurator.SkillsDir</c>.
    /// </para>
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
        /// Resolve the Skills plan for <paramref name="agent"/> against the injected <paramref name="projectRoot"/>. A
        /// null agent, an agent that does not support skills, or one whose <see cref="AgentConfig.AiAgentConfigurator.SkillsPath"/>
        /// is empty all yield <see cref="Unsupported"/>. The shared configurator's <c>SkillsPath</c> is a project-relative
        /// path (e.g. <c>.claude/skills</c>) — combined with the project root here into the absolute destination. Only a
        /// SAFE in-project relative path is honoured (an absolute / traversal path yields <see cref="Unsupported"/>).
        /// Pure-managed: the project root is injected, so the editor's <c>ProjectSettings</c> read stays in the panel.
        /// </summary>
        public static SkillsPlan Resolve(AgentConfig.AiAgentConfigurator? agent, string projectRoot)
        {
            if (agent == null || !agent.SupportsSkills)
                return Unsupported;

            var relative = agent.SkillsPath;
            if (string.IsNullOrEmpty(relative) || !SkillsPathUtils.IsSafeRelativeSkillsPath(relative))
                return Unsupported;

            var dir = Path.Combine(projectRoot, relative);
            return new SkillsPlan(true, dir);
        }
    }
}
