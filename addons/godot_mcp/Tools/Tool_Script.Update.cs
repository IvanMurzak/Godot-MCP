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
        public const string ScriptUpdateToolId = "script-update";

        [AiTool
        (
            ScriptUpdateToolId,
            Title = "Script / Update",
            DestructiveHint = true,
            IdempotentHint = true
        )]
        [Description("Overwrite an EXISTING Godot script file (C# '.cs' or GDScript '.gd') under res:// with " +
            "the provided source. Fails if no file exists at the path — use '" + ScriptCreateToolId + "' to " +
            "create one. GDScript content is syntax-validated before write (invalid '.gd' is rejected and the " +
            "existing file is left untouched); C# is accepted as-is (no in-editor compiler) and the post-write " +
            "build settle surfaces compile errors. For a '.cs' file the editor filesystem reimports and " +
            "BOUNDED-settles before returning (a project rebuild then loads the changed type); a '.gd' file " +
            "only needs a filesystem update so the editor re-parses it. Use '" + ScriptReadToolId + "' to " +
            "inspect the current content first. Returns a structured ScriptInfo.")]
        public ScriptInfo Update
        (
            [Description("res:// path of the existing script to overwrite, ending in '.cs' or '.gd'.")]
            string scriptPath,
            [Description("New source code to write (replaces the file's entire content).")]
            string content
        )
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            return MainThread.Instance.Run(() =>
            {
                var resPath = ScriptLang_.RequireScriptResPath(scriptPath, nameof(scriptPath), out var lang);

                if (!FileAccess.FileExists(resPath))
                    throw new ArgumentException(
                        $"No script file exists at '{resPath}'. Use '{ScriptCreateToolId}' to create it.",
                        nameof(scriptPath));

                if (!ValidateSyntax(content, lang, out var syntaxError))
                    throw new ArgumentException(syntaxError, nameof(content));

                return WriteScript(resPath, lang, content, "updated");
            });
        }
    }
}
#endif
