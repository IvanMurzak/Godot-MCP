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
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Script
    {
        public const string ScriptExecuteToolId = "script-execute";

        [AiTool
        (
            ScriptExecuteToolId,
            Title = "Script / Execute",
            OpenWorldHint = true
        )]
        [AiSkillDescription("Compiles and runs C# in the Godot editor (Roslyn): plain statements with the last expression as the result, " +
            "or a full class with a static method. See the skill body for the modes, usings/include, output and parameters.")]
        [AiSkillBody(
            "## Modes\n\n" +
            "- **Snippet** (default): plain statements. The last expression is the result " +
            "(`EditorInterface.Singleton.GetEditedSceneRoot().Name`). Own `using` lines, local classes and functions are allowed; " +
            "`await` works. Usings for System, LINQ and Godot are already there; an unknown type name (also one of the game's own " +
            "classes) is imported automatically when exactly one namespace has it.\n" +
            "- **Full code**: when `csharpCode` declares class `className` with a static method `methodName`, that method runs.\n" +
            "- `isMethodBody=true` forces snippet mode.\n\n" +
            "The code runs on the editor main thread; Godot APIs and the game's compiled C# types are available (rebuild the " +
            "project to see new game code). Compile errors point at your own lines (`snippet.cs(2,5)`). Nothing is kept between " +
            "calls: use `include` for shared helpers.\n\n" +
            "## Output\n\n" +
            "`result` holds the value (type name and serialized value; `Success` when nothing is returned). `logs` holds what " +
            "the code printed: `Console` always; `GD.Print`, `GD.PushWarning` / `GD.PushError` when the project builds with Godot.NET.Sdk 4.5+.\n\n" +
            "## usings / include\n\n" +
            "- `usings`: extra namespaces, e.g. `[\"MyGame.Gameplay\"]`.\n" +
            "- `include`: helper `.cs` files or folders (recursive), relative to `<project>/.ai-game-dev/snippets`, compiled with the " +
            "snippet. Godot does not compile that folder into the game.\n\n" +
            "## Parameters\n\n" +
            "Parameters become the snippet's variables (or the method's arguments, in order); each has `name`, `typeName` and a value.")]
        [Description("Compiles and runs C# in the Godot editor: plain statements with the last expression as the result " +
            "(own usings, local classes, await allowed), or a declared class 'className' with static method 'methodName'. " +
            "Returns the result and the printed output (Console; GD.Print on Godot 4.5+).")]
        public Task<ScriptExecuteResult> Execute
        (
            [Description("C# code. Snippet: 'var root = EditorInterface.Singleton.GetEditedSceneRoot();\\nroot.GetChildCount()'. " +
                "Full: 'public class Script { public static int Main() { return 42; } }'.")]
            string csharpCode,
            [Description("Full mode: the class whose static method runs (namespace-qualified or not).")]
            string className = "Script",
            [Description("Full mode: the static method to run.")]
            string methodName = "Main",
            [Description("Parameters, each with 'name' and 'typeName': the snippet's variables or the method's arguments in order. " +
                "Leave empty when none.")]
            [Editor.Rql.ToolFormat.RqlArgument("Values for the snippet's / method's parameters by name, e.g. {node: ref(\"Node3D#12\"), n: 6}. " +
                "Leave empty if there are none.")]
            SerializedMemberList? parameters = null,
            [Description("Force snippet mode even when the code declares 'className'.")]
            bool isMethodBody = false,
            [Description("Extra namespaces for snippet mode, e.g. [\"MyGame.Gameplay\"].")]
            string[]? usings = null,
            [Description("Helper .cs files or folders (recursive) compiled with the code, relative to <project>/.ai-game-dev/snippets.")]
            string[]? include = null
        )
            => ScriptExecutor.Execute(csharpCode, className, methodName, parameters, isMethodBody, usings, include);
    }
}
#endif
