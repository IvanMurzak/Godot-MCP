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

namespace com.IvanMurzak.Godot.MCP.UI.Agents.Impl
{
    /// <summary>
    /// Configurator for Claude Code. Project-local config at <c>&lt;projectRoot&gt;/.mcp.json</c>, servers under
    /// <c>mcpServers</c>. Pure-managed (no Godot native types, no <c>#if TOOLS</c>) — CI-unit-tested via the registry.
    /// </summary>
    public sealed class ClaudeCodeConfigurator : GodotAgentConfigurator
    {
        public override string AgentName => "Claude Code";
        public override string AgentId => "claude-code";
        public override string DownloadUrl => "https://docs.anthropic.com/en/docs/claude-code/overview";
        public override string? TutorialUrl => "https://youtu.be/Sknh2p12W8c";

        public override string? Description =>
            "The recommended way to use AI Game Developer with Godot — Claude Code is a CLI agent you launch from the project root.";

        public override IReadOnlyList<string> ManualSteps => new[]
        {
            "Click 'Configure' above to write the AI Game Developer server into '.mcp.json' at the project root (or copy the snippet manually).",
            "Open a terminal in the project root and run: claude",
            "Restart Claude Code to apply the configuration.",
        };

        public override IReadOnlyList<string> Troubleshooting => new[]
        {
            "- Ensure the Claude Code CLI is installed and accessible from the terminal.",
            "- Start Claude Code in the folder that contains the Godot project (the folder with 'project.godot').",
            "- Check that the configuration file '.mcp.json' exists at the project root.",
            "- Restart Claude Code after configuration changes.",
        };

        public override string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.ClaudeCode(projectRoot);

        // Claude Code is the only skills-capable agent in v1 (owner-approved): it reads auto-generated SKILL.md files
        // from `<projectRoot>/.claude/skills`. Mirrors Unity-MCP's ClaudeCodeConfigurator.SkillsPath = ".claude/skills".
        public override bool SupportsSkills => true;

        public override string? SkillsDir(AgentOs os, string home, string appData, string projectRoot) =>
            AgentConfigPaths.ClaudeCodeSkills(projectRoot);
    }
}
