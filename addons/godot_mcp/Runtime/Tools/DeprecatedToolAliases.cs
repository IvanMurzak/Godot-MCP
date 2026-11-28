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
using System;
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Old tool ids that still run under their new name: each is registered as a second, HIDDEN tool
    /// (<c>Visible = false</c>: not in <c>tools/list</c>, still answered by <c>tools/call</c>) so saved permissions,
    /// allowlists and prompts keep working. A hidden alias gets no SKILL.md (<see cref="GodotSkillFileGenerator"/>),
    /// no dock row (<c>ToolManagerAdapter</c>) and no <c>tools</c> RQL row, so agents never see two copies.
    /// Pure-managed and un-gated: the skill generator and the dock adapter compile into every build.
    /// DEPRECATED (DEV-314): remove each alias two releases after its rename shipped; for <c>script-execute</c>
    /// (renamed in 0.43.0) that is 0.45.0. Then delete this class and the <c>[Obsolete]</c> alias tool methods.
    /// </summary>
    public static class DeprecatedToolAliases
    {
        /// <summary>Old id of <c>csharp-exec</c> (DEV-314, renamed in 0.43.0). Remove in 0.45.0.</summary>
        public const string ScriptExecute = "script-execute";

        /// <summary>The new id of <see cref="ScriptExecute"/>.</summary>
        public const string CsharpExec = "csharp-exec";

        /// <summary>Old id → new id.</summary>
        public static readonly IReadOnlyDictionary<string, string> NewNameByAlias = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScriptExecute] = CsharpExec,
        };

        /// <summary>True for a deprecated alias id, which is hidden everywhere it would be listed.</summary>
        public static bool IsAlias(string? name) => name != null && NewNameByAlias.ContainsKey(name);
    }
}
