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
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Rql
    {
        public const string MutateToolId = "rql-mutate";

        [AiTool(MutateToolId, Title = "RQL / Mutate", Enabled = true, DestructiveHint = true, OpenWorldHint = false)]
        [AiSkillDescription(RqlGuide.MutateBlurb)]
        [AiSkillBody(RqlGuide.MutateDescription)]
        [Description(RqlGuide.MutateDescription)]
        public ResponseCallTool Mutate
        (
            [Description("RQL query ending with a set or call stage, e.g. nodes | where name == \"Player\" | set Visible = false")]
            string query,
            [Description("true = preview the receipt without changing anything")]
            bool dry_run = false,
            [Description(RqlGuide.MaxTargetsDescription)]
            int max_targets = RqlTargetLimit.Default
        )
        {
            if (query == null) return ResponseCallTool.Error(MissingQueryText);
            return Run(() => RqlGodot.Mutate(query, dry_run, RqlTargetLimit.Requested(max_targets)));
        }
    }
}
#endif
