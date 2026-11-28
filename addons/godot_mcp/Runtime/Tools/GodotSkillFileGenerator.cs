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
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Skills;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>Adds references to the standalone CLI and companion-app guides.</summary>
    public class GodotSkillFileGenerator : SkillFileGenerator
    {
        /// <summary>
        /// The front matter of every generated SKILL.md. The App's agent reads <c>ai-game-dev</c>: a tool's skill
        /// duplicates its tools/list entry, so it is hidden there (<c>exclude</c>); standalone knowledge skills
        /// (<c>godot-rql</c>, <c>godot-cli-install</c>, <c>ai-game-dev-app</c>) are shown (<c>include</c>).
        /// </summary>
        public static void ApplySkillMetadata(ConnectionConfig config)
        {
            config.SkillMetadata["generated-by"] = "godot-mcp";
            config.ToolSkillMetadata["ai-game-dev"] = "exclude";
            config.StandaloneSkillMetadata["ai-game-dev"] = "include";
        }

        public override bool IncludeAuthorizationExample => false;
        protected virtual bool UsePowerShell => Skill_Cli.IsWindows;

        /// <summary>
        /// No SKILL.md for a hidden deprecated alias (<see cref="DeprecatedToolAliases"/>, DEV-314), so agents never see two
        /// copies of one tool; the folder an older addon wrote for it is deleted.
        /// </summary>
        public override bool Generate(IEnumerable<IRunTool> tools, string skillsPath, string host)
        {
            if (tools == null)
                return base.Generate(tools!, skillsPath, host);
            var list = tools.Where(t => t != null).ToList();
            var aliases = list.Where(t => DeprecatedToolAliases.IsAlias(t.Name)).ToList();
            var generated = base.Generate(list.Except(aliases), skillsPath, host);
            return (aliases.Count == 0 || Delete(aliases, skillsPath)) && generated;
        }

        protected override void BuildHowToCallHeading(StringBuilder sb)
        {
            sb.AppendLine("Run from the project's root directory:");
            sb.AppendLine();
        }

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
            if (skill.Name == Skill_Cli.SkillId || skill.Name == Skill_App.SkillId)
                return markdown;
            var sb = new StringBuilder(markdown).AppendLine().AppendLine();
            AppendReferences(sb);
            return sb.ToString();
        }

        private static void AppendReferences(StringBuilder sb)
        {
            sb.AppendLine("> CLI usage: the `godot-cli` skill (`godot-cli setup-skills <agent>` writes it); installation: `godot-cli-install`.");
            sb.AppendLine("> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.");
            sb.AppendLine();
        }
    }
}
