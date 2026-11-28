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
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Runs <c>csharp-exec</c> on Code-Exec (NuGet <c>com.IvanMurzak.CodeExec</c> + the source-only Roslyn adapter),
    /// for the editor tool (<c>Tool_Script.Execute</c>) and the in-game one (<see cref="Tool_GameScript"/>).
    /// Compiles on the calling thread (MCP calls arrive off the main thread), runs on the Godot main thread. Parameters
    /// arrive as RQL values already converted to their types (<see cref="ScriptParameters"/>); the result leaves as a plain
    /// object the tool binding presents with RQL. Each run is loaded into its own collectible context and unloaded
    /// afterwards, so values of the snippet's own types are made plain before that (<see cref="ScriptResultText.Plain"/>).
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
            ScriptParameters? parameters, bool isMethodBody, string[]? usings, string[]? include,
            int timeoutSeconds = ScriptExecuteSource.DefaultTimeoutSeconds)
        {
            if (string.IsNullOrEmpty(csharpCode))
                throw new Exception("'csharpCode' is null or empty. Please provide valid C# code to execute.");

            Interlocked.Increment(ref _inFlight);
            try
            {
                return await Run(csharpCode, className, methodName, parameters, isMethodBody, usings, include,
                    ScriptExecuteSource.RequestedTimeoutMs(timeoutSeconds));
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        static async Task<ScriptExecuteResult> Run(string csharpCode, string className, string methodName,
            ScriptParameters? parameters, bool isMethodBody, string[]? usings, string[]? include, int timeoutMs)
        {
            // The call's budget counts from its arrival (compiling and waiting for an earlier snippet use it up too); the snippet
            // gets its requested timeout within what is left of it.
            var arrived = System.Diagnostics.Stopwatch.StartNew();
            var runner = Runner();
            // Only the parameter TYPES are needed to compile; the values are bound on the main thread.
            var request = new ExecuteRequest
            {
                Code = csharpCode,
                Usings = usings ?? Array.Empty<string>(),
                Include = include ?? Array.Empty<string>(),
                Parameters = parameters?.Items.Select(p => new SnippetParameter(p.Name, p.Type, null)).ToList()
                    ?? (IReadOnlyList<SnippetParameter>)Array.Empty<SnippetParameter>(),
                TimeoutMs = timeoutMs,
            };
            if (!isMethodBody && ScriptExecuteSource.FullSourceEntryType(csharpCode, className, methodName) is { } entryType)
            {
                request.EntryType = entryType;
                request.EntryMethod = methodName;
                // The method declares its parameter types: the values are converted to them now (a node path or ref resolves
                // through the scene tree, on the main thread), so a wrong value fails here instead of when the method is invoked.
                if (parameters != null && parameters.Items.Count > 0
                    && ScriptExecuteSource.FullSourceParameterTypes(csharpCode, entryType, methodName, parameters.Items.Count) is { } declared)
                {
                    parameters = MainThread.Instance.Run(() => ToDeclaredTypes(parameters, declared));
                    request.Parameters = parameters.Items.Select(p => new SnippetParameter(p.Name, p.Type, null)).ToList();
                }
            }
            var prepared = runner.Prepare(request);
            if (!prepared.Succeeded)
                throw new Exception(ScriptExecuteSource.Describe(runner.Run(prepared))); // a failed Prepare runs nothing

            // Awaits inside the snippet resume on the main thread (Godot's synchronization context).
            return await MainThread.Instance.RunAsync(() => RunOnMainThread(runner, request, prepared, parameters, arrived, timeoutMs)).Unwrap();
        }

        /// <summary>
        /// Each parameter converted to the type the full-mode method declares for it (in order), when that type is known and
        /// differs from the value's; a value that does not convert is an error before anything runs.
        /// </summary>
        internal static ScriptParameters ToDeclaredTypes(ScriptParameters parameters, IReadOnlyList<string> declared)
        {
            var items = new List<ScriptParameter>(parameters.Items.Count);
            for (var i = 0; i < parameters.Items.Count; i++)
            {
                var p = parameters.Items[i];
                var type = Editor.Rql.Adapters.Godot_ScriptParameters_Adapter.ResolveType(declared[i]);
                if (type == null || type == p.Type || p.Convert == null || (p.Value != null && type.IsInstanceOfType(p.Value) && !type.IsValueType))
                {
                    items.Add(p);
                    continue;
                }
                if (!p.Convert(type, out var value, out var reason))
                    throw new ArgumentException($"parameter '{p.Name}' ({declared[i]} in {string.Join(", ", declared)}): {reason}" +
                        (typeof(GodotObject).IsAssignableFrom(type) && p.Value is string
                            ? ". A node: godot.find(\"Crate\") (by name or path) or ref(\"Node3D#12\") from a query result"
                            : ""));
                items.Add(new ScriptParameter(p.Name, type, value, p.Convert));
            }
            return new ScriptParameters(items);
        }

        static async Task<ScriptExecuteResult> RunOnMainThread(CodeRunner runner, ExecuteRequest request, PreparedSnippet prepared,
            ScriptParameters? parameters, System.Diagnostics.Stopwatch arrived, int timeoutMs)
        {
            // The values were converted by the RQL tool binding (live nodes included) on this thread.
            if (parameters != null)
                prepared = prepared.WithValues(parameters.Items.Select(p => p.Value).ToList());

            ExecuteResult result;
            await _runGate.WaitAsync();
            try
            {
                // Code-Exec reads the budget when the run starts (the prepared snippet holds this request).
                request.TimeoutMs = ScriptExecuteSource.RunBudgetMs(timeoutMs, arrived.ElapsedMilliseconds);
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
                Result = converted?.Value,
                // Nothing returned (System.Void) reads "Success"; a typed null names its type.
                TypeName = converted?.TypeName ?? (result.ReturnType == "System.Void" ? null : result.ReturnType ?? "System.Object"),
                Logs = result.Logs.Count > 0 ? ScriptExecuteSource.LogLines(result.Logs) : null,
                MaxValueChars = ScriptExecuteSource.MaxValueChars,
            };
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
                ValueConverter = ToPlain,
                LogCaptures = new List<ILogCapture> { new ConsoleLogCapture(), new GodotPrintLogCapture() },
                // Code-Exec's guards (recursion, loops) stop a runaway snippet with a Runtime / Timeout result instead of a stack
                // overflow that kills the editor or a loop that freezes it; the budget ends before the server's tool timeout. Threads
                // and tasks a snippet starts are stopped at their next guard check once its run ends.
                DefaultTimeoutMs = ScriptExecuteSource.DefaultTimeoutSeconds * 1000,
                MaxTimeoutMs = ScriptExecuteSource.RunTimeoutMs,
                ExecutionGuards = true,
            });
        }

        /// <summary>The plain result and the type shown before it, made before the snippet unloads.</summary>
        sealed record ConvertedValue(object? Value, string TypeName);

        /// <summary>Runs on the main thread before the snippet unloads: nothing of the snippet may outlive this.</summary>
        static object? ToPlain(object? value)
        {
            if (value == null)
                return null;
            var type = value.GetType();
            // The snippet's own types become maps and lists; a freed Godot object its text. The presented text is cut by
            // the tool binding (ScriptExecuteResult.MaxValueChars).
            var plain = ScriptResultText.Plainer(ScriptExecuteSource.IsSnippetType,
                v => v is GodotObject o && !GodotObject.IsInstanceValid(o) ? $"<freed {o.GetType().Name}>" : null);
            // A lazy sequence (LINQ query, iterator) would show the enumerator's state, not its items: enumerated once, its
            // type names the element type.
            if (ScriptExecuteSource.IsLazySequence(type))
                return new ConvertedValue(ScriptExecuteSource.Materialize((System.Collections.IEnumerable)value, plain),
                    "IEnumerable<" + ScriptResultText.TypeName(ElementType(type)) + ">");
            return new ConvertedValue(plain(value), ScriptResultText.ShownTypeName(type));
        }

        // The element type of a sequence (IEnumerable<T>), object when it has none.
        static Type ElementType(Type sequence) =>
            (sequence.IsGenericType && sequence.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? sequence : sequence.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))?.GetGenericArguments()[0] ?? typeof(object);
    }
}
#endif
