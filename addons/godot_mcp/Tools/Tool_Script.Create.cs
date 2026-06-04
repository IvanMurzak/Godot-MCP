/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Script
    {
        public const string ScriptCreateToolId = "script-create";

        [AiTool
        (
            ScriptCreateToolId,
            Title = "Script / Create"
        )]
        [Description("Create a NEW Godot script file (C# '.cs' or GDScript '.gd') under res:// with the " +
            "provided source. Fails if a file already exists at the path — use '" + ScriptUpdateToolId +
            "' to overwrite. GDScript content is syntax-validated before write (invalid '.gd' is rejected and " +
            "nothing is written); C# is accepted as-is (no in-editor compiler) and the post-write build settle " +
            "surfaces compile errors. Missing parent directories are created. For a '.cs' file the editor " +
            "filesystem reimports and BOUNDED-settles before returning (a project rebuild then loads the new " +
            "type); a '.gd' file only needs a filesystem update. Returns a structured ScriptInfo.")]
        public ScriptInfo Create
        (
            [Description("res:// path for the new script, ending in '.cs' (C#) or '.gd' (GDScript), e.g. " +
                "'res://scripts/Enemy.cs' or 'res://scripts/player.gd'.")]
            string scriptPath,
            [Description("Source code for the new script file.")]
            string content
        )
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            return MainThread.Instance.Run(() =>
            {
                var resPath = ScriptLang_.RequireScriptResPath(scriptPath, nameof(scriptPath), out var lang);

                // Guard on the file on disk (like Tool_Resource.Create): an existing file is never silently
                // clobbered by 'create' — that is what 'update' is for.
                if (FileAccess.FileExists(resPath))
                    throw new ArgumentException(
                        $"A file already exists at '{resPath}'. Use '{ScriptUpdateToolId}' to overwrite it, " +
                        $"or '{ScriptDeleteToolId}' to remove it first.", nameof(scriptPath));

                if (!ValidateSyntax(content, lang, out var syntaxError))
                    throw new ArgumentException(syntaxError, nameof(content));

                return WriteScript(resPath, lang, content, "created");
            });
        }
    }
}
#endif
