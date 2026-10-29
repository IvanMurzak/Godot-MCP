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
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Which connection registers a tool family (McpPlugin's <see cref="ToolTarget"/>, read at Build() against the client role):
    /// the game-only families are <see cref="ToolTarget.Runtime"/>, so the editor never registers them and the server sends their
    /// calls to the game while the editor is connected; shared tools such as ping stay <see cref="ToolTarget.Both"/>.
    /// </summary>
    public class ToolTargetTests
    {
        static ToolTarget TargetOf(Type family) => family.GetCustomAttribute<AiToolTypeAttribute>()!.Target;

        [Theory]
        [InlineData(typeof(Tool_RuntimeErrors))]
        [InlineData(typeof(Tool_Console))]
        public void Game_only_families_are_runtime_tools(Type family) => Assert.Equal(ToolTarget.Runtime, TargetOf(family));

        [Theory]
        [InlineData(typeof(Tool_Ping))]
        [InlineData(typeof(Tool_Skills))]
        public void Shared_families_stay_on_both_connections(Type family) => Assert.Equal(ToolTarget.Both, TargetOf(family));
    }
}
