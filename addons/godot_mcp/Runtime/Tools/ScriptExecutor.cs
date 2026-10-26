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
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Runs <c>script-execute</c> on Code-Exec (NuGet <c>com.IvanMurzak.CodeExec</c> + the source-only Roslyn adapter),
    /// for the editor tool (<c>Tool_Script.Execute</c>) and the in-game one (<see cref="Tool_GameScript"/>).
    /// Compiles on the calling thread (MCP calls arrive off the main thread), runs on the Godot main thread. Each run is
    /// loaded into its own collectible context and unloaded afterwards, so the result is serialized before that.
    /// The runner lives in a static of THIS (reloadable) assembly and subscribes to nothing, so a C# rebuild drops it.
    /// </summary>
    static class ScriptExecutor
    {
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
                TimeoutMs = ScriptExecuteSource.RunTimeoutMs,
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
            var projectRoot = MainThread.Instance.Run(() => ScriptExecuteSource.ProjectRoot(
                OS.HasFeature("editor"), ProjectSettings.GlobalizePath("res://"), OS.GetExecutablePath()));
            lock (_runnerLock)
                return _runner ??= Create(projectRoot);
        }

        static CodeRunner Create(string projectRoot)
        {
            // Only the editor build has it (an exported game loads its assemblies from files, so the fallback is unused there).
            var binRoot = Path.Combine(projectRoot, ".godot", "mono", "temp", "bin");
            // The default host binds snippets to the LIVE game assembly and GodotSharp (Code-Exec 0.2.1+), never to the
            // second copies GodotMcpAssemblyResolver would load into the default context; `Console` binds to System.Console
            // (not CoreLib's Internal.Console), so its output is captured.
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
            if (value is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
                return Text(type, $"<freed {type.Name}>");
            // Godot structs without a reflection converter (Basis, Transform3D …) and StringName serialize as "{}": their text.
            if (ScriptExecuteSource.IsSnippetType(type) || ScriptExecuteSource.IsGodotTextValue(type))
                return Text(type, value.ToString());
            // A lazy sequence (LINQ query, iterator) would show the enumerator's state, not its items.
            if (ScriptExecuteSource.IsLazySequence(type))
                value = ScriptExecuteSource.Materialize((System.Collections.IEnumerable)value, AsItem);
            var member = GodotMcpReflector.GetOrCreate().Serialize(obj: value);
            if (string.IsNullOrEmpty(member.name))
                member.name = JsonSchema.Result;
            return ScriptExecuteSource.CapValue(member);
        }

        // An item of an enumerated sequence: the same text rules as a top-level value (a freed object, a Godot struct).
        static object? AsItem(object? item) => item switch
        {
            GodotObject o when !GodotObject.IsInstanceValid(o) => $"<freed {o.GetType().Name}>",
            not null when ScriptExecuteSource.IsGodotTextValue(item.GetType()) => item.ToString(),
            _ => item,
        };

        static SerializedMember Text(Type type, string? text) =>
            ScriptExecuteSource.CapValue(ScriptExecuteSource.TextMember(JsonSchema.Result, type.FullName ?? type.Name, text));

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
