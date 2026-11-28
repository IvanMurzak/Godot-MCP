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
using System.Linq;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Old tool ids that still run under their new name: <see cref="Register"/> adds each as a second, HIDDEN tool
    /// (<c>Visible = false</c>: not in <c>tools/list</c>, still answered by <c>tools/call</c>) over the new tool's own
    /// method, so saved permissions, allowlists and prompts keep working. A hidden alias gets no SKILL.md
    /// (<see cref="GodotSkillFileGenerator"/>), no dock row (<c>ToolManagerAdapter</c>) and no <c>tools</c> RQL row, so
    /// agents never see two copies. Un-gated: the skill generator and the dock adapter compile into every build.
    /// DEPRECATED (DEV-314): remove each alias two releases after its rename shipped; for <c>script-execute</c>
    /// (renamed in 0.43.0) that is 0.45.0. Then delete this class and its two <see cref="Register"/> calls.
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

        /// <summary>
        /// Register every alias whose new tool is registered, as a hidden tool over that tool's method (same arguments,
        /// title, hints and result); an alias registered earlier is replaced, so it always runs the current winner of the
        /// new id. Call it after the tool list is final and before the tools are wrapped. Returns the aliases registered.
        /// </summary>
        public static int Register(IToolManager? tools, Reflector reflector)
        {
            if (tools == null)
                return 0;
            var registered = 0;
            foreach (var alias in NewNameByAlias)
            {
                if (tools.GetAllTools().FirstOrDefault(t => t.Name == alias.Value) is not RunTool { Method: { DeclaringType: { } type } method } tool)
                    continue;
                if (tools.GetAllTools().Any(t => t.Name == alias.Key))
                    tools.RemoveTool(alias.Key);
                tools.AddTool(alias.Key, RunTool.CreateFromClassMethod(reflector, logger: null, alias.Key, type, method,
                    title: tool.Title,
                    readOnlyHint: tool.ReadOnlyHint,
                    destructiveHint: tool.DestructiveHint,
                    idempotentHint: tool.IdempotentHint,
                    openWorldHint: tool.OpenWorldHint,
                    visible: false));
                registered++;
            }
            return registered;
        }
    }
}
