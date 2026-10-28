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
            // The time budget counts from the call's arrival: compiling and waiting for an earlier snippet use it up too.
            var arrived = System.Diagnostics.Stopwatch.StartNew();
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
            return await MainThread.Instance.RunAsync(() => RunOnMainThread(runner, request, prepared, parameters, arrived)).Unwrap();
        }

        static async Task<ScriptExecuteResult> RunOnMainThread(CodeRunner runner, ExecuteRequest request, PreparedSnippet prepared,
            SerializedMemberList? parameters, System.Diagnostics.Stopwatch arrived)
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
                // Code-Exec reads the budget when the run starts (the prepared snippet holds this request).
                request.TimeoutMs = ScriptExecuteSource.RemainingRunTimeoutMs(arrived.ElapsedMilliseconds);
                result = await runner.RunAsync(prepared);
            }
            finally
            {
                _runGate.Release();
            }
            if (!result.Success)
                throw new Exception(ScriptExecuteSource.Describe(result));

            var converted = result.Value as ConvertedValue;
            return new ScriptExecuteResult
            {
                Result = converted?.Member ?? NoValue(result.ReturnType),
                Text = converted?.Text,
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
                // Code-Exec's guards (recursion, loops) stop a runaway snippet with a Runtime / Timeout result instead of a stack
                // overflow that kills the editor or a loop that freezes it; the budget ends before the server's tool timeout.
                DefaultTimeoutMs = ScriptExecuteSource.RunTimeoutMs,
                ExecutionGuards = true,
            });
        }

        /// <summary>The serialized result and its compact text (null for a scalar), made before the snippet unloads.</summary>
        sealed record ConvertedValue(SerializedMember Member, string? Text);

        /// <summary>Runs on the main thread before the snippet unloads: nothing of the snippet may outlive this.</summary>
        static object? Serialize(object? value)
        {
            if (value == null)
                return null;
            var type = value.GetType();
            if (value is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
                return new ConvertedValue(Text(type, Freed(godotObject)), null);
            // A lazy sequence (LINQ query, iterator) would show the enumerator's state, not its items: enumerated once, its
            // text names the element type (the materialized list is a List<object>), the serialized form gets the item rules.
            string? text;
            if (ScriptExecuteSource.IsLazySequence(type))
            {
                var items = ScriptExecuteSource.Materialize((System.Collections.IEnumerable)value);
                text = CompactText(items, "IEnumerable<" + ScriptResultText.TypeName(ElementType(type)) + ">");
                value = items.Select(AsItem).ToList();
            }
            else
            {
                text = CompactText(value);
            }
            // Godot structs without a reflection converter (Basis, Transform3D …) and StringName serialize as "{}": their text.
            if (ScriptExecuteSource.IsSnippetType(type) || ScriptExecuteSource.IsGodotTextValue(type))
                return new ConvertedValue(Text(type, value.ToString()), text);
            var member = GodotMcpReflector.GetOrCreate().Serialize(obj: value);
            if (string.IsNullOrEmpty(member.name))
                member.name = JsonSchema.Result;
            return new ConvertedValue(ScriptExecuteSource.CapValue(member), text);
        }

        // The RQL format's text of the value, capped like the serialized value; null for a scalar.
        static string? CompactText(object? value, string? typeName = null)
        {
            string? text;
            try { text = ScriptResultText.Format(value, EngineValueText, Unwrap, typeName); }
            catch (Exception) { return null; } // a member that cannot be read this way: the serialized form stays
            return text == null ? null : ScriptExecuteSource.CapText(text);
        }

        // A Variant (an item of a Godot collection) is read as the value it holds.
        static object? Unwrap(object value) => value is Variant v ? v.Obj : value;

        // The element type of a sequence (IEnumerable<T>), object when it has none.
        static Type ElementType(Type sequence) =>
            (sequence.IsGenericType && sequence.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? sequence : sequence.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))?.GetGenericArguments()[0] ?? typeof(object);

        // Nodes and resources the way rql-query prints them (Node3D#123(Geometry/Floor), PlaneMesh#45(res://m.tres)),
        // Godot structs by their text.
        static string? EngineValueText(object value)
        {
            switch (value)
            {
                case GodotObject o when !GodotObject.IsInstanceValid(o):
                    return Freed(o);
                case Node n:
                    return $"{n.GetClass()}#{n.GetInstanceId()}({(n.Owner != null && n.Owner.IsAncestorOf(n) ? n.Owner.GetPathTo(n).ToString() : n.Name.ToString())})";
                case Resource r:
                    return $"{r.GetClass()}#{r.GetInstanceId()}({r.ResourcePath})";
                case GodotObject o:
                    return $"{o.GetClass()}#{o.GetInstanceId()}";
                case StringName or NodePath:
                    return value.ToString();
            }
            // Every Godot struct (Vector3, Color, Basis …) by its text, not only the IsGodotTextValue ones: here the text is
            // the whole output, while the serialized form keeps the struct's members where it has a converter.
            var type = value.GetType();
            return type.IsValueType && type.Namespace == "Godot" ? value.ToString() : null;
        }

        static string Freed(GodotObject o) => $"<freed {o.GetType().Name}>";

        // An item of an enumerated sequence: the same text rules as a top-level value (a freed object, a Godot struct).
        static object? AsItem(object? item) => item switch
        {
            GodotObject o when !GodotObject.IsInstanceValid(o) => Freed(o),
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
