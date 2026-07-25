/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Skills
    {
        public const string SkillsCreateToolId = "godot-skill-create";

        [AiTool
        (
            SkillsCreateToolId,
            Title = "Skill (Tool) / Create",
            DestructiveHint = false,
            Enabled = false,
            ToolType = McpToolType.System
        )]
        [AiSkillDescription("Create a new skill (MCP tool) for the Godot Editor by writing a C# (.cs) file " +
            "into the project, which Godot compiles into the project assembly. After a rebuild the new tool " +
            "becomes callable through MCP. The file must declare a partial class decorated with " +
            "[AiToolType]; each tool method must be decorated with [AiTool] plus [Description]; every Godot " +
            "API call must run through com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance.Run(); " +
            "editor-only code must be wrapped in #if TOOLS; and the method should return a structured data " +
            "model (for parseable output) or void (for side-effect-only operations). See the body of this " +
            "skill for a full sample and best-practice notes.")]
        [AiSkillBody(SkillsCreateSkillBody)]
        [Description("Create a new skill (MCP tool) using C# code. The code is written to a '.cs' file " +
            "under res:// and compiled into the project assembly by Godot; the tool becomes callable after " +
            "the project is REBUILT (Godot builds C# out-of-band — there is no automatic reload like " +
            "Unity's). An existing file at the path is overwritten.\n" +
            "\n" +
            "The file must declare a partial class decorated with [AiToolType]; each tool method must carry " +
            "[AiTool(\"<tool-name>\", ...)] and [Description]. All Godot API calls must be marshalled onto " +
            "the editor main thread via com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance.Run(), and " +
            "editor-only code (EditorInterface / EditorPlugin / EditorFileSystem) must be wrapped in " +
            "#if TOOLS so it is stripped from an exported game build. Return a data model for structured " +
            "output, or void for side-effect-only operations.\n" +
            "\n" +
            "The CONSUMER project's .csproj must already declare the addon's NuGet package references " +
            "(com.IvanMurzak.ReflectorNet, com.IvanMurzak.McpPlugin) — without them the whole project " +
            "assembly fails to compile.")]
        public ScriptInfo Create
        (
            [Description("res:// path for the C# (.cs) file to create, e.g. 'res://Skills/Tool_Sample.cs'. " +
                "Must be a res:// file path ending in '.cs' with no '..' segment. GDScript ('.gd') is not " +
                "accepted — only C# can declare MCP tools.")]
            string path,

            [Description("C# source code for the skill tool file.")]
            string code
        )
        {
            // Validate BEFORE reaching for the host so a bad path fails identically whether or not an
            // editor is attached (and so the guards stay unit-testable without one).
            var resPath = SkillsToolPaths.RequireSkillFileResPath(path, nameof(path));

            if (code == null)
                throw new ArgumentNullException(nameof(code));

            return RequireHost().CreateSkillFile(resPath, code);
        }
    }
}
