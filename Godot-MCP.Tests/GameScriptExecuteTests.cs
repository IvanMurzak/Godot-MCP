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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Runtime;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet;
using Xunit;
using McpVersion = com.IvanMurzak.McpPlugin.Common.Version;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The in-game <c>csharp-exec</c> (<see cref="Tool_GameScript"/>, <see cref="GodotMcpRuntimeBuilder.WithCsharpExec"/>):
    /// opt-in registration, no clash with the editor tool of the same name, and where <c>include</c> paths start. This
    /// suite builds with <c>GODOT_MCP_CODE_EXEC_IN_GAME</c>, i.e. as an opted-in game. Running a snippet needs the Godot
    /// main thread: that is the runtime harness (<c>Godot-Tests/Harness/RuntimeHarness.cs</c>).
    /// </summary>
    public class GameScriptExecuteTests
    {
        [Fact]
        public void WithCsharpExec_IsOffByDefault_AndNotImpliedByAnAssemblyScan()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithToolsFromAssembly(typeof(Tool_GameScript).Assembly));

            Assert.DoesNotContain(typeof(Tool_GameScript), builder.ToolTypes);
        }

        [Fact]
        public void WithCsharpExec_RegistersTheInGameToolOnce()
        {
#pragma warning disable CS0618 // the obsolete WithScriptExecute() is the same registration (DEV-314)
            var builder = GodotMcpRuntime.Initialize(b => b.WithCsharpExec().WithScriptExecute());
#pragma warning restore CS0618

            Assert.Equal(new[] { typeof(Tool_GameScript) }, builder.ToolTypes);
        }

        [Fact]
        public void UnavailableWarning_ShowsTheCsprojOptIn()
        {
            Assert.Contains("<GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>", GodotMcpRuntimeBuilder.CsharpExecUnavailableMessage);
            Assert.Contains("GODOT_MCP_CODE_EXEC_IN_GAME", GodotMcpRuntimeBuilder.CsharpExecUnavailableMessage);
        }

        [Fact]
        public void InGameTool_IsNoToolType_SoTheEditorScanNeverRegistersIt()
        {
            Assert.False(Attribute.IsDefined(typeof(Tool_GameScript), typeof(AiToolTypeAttribute)));
            var tool = typeof(Tool_GameScript).GetMethod(nameof(Tool_GameScript.Execute))!.GetCustomAttribute<AiToolAttribute>();
            Assert.Equal("csharp-exec", tool?.Name);
        }

        [Fact]
        public void EnsureRegistered_ReplacesAScannedEditorCsharpExec()
        {
            // McpPlugin processes explicit types first and the assembly scan after them; the last runner of a name wins.
            // So a game that registers the addon assembly in an editor build gets the editor csharp-exec from the scan.
            var reflector = new Reflector();
            var plugin = new McpPluginBuilder(new McpVersion())
                .WithTools(typeof(Tool_GameScript))
                .WithToolsFromAssembly(new[] { typeof(ScannedEditorScript).Assembly })
                .Build(reflector);
            var tools = plugin.McpManager.ToolManager!;
            Assert.Equal(typeof(ScannedEditorScript), Winner(tools));

            Assert.True(Tool_GameScript.EnsureRegistered(tools, reflector));
            Assert.Equal(typeof(Tool_GameScript), Winner(tools));

            // The hand-registered runner carries the attribute's title and MCP hints, as a scanned one would.
            var attribute = typeof(Tool_GameScript).GetMethod(nameof(Tool_GameScript.Execute))!.GetCustomAttribute<AiToolAttribute>()!;
            var registered = tools.GetAllTools().Single(t => t.Name == Tool_GameScript.CsharpExecToolId);
            Assert.Equal(attribute.Title, registered.Title);
            Assert.Equal(attribute.ReadOnlyHintValue, registered.ReadOnlyHint);
            Assert.Equal(attribute.DestructiveHintValue, registered.DestructiveHint);
            Assert.Equal(attribute.IdempotentHintValue, registered.IdempotentHint);
            Assert.Equal(attribute.OpenWorldHintValue, registered.OpenWorldHint);

            // The hidden alias is replaced too, and stays hidden.
            Assert.Equal(typeof(Tool_GameScript), Winner(tools, Tool_GameScript.ScriptExecuteAliasToolId));
            Assert.False(tools.IsToolVisible(Tool_GameScript.ScriptExecuteAliasToolId));

            Assert.False(Tool_GameScript.EnsureRegistered(tools, reflector));
            Assert.Single(tools.GetAllTools(), t => t.Name == Tool_GameScript.CsharpExecToolId);
            Assert.Single(tools.GetAllTools(), t => t.Name == Tool_GameScript.ScriptExecuteAliasToolId);
        }

        [Fact]
        public async Task OldId_IsAHiddenAliasThatRunsTheSameTool()
        {
            // DEV-314: script-execute is not listed (Visible = false) but still answers tools/call, with the same arguments.
            var reflector = new Reflector();
            var plugin = new McpPluginBuilder(new McpVersion()).WithTools(typeof(Tool_GameScript)).Build(reflector);
            var tools = plugin.McpManager.ToolManager!;

            Assert.Equal("script-execute", Tool_GameScript.ScriptExecuteAliasToolId);
            Assert.True(tools.IsToolVisible(Tool_GameScript.CsharpExecToolId));
            Assert.False(tools.IsToolVisible(Tool_GameScript.ScriptExecuteAliasToolId));
            var listed = (await tools.RunListTool(new RequestListTool())).Value!;
            Assert.True(listed.Single(t => t.Name == Tool_GameScript.CsharpExecToolId).Visible);
            Assert.False(listed.Single(t => t.Name == Tool_GameScript.ScriptExecuteAliasToolId).Visible);

            var alias = (RunTool)tools.GetAllTools().Single(t => t.Name == Tool_GameScript.ScriptExecuteAliasToolId);
            var tool = (RunTool)tools.GetAllTools().Single(t => t.Name == Tool_GameScript.CsharpExecToolId);
            Assert.Equal(
                tool.Method.GetParameters().Select(p => (p.Name, p.ParameterType, p.DefaultValue?.ToString())),
                alias.Method.GetParameters().Select(p => (p.Name, p.ParameterType, p.DefaultValue?.ToString())));
            Assert.Equal(tool.Method.ReturnType, alias.Method.ReturnType);
            Assert.Equal(Tool_GameScript.CsharpExecToolId, DeprecatedToolAliases.NewNameByAlias[Tool_GameScript.ScriptExecuteAliasToolId]);

            // tools/call reaches it: a missing tool answers "not found", the alias runs the executor (here: its empty-code check,
            // which needs no Godot main thread).
            var args = new Dictionary<string, JsonElement> { ["csharpCode"] = JsonDocument.Parse("\"\"").RootElement };
            var call = await tools.RunCallTool(new RequestCallTool(Tool_GameScript.ScriptExecuteAliasToolId, args));
            Assert.Contains("'csharpCode' is null or empty", call.Message);
            var missing = await tools.RunCallTool(new RequestCallTool("no-such-tool", args));
            Assert.Contains("Tool with Name 'no-such-tool' not found", missing.Message);
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

        static Type? Winner(IToolManager tools, string toolId = Tool_GameScript.CsharpExecToolId)
            => (tools.GetAllTools().Single(t => t.Name == toolId) as RunTool)?.Method.DeclaringType;

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
        [AiTool("csharp-exec")]
        public string Execute(string csharpCode) => csharpCode;

        [AiTool("script-execute", Visible = false)]
        public string ExecuteDeprecatedAlias(string csharpCode) => csharpCode;
    }
}
