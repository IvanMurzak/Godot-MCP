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
        public const string QueryToolId = "rql-query";

        [AiTool(QueryToolId, Title = "RQL / Query", Enabled = true, ReadOnlyHint = true, OpenWorldHint = false)]
        [AiSkillDescription(RqlGuide.QueryBlurb)]
        [AiSkillBody(RqlGuide.QueryDescription)]
        [Description(RqlGuide.QueryDescription)]
        public ResponseCallTool Query
        (
            [Description("RQL query, e.g. nodes | where $ is Node3D | select $ref name Position")]
            string query
        )
        {
            if (query == null) return ResponseCallTool.Error(MissingQueryText);
            return Run(() => RqlGodot.RunQuery(() => RqlGodot.Engine.Execute(query)));
        }
    }
}
#endif
