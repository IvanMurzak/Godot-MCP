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
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Skills;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>Adds references to the standalone CLI and companion-app guides.</summary>
    public class GodotSkillFileGenerator : SkillFileGenerator
    {
        public override bool IncludeAuthorizationExample => false;
        protected virtual bool UsePowerShell => Skill_Cli.IsWindows;

        protected override void BuildToolCommand(StringBuilder sb, IRunTool tool, string host, string inputExample)
        {
            var command = tool.ToolType == McpToolType.System ? "run-system-tool" : "run-tool";
            sb.AppendLine(Skill_Cli.ToolCommand(command, tool.Name, inputExample, UsePowerShell));
            if (!UsePowerShell && inputExample != "{}")
            {
                sb.AppendLine("For complex input, create a JSON file instead:");
                sb.AppendLine("```bash");
                sb.AppendLine(Skill_Cli.JsonFile(inputExample, false));
                sb.AppendLine($"godot-cli {command} {tool.Name} --input-file args.json");
                sb.AppendLine("```");
                sb.AppendLine();
            }
            AppendReferences(sb);
        }

        protected override string BuildSkillContentMarkdown(ISkillContent skill, string skillName)
        {
            var markdown = base.BuildSkillContentMarkdown(skill, skillName);
            if (skill.Name == "godot-cli" || skill.Name == "ai-game-dev-app")
                return markdown;
            var sb = new StringBuilder(markdown).AppendLine().AppendLine();
            AppendReferences(sb);
            return sb.ToString();
        }

        private static void AppendReferences(StringBuilder sb)
        {
            sb.AppendLine("> CLI installation and usage: see the `godot-cli` skill.");
            sb.AppendLine("> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.");
            sb.AppendLine();
        }
    }
}
