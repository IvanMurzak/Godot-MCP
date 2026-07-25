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
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Outcome record of the <c>godot-skill-generate</c> SYSTEM tool: WHERE the skill files were written,
    /// which AI agent's configured folder that came from, and HOW MANY <c>SKILL.md</c> files the destination
    /// holds afterwards.
    ///
    /// <para>
    /// Deliberate deviation from the Unity reference, whose <c>unity-skill-generate</c> returns <c>void</c>:
    /// this repo's <c>conventions.md</c> requires a structured, ReflectorNet-serialized result over ad-hoc
    /// strings, and the destination is genuinely non-obvious to the caller (it is resolved from the editor's
    /// SELECTED agent, not passed in) — so echoing it back is what makes the tool verifiable from the client
    /// side without a second round-trip.
    /// </para>
    ///
    /// <para>Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable.</para>
    /// </summary>
    [System.Serializable]
    [Description("Outcome of a skill-file generation: the absolute destination folder, the AI agent it was " +
        "resolved from, and the number of SKILL.md files present afterwards.")]
    public class SkillsGenerateInfo
    {
        [JsonInclude, JsonPropertyName("skillsFolder")]
        [Description("Absolute path of the folder the SKILL.md files were written into.")]
        public string SkillsFolder { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("agentId")]
        [Description("Id of the AI agent whose configured skills folder was used (e.g. 'claude-code'), or " +
            "null when an explicit 'path' override was supplied instead.")]
        public string? AgentId { get; set; } = null;

        [JsonInclude, JsonPropertyName("skillCount")]
        [Description("Number of SKILL.md files found under the destination folder after generation.")]
        public int SkillCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("status")]
        [Description("Short human-readable status note about the generation outcome.")]
        public string? Status { get; set; } = null;

        public SkillsGenerateInfo() { }

        public override string ToString()
            => $"Generated {SkillCount} skill(s) in '{SkillsFolder}'" +
               $"{(AgentId != null ? $" (agent '{AgentId}')" : string.Empty)}";
    }
}
