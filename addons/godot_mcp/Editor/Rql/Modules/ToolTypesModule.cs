/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Modules
{
    /// <summary>
    /// Adapters for the parameter types of the kept tools (RqlToolBinding): NodeRef (script-attach-to-node, screenshot-*) and
    /// SerializedMemberList (script-execute parameters).
    /// </summary>
    public sealed class ToolTypesModule : IRqlModule
    {
        public int Order => 10;

        public void Configure(RqlModuleContext ctx) => ctx.AddAdapter(new Godot_NodeRef_Adapter())
            .AddAdapter(new ReflectorNet_SerializedMemberList_Adapter());
    }
}
#endif
