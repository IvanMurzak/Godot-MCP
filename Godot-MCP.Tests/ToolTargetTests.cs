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

        // Tool_Reflection is not compiled into this suite (it needs the Godot reflector): its source carries the marker.
        [Fact]
        public void Reflection_family_is_a_runtime_tool()
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "addons", "godot_mcp", "Runtime", "Tools", "Tool_Reflection.cs"));
            Assert.Contains("[AiToolType(Target = ToolTarget.Runtime)]\n    public partial class Tool_Reflection", source.Replace("\r\n", "\n"));
        }

        static string RepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Godot-MCP.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Godot-MCP.csproj not found above " + AppContext.BaseDirectory);
        }

        [Theory]
        [InlineData(typeof(Tool_Ping))]
        [InlineData(typeof(Tool_Skills))]
        public void Shared_families_stay_on_both_connections(Type family) => Assert.Equal(ToolTarget.Both, TargetOf(family));
    }
}
