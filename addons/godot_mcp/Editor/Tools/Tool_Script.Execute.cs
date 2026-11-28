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
        public const string CsharpExecToolId = Tool_GameScript.CsharpExecToolId;
        /// <summary>DEV-314: the old id of <see cref="CsharpExecToolId"/>, a hidden alias. Remove in 0.45.0.</summary>
        public const string ScriptExecuteAliasToolId = Tool_GameScript.ScriptExecuteAliasToolId;
        const string CsharpExecTitle = "C# / Exec";

        [AiTool
        (
            CsharpExecToolId,
            Title = CsharpExecTitle,
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false,
            OpenWorldHint = true
        )]
        [AiSkillDescription("Compiles and runs C# in the Godot editor (Roslyn): plain statements with the last expression as the result, " +
            "or a full class with a static method. See the skill body for the modes, usings/include, output and parameters.")]
        [AiSkillBody(
            "## Modes\n\n" +
            "- **Snippet** (default): plain statements. The last expression is the result " +
            "(`EditorInterface.Singleton.GetEditedSceneRoot().Name`)." + ScriptExecuteSource.SkillSnippetModeTail +
            "The code runs on the editor main thread; Godot APIs and the game's compiled C# types are available (build with " +
            "script-build to see new game code). Compile errors point at your own lines (`snippet.cs(2,5)`). Nothing is kept between " +
            "calls: use `include` for shared helpers. Edits the code makes are not in the editor's undo history; for undoable " +
            "edits use `EditorInterface.Singleton.GetEditorUndoRedo()` or rql-mutate.\n\n" +
            ScriptExecuteSource.SkillOutputAndUsings +
            "- `include`: helper `.cs` files or folders (recursive), relative to `<project>/.ai-game-dev/snippets`, compiled with the " +
            "snippet. Godot does not compile that folder into the game.\n\n" +
            ScriptExecuteSource.SkillParameters)]
        [Description("Prefer rql-mutate for scene, resource and project edits: one line instead of C# to compile. " +
            "Compiles and runs C# in the Godot editor: plain statements with the last expression as the result " +
            "(own usings, local classes, await allowed), or a declared class 'className' with static method 'methodName'. " +
            "Returns the result and the printed output (Console; GD.Print on Godot 4.5+). " + ScriptExecuteSource.GuardDescription)]
        public Task<ScriptExecuteResult> Execute
        (
            [Description("C# code. Snippet: 'var root = EditorInterface.Singleton.GetEditedSceneRoot();\\nroot.GetChildCount()'" +
                ScriptExecuteSource.CodeDescriptionTail)]
            string csharpCode,
            [Description(ScriptExecuteSource.ClassNameDescription)]
            string className = "Script",
            [Description(ScriptExecuteSource.MethodNameDescription)]
            string methodName = "Main",
            [Description(ScriptExecuteSource.ParametersDescription)]
            [Editor.Rql.ToolFormat.RqlArgument("Values for the snippet's / method's parameters by name, e.g. {node: ref(\"Node3D#12\"), n: 6}. " +
                "Leave empty if there are none.")]
            SerializedMemberList? parameters = null,
            [Description(ScriptExecuteSource.IsMethodBodyDescription)]
            bool isMethodBody = false,
            [Description(ScriptExecuteSource.UsingsDescription)]
            string[]? usings = null,
            [Description("Helper .cs files or folders (recursive) compiled with the code, relative to <project>/.ai-game-dev/snippets.")]
            string[]? include = null,
            [Description(ScriptExecuteSource.TimeoutDescription)]
            int timeout = ScriptExecuteSource.DefaultTimeoutSeconds
        )
            => ScriptExecutor.Execute(csharpCode, className, methodName, parameters, isMethodBody, usings, include, timeout);

        /// <summary>
        /// DEV-314: the old id <c>script-execute</c>, kept as a HIDDEN alias of <see cref="Execute"/> (not listed, still
        /// callable) so saved permissions and prompts keep working. Remove in 0.45.0 with <see cref="DeprecatedToolAliases"/>.
        /// </summary>
        [Obsolete("DEV-314: hidden alias 'script-execute' of csharp-exec. Remove in 0.45.0.")]
        [AiTool(ScriptExecuteAliasToolId, Title = CsharpExecTitle, ReadOnlyHint = false, DestructiveHint = true,
            IdempotentHint = false, OpenWorldHint = true, Visible = false)]
        [Description(ScriptExecuteSource.AliasDescription)]
        public Task<ScriptExecuteResult> ExecuteDeprecatedAlias
        (
            [Description("C# code.")] string csharpCode,
            [Description(ScriptExecuteSource.ClassNameDescription)] string className = "Script",
            [Description(ScriptExecuteSource.MethodNameDescription)] string methodName = "Main",
            [Description(ScriptExecuteSource.ParametersDescription)] SerializedMemberList? parameters = null,
            [Description(ScriptExecuteSource.IsMethodBodyDescription)] bool isMethodBody = false,
            [Description(ScriptExecuteSource.UsingsDescription)] string[]? usings = null,
            [Description("Helper .cs files or folders compiled with the code, relative to <project>/.ai-game-dev/snippets.")] string[]? include = null,
            [Description(ScriptExecuteSource.TimeoutDescription)] int timeout = ScriptExecuteSource.DefaultTimeoutSeconds
        )
            => Execute(csharpCode, className, methodName, parameters, isMethodBody, usings, include, timeout);
    }
}
#endif
