/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Skills
    {
        public const string SkillsGenerateToolId = "godot-skill-generate";

        [AiTool
        (
            SkillsGenerateToolId,
            Title = "Skill (Tool) / Generate All",
            DestructiveHint = false,
            Enabled = false,
            ToolType = McpToolType.System
        )]
        [AiSkillDescription("Regenerate every `SKILL.md` from the project's currently-registered MCP tools " +
            "into the selected AI agent's skills folder (or a project-relative override path). " +
            "Writes the YAML `description:` from `[AiSkillDescription]` and the body from `[AiSkillBody]`.")]
        [AiSkillBody("Generate all skills from the tools currently registered in the Godot project.\n\n" +
            "## Inputs\n\n" +
            "- `path` (optional) — project-relative skills folder (e.g. `.claude/skills`). Absolute paths, " +
            "`res://` paths, and `..` traversal segments are rejected. When null/empty, the folder " +
            "configured for the editor's SELECTED AI agent is used — which is the normal case.\n\n" +
            "## Behavior\n\n" +
            "Creates the destination folder if missing, then drives the McpPlugin skill generator to emit a " +
            "`SKILL.md` per registered MCP tool. The plugin's `SkillsPath` / `ProjectRootPath` are " +
            "temporarily swapped to the target folder and restored in a `finally`, so the on-disk " +
            "configuration is unchanged after the call returns. Returns the resolved destination, the agent " +
            "it came from, and the resulting `SKILL.md` count.\n\n" +
            "Note that the addon ALSO auto-generates skills on plugin boot when the dock's \"Auto-generate\" " +
            "toggle is on; this tool is the explicit, on-demand path.")]
        [Description("Generate all skills from the tools registered in the Godot project. Writes a " +
            "SKILL.md per registered MCP tool into the selected AI agent's configured skills folder, or " +
            "into 'path' when a project-relative override is supplied. Returns the resolved destination " +
            "folder, the agent it was resolved from, and the resulting SKILL.md count.")]
        public SkillsGenerateInfo GenerateAll
        (
            [Description("Optional project-relative path to the skills folder, e.g. '.claude/skills'. " +
                "Absolute paths, res:// paths, and '..' traversal segments are rejected. If null or empty, " +
                "the selected AI agent's configured skills folder is used.")]
            string? path = null
        )
        {
            // Validate BEFORE reaching for the host so a hostile path fails identically whether or not an
            // editor is attached (and so the guards stay unit-testable without one).
            var relativeFolder = SkillsToolPaths.RequireRelativeSkillsFolder(path, nameof(path));

            return RequireHost().GenerateSkills(relativeFolder);
        }
    }
}
