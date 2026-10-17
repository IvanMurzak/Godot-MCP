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
        public const string SchemaToolId = "rql-schema";

        [AiTool(SchemaToolId, Title = "RQL / Schema", Enabled = true, ReadOnlyHint = true, OpenWorldHint = false)]
        [AiSkillDescription(RqlGuide.SchemaBlurb)]
        [AiSkillBody(RqlGuide.SchemaDescription)]
        [Description(RqlGuide.SchemaDescription)]
        public ResponseCallTool Schema
        (
            [Description("Optional type name, e.g. \"Node3D\", \"OmniLight3D\", \"StandardMaterial3D\", \"Vector3\", \"editor\"")]
            string? type = null
        )
        {
            return Run(() => RqlGodotSchema.Render(RqlGodot.Engine, string.IsNullOrWhiteSpace(type) ? null : type!.Trim()),
                ResponseCallTool.Success);
        }
    }
}
#endif
