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
        public const string ScriptDeleteToolId = "script-delete";

        [AiTool
        (
            ScriptDeleteToolId,
            Title = "Script / Delete",
            DestructiveHint = true
        )]
        [Description("Delete a Godot script file (C# '.cs' or GDScript '.gd') from the res:// filesystem. " +
            "Fails if no file exists at the path. The script's identity (path, language) is captured BEFORE " +
            "deletion and returned for confirmation. The file is removed with DirAccess, along with its " +
            "Godot '.uid' sidecar when present, then the editor filesystem is rescanned and (for a '.cs' file) " +
            "BOUNDED-settles. WARNING: nodes/resources referencing the deleted script will break — verify " +
            "with '" + ScriptReadToolId + "' first. Returns a structured ScriptInfo.")]
        public ScriptInfo Delete
        (
            [Description("res:// path of the script file to delete, ending in '.cs' or '.gd'.")]
            string scriptPath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var resPath = ScriptLang_.RequireScriptResPath(scriptPath, nameof(scriptPath), out var lang);

                if (!FileAccess.FileExists(resPath))
                    throw new ArgumentException($"No script file exists at '{resPath}'.", nameof(scriptPath));

                var rmErr = DirAccess.RemoveAbsolute(resPath);
                if (rmErr != Error.Ok)
                    throw new Exception($"Failed to delete '{resPath}': {rmErr}.");

                // From here the script file is already gone. This is a MULTI-FILE op (script + '.uid'
                // sidecar): if the sidecar removal throws below, the script itself is still deleted. Refresh
                // in 'finally' regardless so the editor index drops the removed entry even on partial failure.
                string status;
                try
                {
                    // Godot 4.3+ emits a '.uid' sidecar next to a script; remove it too when present so no
                    // orphaned uid file is left behind (the analog of Unity's '.meta').
                    var uidPath = resPath + ".uid";
                    if (FileAccess.FileExists(uidPath))
                    {
                        var rmUidErr = DirAccess.RemoveAbsolute(uidPath);
                        if (rmUidErr != Error.Ok)
                            throw new Exception(
                                $"Inconsistent state: script '{resPath}' was deleted but its '.uid' sidecar " +
                                $"'{uidPath}' failed to remove ({rmUidErr}). The script itself is already gone; " +
                                "remove the orphaned sidecar manually.");
                    }
                }
                finally
                {
                    status = RefreshAndSettle(resPath, lang, removed: true);
                }

                return ToScriptInfo(resPath, lang, $"Script deleted; {status}");
            });
        }
    }
}
#endif
