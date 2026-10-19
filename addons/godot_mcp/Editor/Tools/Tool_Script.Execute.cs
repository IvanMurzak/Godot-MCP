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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.CodeExec;
using com.IvanMurzak.CodeExec.Roslyn;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

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

    /// <summary>
    /// Runs <c>script-execute</c> on Code-Exec (NuGet <c>com.IvanMurzak.CodeExec</c> + the source-only Roslyn adapter).
    /// Compiles on the calling thread (MCP calls arrive off the main thread), runs on the editor main thread. Each run is
    /// loaded into its own collectible context and unloaded afterwards, so the result is serialized before that.
    /// The runner lives in a static of THIS (reloadable) assembly and subscribes to nothing, so a C# rebuild drops it.
    /// </summary>
    static class ScriptExecutor
    {
        const int TimeoutMs = 120_000;

        static CodeRunner? _runner;
        static readonly object _runnerLock = new object();

        // One snippet runs at a time: an awaiting snippet frees the main thread, and overlapping runs would interleave the
        // process-wide Console / engine-logger capture (Code-Exec runs must not overlap).
        static readonly SemaphoreSlim _runGate = new SemaphoreSlim(1, 1);
        static int _inFlight;

        /// <summary>A call is compiling or running (an awaiting snippet neither moves the tool-call counter nor stalls frames).</summary>
        public static bool IsBusy => Volatile.Read(ref _inFlight) > 0;

        public static async Task<ScriptExecuteResult> Execute(string csharpCode, string className, string methodName,
            SerializedMemberList? parameters, bool isMethodBody, string[]? usings, string[]? include)
        {
            if (string.IsNullOrEmpty(csharpCode))
                throw new Exception("'csharpCode' is null or empty. Please provide valid C# code to execute.");

            Interlocked.Increment(ref _inFlight);
            try
            {
                return await Run(csharpCode, className, methodName, parameters, isMethodBody, usings, include);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        static async Task<ScriptExecuteResult> Run(string csharpCode, string className, string methodName,
            SerializedMemberList? parameters, bool isMethodBody, string[]? usings, string[]? include)
        {
            var runner = Runner();
            // Only the parameter TYPES are needed to compile; the values are deserialized on the main thread.
            var request = new ExecuteRequest
            {
                Code = csharpCode,
                Usings = usings ?? Array.Empty<string>(),
                Include = include ?? Array.Empty<string>(),
                Parameters = parameters?.Select((p, i) => new SnippetParameter(ParameterName(p, i), ParameterType(p, i), null)).ToList()
                    ?? (IReadOnlyList<SnippetParameter>)Array.Empty<SnippetParameter>(),
                TimeoutMs = TimeoutMs,
            };
            if (!isMethodBody && ScriptExecuteSource.FullSourceEntryType(csharpCode, className, methodName) is { } entryType)
            {
                request.EntryType = entryType;
                request.EntryMethod = methodName;
            }
            var prepared = runner.Prepare(request);
            if (!prepared.Succeeded)
                throw new Exception(ScriptExecuteSource.Describe(runner.Run(prepared))); // a failed Prepare runs nothing

            // Awaits inside the snippet resume on the main thread (Godot's synchronization context).
            return await MainThread.Instance.RunAsync(() => RunOnMainThread(runner, prepared, parameters)).Unwrap();
        }

        static async Task<ScriptExecuteResult> RunOnMainThread(CodeRunner runner, PreparedSnippet prepared, SerializedMemberList? parameters)
        {
            if (parameters != null)
            {
                var reflector = GodotMcpReflector.GetOrCreate();
                prepared = prepared.WithValues(parameters.Select(p => reflector.Deserialize(data: p)).ToList());
            }

            ExecuteResult result;
            await _runGate.WaitAsync();
            try
            {
                result = await runner.RunAsync(prepared);
            }
            finally
            {
                _runGate.Release();
            }
            if (!result.Success)
                throw new Exception(ScriptExecuteSource.Describe(result));

            return new ScriptExecuteResult
            {
                Result = result.Value as SerializedMember ?? NoValue(result.ReturnType),
                Logs = result.Logs.Count > 0 ? ScriptExecuteSource.LogLines(result.Logs) : null,
            };
        }

        /// <summary>Typed null reports the result type; nothing returned (System.Void) reads "Success".</summary>
        static SerializedMember NoValue(string? returnType)
        {
            var member = new SerializedMember { name = JsonSchema.Result, typeName = returnType ?? JsonSchema.Object };
            return member.SetJsonValue(returnType == "System.Void" ? "\"Success\"" : "\"null\"");
        }

        static CodeRunner Runner()
        {
            if (Volatile.Read(ref _runner) is { } existing)
                return existing;
            // res:// resolves on the main thread; taking it outside the lock keeps a main-thread caller from waiting on us.
            var projectRoot = MainThread.Instance.Run(() => ProjectSettings.GlobalizePath("res://"));
            lock (_runnerLock)
                return _runner ??= Create(projectRoot);
        }

        static CodeRunner Create(string projectRoot)
        {
            var binRoot = Path.Combine(projectRoot, ".godot", "mono", "temp", "bin");
            // Code-Exec's default host binds snippets to the assemblies the editor already runs (the live game assembly and
            // GodotSharp) before the default context is asked, where GodotMcpAssemblyResolver would load second copies.
            return new CodeRunner(new RoslynCSharpCompiler(), new CodeRunnerOptions
            {
                DefaultUsings = ScriptExecuteSource.DefaultUsings,
                IncludeRoot = Path.GetFullPath(Path.Combine(projectRoot, McpPlugin.AgentConfig.ProjectMarker.DirectoryName, "snippets")),
                // Godot loads the game assembly from memory: point the compiler at the DLL it was built to.
                References = new LoadedAssembliesReferenceProvider(assembly => ScriptExecuteSource.BuiltAssemblyPath(binRoot, assembly.GetName().Name)),
                ValueConverter = Serialize,
                LogCaptures = new List<ILogCapture> { new ConsoleLogCapture(), new GodotPrintLogCapture() },
            });
        }

        /// <summary>Runs on the main thread before the snippet unloads: nothing of the snippet may outlive this.</summary>
        static object? Serialize(object? value)
        {
            if (value == null)
                return null;
            var type = value.GetType();
            if (ScriptExecuteSource.IsSnippetType(type))
            {
                var text = new SerializedMember { name = JsonSchema.Result, typeName = type.FullName ?? type.Name };
                return text.SetJsonValue(System.Text.Json.JsonSerializer.Serialize(value.ToString()));
            }
            var member = GodotMcpReflector.GetOrCreate().Serialize(obj: value);
            if (string.IsNullOrEmpty(member.name))
                member.name = JsonSchema.Result;
            return member;
        }

        static string ParameterName(SerializedMember parameter, int index) => parameter.name ?? $"param{index}";

        /// <summary>An unknown type name fails here, by name, instead of surfacing as an untyped <c>object</c> variable.</summary>
        static Type ParameterType(SerializedMember parameter, int index) =>
            string.IsNullOrEmpty(parameter.typeName)
                ? typeof(object)
                : TypeUtils.GetType(parameter.typeName)
                    ?? throw new Exception($"Parameter '{ParameterName(parameter, index)}': type '{parameter.typeName}' not found.");
    }
}
#endif
