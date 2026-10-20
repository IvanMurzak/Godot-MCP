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
using System.IO;
using System.Linq;
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Runtime;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using Xunit;
using McpVersion = com.IvanMurzak.McpPlugin.Common.Version;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The in-game <c>script-execute</c> (<see cref="Tool_GameScript"/>, <see cref="GodotMcpRuntimeBuilder.WithScriptExecute"/>):
    /// opt-in registration, no clash with the editor tool of the same name, and where <c>include</c> paths start. This
    /// suite builds with <c>GODOT_MCP_CODE_EXEC_IN_GAME</c>, i.e. as an opted-in game. Running a snippet needs the Godot
    /// main thread: that is the runtime harness (<c>Godot-Tests/Harness/RuntimeHarness.cs</c>).
    /// </summary>
    public class GameScriptExecuteTests
    {
        [Fact]
        public void WithScriptExecute_IsOffByDefault_AndNotImpliedByAnAssemblyScan()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithToolsFromAssembly(typeof(Tool_GameScript).Assembly));

            Assert.DoesNotContain(typeof(Tool_GameScript), builder.ToolTypes);
        }

        [Fact]
        public void WithScriptExecute_RegistersTheInGameToolOnce()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithScriptExecute().WithScriptExecute());

            Assert.Equal(new[] { typeof(Tool_GameScript) }, builder.ToolTypes);
        }

        [Fact]
        public void UnavailableWarning_ShowsTheCsprojOptIn()
        {
            Assert.Contains("<GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>", GodotMcpRuntimeBuilder.ScriptExecuteUnavailableMessage);
            Assert.Contains("GODOT_MCP_CODE_EXEC_IN_GAME", GodotMcpRuntimeBuilder.ScriptExecuteUnavailableMessage);
        }

        [Fact]
        public void InGameTool_IsNoToolType_SoTheEditorScanNeverRegistersIt()
        {
            Assert.False(Attribute.IsDefined(typeof(Tool_GameScript), typeof(AiToolTypeAttribute)));
            var tool = typeof(Tool_GameScript).GetMethod(nameof(Tool_GameScript.Execute))!.GetCustomAttribute<AiToolAttribute>();
            Assert.Equal("script-execute", tool?.Name);
        }

        [Fact]
        public void EnsureRegistered_ReplacesAScannedEditorScriptExecute()
        {
            // McpPlugin processes explicit types first and the assembly scan after them; the last runner of a name wins.
            // So a game that registers the addon assembly in an editor build gets the editor script-execute from the scan.
            var reflector = new Reflector();
            var plugin = new McpPluginBuilder(new McpVersion())
                .WithTools(typeof(Tool_GameScript))
                .WithToolsFromAssembly(new[] { typeof(ScannedEditorScript).Assembly })
                .Build(reflector);
            var tools = plugin.McpManager.ToolManager!;
            Assert.Equal(typeof(ScannedEditorScript), Winner(tools));

            Assert.True(Tool_GameScript.EnsureRegistered(tools, reflector));
            Assert.Equal(typeof(Tool_GameScript), Winner(tools));
            Assert.False(Tool_GameScript.EnsureRegistered(tools, reflector));
            Assert.Single(tools.GetAllTools(), t => t.Name == Tool_GameScript.ScriptExecuteToolId);
        }

        [Fact]
        public void EnsureRegistered_KeepsTheInGameToolWhenItAlreadyWins()
        {
            var reflector = new Reflector();
            var plugin = new McpPluginBuilder(new McpVersion()).WithTools(typeof(Tool_GameScript)).Build(reflector);
            var tools = plugin.McpManager.ToolManager!;

            Assert.False(Tool_GameScript.EnsureRegistered(tools, reflector));
            Assert.Equal(typeof(Tool_GameScript), Winner(tools));
        }

        [Fact]
        public void EnsureRegistered_ToleratesNoToolManager()
            => Assert.False(Tool_GameScript.EnsureRegistered(null, new Reflector()));

        static Type? Winner(IToolManager tools)
            => (tools.GetAllTools().Single(t => t.Name == Tool_GameScript.ScriptExecuteToolId) as RunTool)?.Method.DeclaringType;

        [Fact]
        public void ProjectRoot_IsTheProjectInEditorBuilds_AndTheExecutableFolderInExports()
        {
            var exe = Path.Combine(Path.GetTempPath(), "game", "MyGame.exe");

            Assert.Equal("C:/project/", ScriptExecuteSource.ProjectRoot(editorBuild: true, "C:/project/", exe));
            Assert.Equal(Path.GetDirectoryName(exe), ScriptExecuteSource.ProjectRoot(editorBuild: false, "res://", exe));
            Assert.Equal("res-root", ScriptExecuteSource.ProjectRoot(editorBuild: false, "res-root", ""));
        }
    }

    /// <summary>Stands in for the editor <c>Tool_Script.Execute</c> (<c>#if TOOLS</c>, not compiled here).</summary>
    [AiToolType]
    public class ScannedEditorScript
    {
        [AiTool("script-execute")]
        public string Execute(string csharpCode) => csharpCode;
    }
}
