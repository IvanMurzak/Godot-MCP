/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// The editor and a game built with the in-game opt-in (GODOT_MCP_CODE_EXEC_IN_GAME, docs/ARCHITECTURE.md).
#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME
#nullable enable
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// <c>script-execute</c> inside a RUNNING GAME: compiles C# with Roslyn and runs it on the game's main thread.
    /// Registered only by <c>GodotMcpRuntimeBuilder.WithScriptExecute()</c>. Deliberately NOT an <c>[AiToolType]</c>:
    /// the editor's assembly scan must never pick it up next to the editor <c>script-execute</c>
    /// (<c>Tool_Script.Execute</c>), which has the same name.
    /// </summary>
    public sealed class Tool_GameScript
    {
        public const string ScriptExecuteToolId = "script-execute";
        const string ScriptExecuteTitle = "Script / Execute (in game)";

        [AiTool
        (
            ScriptExecuteToolId,
            Title = ScriptExecuteTitle,
            OpenWorldHint = true
        )]
        [AiSkillDescription("Compiles and runs C# INSIDE THE RUNNING GAME (Roslyn): plain statements with the last expression as the " +
            "result, or a full class with a static method. See the skill body for the modes, usings/include, output and parameters.")]
        [AiSkillBody(
            "## Where it runs\n\n" +
            "Inside the running game process, on its main thread. The game's own C# types, autoloads and the scene tree are " +
            "available: `((SceneTree)Engine.GetMainLoop()).Root` is the root window, `GetTree()` is not (there is no `this`). " +
            "There is no editor: `EditorInterface` and other editor APIs do not exist here.\n\n" +
            "## Modes\n\n" +
            "- **Snippet** (default): plain statements. The last expression is the result " +
            "(`((SceneTree)Engine.GetMainLoop()).Root.GetChildCount()`)." + ScriptExecuteSource.SkillSnippetModeTail +
            "Compile errors point at your own lines (`snippet.cs(2,5)`). Nothing is kept between calls: use `include` for " +
            "shared helpers.\n\n" +
            ScriptExecuteSource.SkillOutputAndUsings +
            "- `include`: helper `.cs` files or folders (recursive), relative to `.ai-game-dev/snippets` in the Godot project " +
            "(a game started from the editor) or next to the game executable (an exported game).\n\n" +
            ScriptExecuteSource.SkillParameters)]
        [Description("Compiles and runs C# inside the running game (main thread; game types, autoloads and the scene tree via " +
            "((SceneTree)Engine.GetMainLoop()).Root; no editor APIs): plain statements with the last expression as the result " +
            "(own usings, local classes, await allowed), or a declared class 'className' with static method 'methodName'. " +
            "Returns the result and the printed output (Console; GD.Print on Godot 4.5+).")]
        public Task<ScriptExecuteResult> Execute
        (
            [Description("C# code. Snippet: 'var root = ((SceneTree)Engine.GetMainLoop()).Root;\\nroot.GetChildCount()'" +
                ScriptExecuteSource.CodeDescriptionTail)]
            string csharpCode,
            [Description(ScriptExecuteSource.ClassNameDescription)]
            string className = "Script",
            [Description(ScriptExecuteSource.MethodNameDescription)]
            string methodName = "Main",
            [Description(ScriptExecuteSource.ParametersDescription)]
            SerializedMemberList? parameters = null,
            [Description(ScriptExecuteSource.IsMethodBodyDescription)]
            bool isMethodBody = false,
            [Description(ScriptExecuteSource.UsingsDescription)]
            string[]? usings = null,
            [Description("Helper .cs files or folders (recursive) compiled with the code, relative to .ai-game-dev/snippets " +
                "(in the project, or next to the game executable).")]
            string[]? include = null
        )
            => ScriptExecutor.Execute(csharpCode, className, methodName, parameters, isMethodBody, usings, include);

        /// <summary>
        /// Makes THIS tool the plugin's <c>script-execute</c>. A game that also registers the addon assembly
        /// (<c>WithToolsFromAssembly</c>) in an editor build (<c>TOOLS</c>, e.g. a game started from the editor) gets the
        /// editor <c>Tool_Script.Execute</c> from that scan, and McpPlugin processes the scan after the explicit types (the
        /// last registration of a name wins). Returns true when it replaced another runner.
        /// </summary>
        internal static bool EnsureRegistered(IToolManager? tools, Reflector reflector)
        {
            if (tools == null)
                return false;
            var current = tools.GetAllTools().FirstOrDefault(t => t.Name == ScriptExecuteToolId);
            if (current is RunTool { Method: var method } && method.DeclaringType == typeof(Tool_GameScript))
                return false;
            if (current != null)
                tools.RemoveTool(ScriptExecuteToolId);
            var execute = typeof(Tool_GameScript).GetMethod(nameof(Execute))!;
            tools.AddTool(ScriptExecuteToolId, RunTool.CreateFromClassMethod(
                reflector, logger: null, ScriptExecuteToolId, typeof(Tool_GameScript), execute,
                title: ScriptExecuteTitle, openWorldHint: true));
            return current != null;
        }
    }
}
#endif
