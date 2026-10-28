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
using System.Runtime.Loader;
using System.Text.Json;
using com.IvanMurzak.CodeExec;
using com.IvanMurzak.CodeExec.Roslyn;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.ReflectorNet.Model;
using Xunit;
using LogEntry = com.IvanMurzak.CodeExec.LogEntry;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for <c>script-execute</c>: mode detection, the game-assembly location hook, snippet-type
    /// flattening, error text and the result model, plus real Code-Exec runs with the editor's default usings. The editor
    /// handler (main thread, GD.Print capture, the live game assembly) is covered by the headless Godot smoke
    /// (<c>scripts/smoke_script_execute.py</c>).
    /// </summary>
    public class ScriptExecuteTests
    {
        static CodeRunner NewRunner(Func<object?, object?>? converter = null) => new CodeRunner(new RoslynCSharpCompiler(), new CodeRunnerOptions
        {
            DefaultUsings = ScriptExecuteSource.DefaultUsings,
            ValueConverter = converter ?? (value => value?.ToString()),
        });

        // ---- Execution guards (Code-Exec 0.4): the editor's budget, endless loops, runaway recursion ----------------

        [Fact]
        public void RunTimeout_IsTheToolTimeoutMinusAMargin_AndNeverZero()
        {
            Assert.Equal(com.IvanMurzak.McpPlugin.Common.Consts.Hub.DefaultToolTimeoutMs - ScriptExecuteSource.ToolTimeoutMarginMs,
                ScriptExecuteSource.RunTimeoutMs);
            Assert.True(ScriptExecuteSource.RunTimeoutMs > 0);
        }

        static CodeRunner GuardedRunner(int defaultTimeoutMs) => new CodeRunner(new RoslynCSharpCompiler(), new CodeRunnerOptions
        {
            DefaultUsings = ScriptExecuteSource.DefaultUsings,
            ValueConverter = value => value?.ToString(),
            DefaultTimeoutMs = defaultTimeoutMs,
            ExecutionGuards = true,
        });

        // wave 5 (C2): `while (true) {}` froze the editor. On the calling thread (the editor runs snippets on its main thread)
        // the guard stops it once the budget is spent, also for a request that leaves TimeoutMs at 0 (= the host default).
        [Fact]
        public void EndlessLoop_StopsWithATimeout()
        {
            var result = GuardedRunner(defaultTimeoutMs: 500).Execute(new ExecuteRequest { Code = "while (true) { }" });
            Assert.False(result.Success);
            Assert.Equal(ErrorKind.Timeout, result.ErrorKind);
        }

        // wave 5 (C1): runaway recursion overflowed the stack and killed the editor; now it is a Runtime error.
        [Fact]
        public void RunawayRecursion_IsARuntimeError()
        {
            var result = GuardedRunner(defaultTimeoutMs: 30_000).Execute(new ExecuteRequest
            {
                Code = "int Deep(int n) { return Deep(n + 1) + 1; }\nDeep(0)",
            });
            Assert.False(result.Success);
            Assert.Equal(ErrorKind.Runtime, result.ErrorKind);
            Assert.Contains("Recursion too deep", result.Error);
        }

        // ---- FullSourceEntryType -----------------------------------------------------------------

        [Theory]
        [InlineData("1 + 2")]
        [InlineData("var x = 2;\nx * 3")]
        // Statements plus a helper class of the same name: still a snippet.
        [InlineData("class Script { public static int Main() => 1; }\nScript.Main()")]
        public void FullSourceEntryType_Snippet_ReturnsNull(string code)
            => Assert.Null(ScriptExecuteSource.FullSourceEntryType(code, "Script", "Main"));

        [Theory]
        [InlineData("public class Script { public static int Main() { return 42; } }", "Script", "Script")]
        [InlineData("namespace A.B { public class Tool { public static void Run() { } } }", "Tool", "A.B.Tool")]
        [InlineData("namespace A.B { public class Tool { public static void Run() { } } }", "A.B.Tool", "A.B.Tool")]
        [InlineData("public class Outer { public class Tool { public static void Run() { } } }", "Tool", "Outer+Tool")]
        public void FullSourceEntryType_DeclaredEntry_ReturnsRuntimeName(string code, string className, string expected)
        {
            var method = code.Contains("Run") ? "Run" : "Main";
            Assert.Equal(expected, ScriptExecuteSource.FullSourceEntryType(code, className, method));
        }

        [Fact]
        public void FullSourceEntryType_ClassWithoutStaticMethod_Throws()
        {
            var e = Assert.Throws<Exception>(() => ScriptExecuteSource.FullSourceEntryType("public class Script { public int Main() => 1; }", "Script", "Main"));
            Assert.Contains("no static method 'Main'", e.Message);
        }

        [Fact]
        public void FullSourceEntryType_OnlyOtherTypes_Throws()
        {
            var e = Assert.Throws<Exception>(() => ScriptExecuteSource.FullSourceEntryType("public class Helper { }", "Script", "Main"));
            Assert.Contains("declares no class 'Script'", e.Message);
        }

        // ---- BuiltAssemblyPath ---------------------------------------------------------------------

        [Fact]
        public void BuiltAssemblyPath_PicksTheNewestConfiguration()
        {
            var bin = Path.Combine(Path.GetTempPath(), "godot-mcp-bin-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(bin, "Debug"));
                Directory.CreateDirectory(Path.Combine(bin, "ExportDebug"));
                Directory.CreateDirectory(Path.Combine(bin, "Empty"));
                var debug = Path.Combine(bin, "Debug", "Game.dll");
                var export = Path.Combine(bin, "ExportDebug", "Game.dll");
                File.WriteAllText(debug, "");
                File.WriteAllText(export, "");
                File.SetLastWriteTimeUtc(export, DateTime.UtcNow.AddMinutes(-5));
                File.SetLastWriteTimeUtc(debug, DateTime.UtcNow);

                Assert.Equal(debug, ScriptExecuteSource.BuiltAssemblyPath(bin, "Game"));
                Assert.Null(ScriptExecuteSource.BuiltAssemblyPath(bin, "Other"));
                Assert.Null(ScriptExecuteSource.BuiltAssemblyPath(bin, null));
                Assert.Null(ScriptExecuteSource.BuiltAssemblyPath(Path.Combine(bin, "missing"), "Game"));
            }
            finally
            {
                Directory.Delete(bin, recursive: true);
            }
        }

        // ---- Real runs (Code-Exec + the source-only Roslyn adapter) --------------------------------

        [Fact]
        public void Run_DefaultUsingsIncludeGodot()
        {
            _ = typeof(global::Godot.Vector2); // GodotSharp must be loaded to be referenced
            var result = NewRunner().Execute(new ExecuteRequest { Code = "var v = new Vector2(3, 4);\nv.Length()" });
            Assert.True(result.Success, ScriptExecuteSource.Describe(result));
            Assert.Equal("5", result.Value);
            Assert.Equal("System.Single", result.ReturnType);
        }

        [Fact]
        public void Run_CompileError_PointsAtTheSnippetLine()
        {
            var result = NewRunner().Execute(new ExecuteRequest { Code = "var a = 1;\nvar b = a +;\nb" });
            Assert.False(result.Success);
            Assert.Equal(ErrorKind.Compile, result.ErrorKind);
            var text = ScriptExecuteSource.Describe(result);
            Assert.StartsWith("Compilation failed:", text);
            Assert.Contains("snippet.cs(2,", text);
        }

        [Fact]
        public void Run_SnippetTypes_AreDetected_AndUnload()
        {
            var seen = new List<bool>();
            var runner = NewRunner(value =>
            {
                seen.Add(value != null && ScriptExecuteSource.IsSnippetType(value.GetType()));
                return value?.ToString();
            });

            Assert.True(runner.Execute(new ExecuteRequest { Code = "class P { public override string ToString() => \"p\"; }\nnew P()" }).Success);
            Assert.True(runner.Execute(new ExecuteRequest { Code = "class P { }\nnew List<P> { new P() }" }).Success);
            Assert.True(runner.Execute(new ExecuteRequest { Code = "class P { }\nnew P[1]" }).Success);
            Assert.True(runner.Execute(new ExecuteRequest { Code = "new List<int> { 1 }" }).Success);
            Assert.Equal(new[] { true, true, true, false }, seen);

            // Each run had its own collectible context; nothing keeps them alive once the value is converted.
            for (var i = 0; i < 5 && AliveSnippetContexts() > 0; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Assert.Equal(0, AliveSnippetContexts());
        }

        /// <summary>
        /// Godot's situation: the game assembly lives in its own collectible context, loaded from memory, while a
        /// default-context resolver (GodotMcpAssemblyResolver) would answer a miss with a fresh copy from the bin folder.
        /// Code-Exec's default host (0.2.1+) must bind the snippet to the LIVE assembly (its statics), never a copy.
        /// </summary>
        [Fact]
        public void DefaultHost_BindsToTheLiveAssembly_NotADiskCopy()
        {
            var dir = Path.Combine(Path.GetTempPath(), "godot-mcp-game-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var gameName = "SmokeGame" + Guid.NewGuid().ToString("N");
            var dllPath = Path.Combine(dir, gameName + ".dll");
            var compile = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(gameName,
                new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("namespace MyGame { public static class Score { public static int Value = 1; } }") },
                new[] { Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            using (var file = File.Create(dllPath))
                Assert.True(compile.Emit(file).Success);

            var gameContext = new AssemblyLoadContext("TestGame", isCollectible: true);
            Func<AssemblyLoadContext, System.Reflection.AssemblyName, System.Reflection.Assembly?> diskCopy = (context, name) =>
                name.Name == gameName ? context.LoadFromAssemblyPath(dllPath) : null;
            AssemblyLoadContext.Default.Resolving += diskCopy;
            try
            {
                var live = gameContext.LoadFromStream(new MemoryStream(File.ReadAllBytes(dllPath))); // like Godot: no Location
                live.GetType("MyGame.Score")!.GetField("Value")!.SetValue(null, 5);

                var runner = new CodeRunner(new RoslynCSharpCompiler(), new CodeRunnerOptions
                {
                    References = new LoadedAssembliesReferenceProvider(a => a.GetName().Name == gameName ? dllPath : null),
                });
                var result = runner.Execute(new ExecuteRequest { Code = "MyGame.Score.Value" });
                Assert.True(result.Success, ScriptExecuteSource.Describe(result));
                Assert.Equal("5", result.Value);
                Assert.Same(live, Assert.Single(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == gameName));
            }
            finally
            {
                AssemblyLoadContext.Default.Resolving -= diskCopy;
                gameContext.Unload();
            }
        }

        [Fact]
        public void ParametersAdapter_NamedValuesAndReflectorNetForm()
        {
            var adapter = new com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters.ReflectorNet_SerializedMemberList_Adapter();
            var named = new global::Rql.Execution.ObjectValue { ["n"] = 21.0, ["f"] = 1.5, ["s"] = "x", ["b"] = true, ["none"] = null };
            Assert.Equal(global::Rql.Execution.AdapterOutcome.Ok, adapter.TryCoerce(named, typeof(SerializedMemberList), null!, out var result, out _));
            var list = Assert.IsType<SerializedMemberList>(result);
            Assert.Equal(new[] { "n:System.Int32:21", "f:System.Double:1.5", "s:System.String:\"x\"", "b:System.Boolean:true", "none:System.Object:" },
                list.Select(m => $"{m.name}:{m.typeName}:{m.valueJsonElement?.GetRawText()}"));

            var reflectorForm = new List<object?> { new global::Rql.Execution.ObjectValue { ["name"] = "n", ["typeName"] = "System.Int32", ["value"] = 6.0 } };
            Assert.Equal(global::Rql.Execution.AdapterOutcome.Ok, adapter.TryCoerce(reflectorForm, typeof(SerializedMemberList), null!, out result, out _));
            var member = Assert.Single(Assert.IsType<SerializedMemberList>(result));
            Assert.Equal("n:System.Int32:6", $"{member.name}:{member.typeName}:{member.valueJsonElement?.GetRawText()}");

            Assert.Equal(global::Rql.Execution.AdapterOutcome.NotHandled, adapter.TryCoerce("text", typeof(SerializedMemberList), null!, out _, out _));
        }

        static int AliveSnippetContexts() => AssemblyLoadContext.All.Count(c => c.Name?.StartsWith("CodeExec:", StringComparison.Ordinal) == true);

        [Theory]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(List<string>), false)]
        [InlineData(typeof(string[]), false)]
        public void IsSnippetType_HostTypes_False(Type type, bool expected)
            => Assert.Equal(expected, ScriptExecuteSource.IsSnippetType(type));

        // ---- Output text ----------------------------------------------------------------------------

        [Fact]
        public void LogLines_PrefixesOnlyNonInfoLevels()
        {
            var lines = ScriptExecuteSource.LogLines(new[] { new LogEntry("info", "hello"), new LogEntry("warning", "careful"), new LogEntry("error", "boom") });
            Assert.Equal(new[] { "hello", "warning: careful", "error: boom" }, lines);
        }

        // The plugin's runner keeps Code-Exec's default output limits (it used to cap logs itself).
        [Fact]
        public void Run_OutputIsBoundedByCodeExec()
        {
            var result = NewRunner().Execute(new ExecuteRequest
            {
                Code = "Console.WriteLine(new string('y', 10000));\nfor (var i = 0; i < 300; i++) Console.WriteLine(new string('x', 100));",
            });
            Assert.True(result.Success, ScriptExecuteSource.Describe(result));
            var lines = ScriptExecuteSource.LogLines(result.Logs);

            Assert.Contains("logs", result.Truncated);
            Assert.Equal(OutputLimits.DefaultLogLineChars, lines[0].IndexOf("... [truncated:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("warning: ") && l.Contains("left out"));
            Assert.True(lines.Sum(l => l.Length) <= OutputLimits.DefaultLogChars);
        }

        [Fact]
        public void Describe_ListsWhatCodeExecCut()
        {
            var result = NewRunner().Execute(new ExecuteRequest { Code = "throw new Exception(new string('e', 30000));" });
            var text = ScriptExecuteSource.Describe(result);
            Assert.Contains("\nTruncated: error", text);
            Assert.True(text.Length < OutputLimits.DefaultErrorChars + 2000, $"{text.Length} chars");
        }

        [Fact]
        public void CapValue_CutsAHugeValueToText()
        {
            var big = new SerializedMember { name = "result", typeName = "System.String" }
                .SetJsonValue(JsonSerializer.Serialize(new string('z', 2_000_000)));
            var capped = ScriptExecuteSource.CapValue(big);

            var text = capped.valueJsonElement!.Value.GetString()!;
            Assert.InRange(text.Length, ScriptExecuteSource.MaxValueChars, ScriptExecuteSource.MaxValueChars + 200);
            Assert.StartsWith(new string('z', 100), text);
            Assert.Contains("[truncated: 20000 of 2000000 characters shown", text);
            Assert.Equal("System.String", capped.typeName);
        }

        [Fact]
        public void CapValue_CountsNestedMembers()
        {
            var list = new SerializedMember { name = "result", typeName = "System.Int32[]", fields = new SerializedMemberList() };
            for (int i = 0; i < 5_000; i++)
                list.fields.Add(new SerializedMember { name = $"[{i}]", typeName = "System.Int32" }.SetJsonValue(i.ToString()));
            var capped = ScriptExecuteSource.CapValue(list);
            Assert.Null(capped.fields);
            Assert.Contains("[truncated:", capped.valueJsonElement!.Value.GetString());
        }

        [Fact]
        public void CapValue_SmallValueIsTheSameObject()
        {
            var small = new SerializedMember { name = "result", typeName = "System.Int32" }.SetJsonValue("3");
            Assert.Same(small, ScriptExecuteSource.CapValue(small));
        }

        [Theory]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(int[]), false)]
        [InlineData(typeof(List<int>), false)]
        [InlineData(typeof(Dictionary<string, int>), false)]
        [InlineData(typeof(HashSet<int>), false)]
        public void IsLazySequence_Collections_False(Type type, bool expected)
            => Assert.Equal(expected, ScriptExecuteSource.IsLazySequence(type));

        [Fact]
        public void IsLazySequence_QueriesAndIterators_True()
        {
            Assert.True(ScriptExecuteSource.IsLazySequence(Enumerable.Range(0, 5).Select(i => i * i).GetType()));
            Assert.True(ScriptExecuteSource.IsLazySequence(Endless().GetType()));
        }

        [Fact]
        public void Materialize_StopsAnEndlessSequence()
        {
            var items = ScriptExecuteSource.Materialize(Endless());
            Assert.Equal(ScriptExecuteSource.MaxSequenceItems + 1, items.Count);
            Assert.Equal(ScriptExecuteSource.MaxSequenceItems - 1, items[ScriptExecuteSource.MaxSequenceItems - 1]);
            Assert.Equal($"… (the first {ScriptExecuteSource.MaxSequenceItems} items; the sequence has more)", items[^1]);
            Assert.Equal(new object?[] { 0, 1, 4 }, ScriptExecuteSource.Materialize(Enumerable.Range(0, 3).Select(i => i * i)));
            // Exactly the cap: no note.
            Assert.Equal(ScriptExecuteSource.MaxSequenceItems,
                ScriptExecuteSource.Materialize(Enumerable.Range(0, ScriptExecuteSource.MaxSequenceItems).Select(i => i)).Count);
        }

        [Fact]
        public void Materialize_AppliesTheItemRule()
            => Assert.Equal(new object?[] { "2", "4" }, ScriptExecuteSource.Materialize(Enumerable.Range(1, 2).Select(i => i * 2), v => v?.ToString()));

        static IEnumerable<int> Endless()
        {
            for (int i = 0; ; i++) yield return i;
        }

        [Theory]
        [InlineData(typeof(global::Godot.Basis), true)]
        [InlineData(typeof(global::Godot.Transform3D), true)]
        [InlineData(typeof(global::Godot.Quaternion), true)]
        [InlineData(typeof(global::Godot.StringName), true)]
        [InlineData(typeof(global::Godot.Vector3), false)] // has a reflection converter
        [InlineData(typeof(global::Godot.Color), false)]
        [InlineData(typeof(string), false)]
        public void IsGodotTextValue(Type type, bool expected) => Assert.Equal(expected, ScriptExecuteSource.IsGodotTextValue(type));

        [Fact]
        public void SkillText_StatesTheCaps()
        {
            Assert.Contains($"{OutputLimits.DefaultLogChars} characters each, {OutputLimits.DefaultLogLineChars} per output line", ScriptExecuteSource.SkillOutputAndUsings);
            Assert.Equal(OutputLimits.DefaultLogChars, ScriptExecuteSource.MaxValueChars); // "each": the result and the logs
            Assert.Contains($"at most {ScriptExecuteSource.MaxSequenceItems} items", ScriptExecuteSource.SkillOutputAndUsings);
        }

        [Fact]
        public void Describe_RuntimeError_AppendsOutput()
        {
            var result = NewRunner().Execute(new ExecuteRequest { Code = "Console.WriteLine(\"before\");\nthrow new InvalidOperationException(\"nope\");" });
            Assert.Equal(ErrorKind.Runtime, result.ErrorKind);
            var text = ScriptExecuteSource.Describe(result);

            Assert.Contains("Execution failed (Runtime): System.InvalidOperationException: nope", text);
            Assert.EndsWith("Output:\nbefore", text);
        }

        [Fact]
        public void Result_OmitsLogsWhenThereAreNone()
        {
            var member = new SerializedMember { name = "result", typeName = "System.Int32" }.SetJsonValue("3");
            var json = JsonSerializer.Serialize(new ScriptExecuteResult { Result = member });
            Assert.DoesNotContain("logs", json);
            Assert.Contains("\"result\"", json);

            json = JsonSerializer.Serialize(new ScriptExecuteResult { Result = member, Logs = new List<string> { "hi" } });
            Assert.Contains("\"logs\":[\"hi\"]", json);
        }
    }

    /// <summary>The idle-shutdown bookkeeping of CLI-started editors (pure; the editor tick is in IdleShutdown.cs).</summary>
    public class IdleShutdownTimerTests
    {
        static readonly DateTime T0 = new DateTime(2026, 1, 1);

        [Fact]
        public void Quits_AfterTheTimeoutWithoutActivity()
        {
            var timer = new IdleShutdownTimer(TimeSpan.FromSeconds(10), T0);
            for (var s = 0.5; s < 10; s += 0.5)
                Assert.False(timer.ShouldQuit(T0.AddSeconds(s), 0, busy: false));
            Assert.True(timer.ShouldQuit(T0.AddSeconds(10), 0, busy: false));
        }

        [Fact]
        public void ToolCalls_Busy_AndStalls_RestartTheTimer()
        {
            var timer = new IdleShutdownTimer(TimeSpan.FromSeconds(2), T0);
            var now = T0;
            bool Step(ulong calls, bool busy = false, double seconds = 0.5) => timer.ShouldQuit(now = now.AddSeconds(seconds), calls, busy);

            Assert.False(Step(0));
            Assert.False(Step(1));      // a tool call
            Assert.False(Step(1));
            Assert.False(Step(1));
            Assert.False(Step(1));
            Assert.False(Step(1, busy: true)); // playing / scanning
            Assert.False(Step(1));
            Assert.False(Step(1, seconds: 5)); // a stalled frame: the main thread ran a long call
            Assert.False(Step(1));
            Assert.False(Step(0));      // counter reset (reconnect) is activity too
            Assert.False(Step(0));
            Assert.False(Step(0));
            Assert.False(Step(0));
            Assert.True(Step(0));
        }

        [Theory]
        [InlineData("300", 300)]
        [InlineData(" 45 ", 45)]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("0", null)]
        [InlineData("-5", null)]
        [InlineData("abc", null)]
        public void ReadSeconds(string? idle, int? expected)
        {
            // The owner mark alone never arms it: a CLI session started with idle 0 passes no idle value.
            var env = new Dictionary<string, string?> { [GodotMcpEnv.SessionIdleSeconds] = idle, ["AIGD_SESSION_OWNER"] = "cli" };
            Assert.Equal(expected, IdleShutdownTimer.ReadSeconds(name => env.TryGetValue(name, out var v) ? v : null));
        }
    }

    /// <summary>The editor removes the CLI's session lease on exit, but only its own (same nonce).</summary>
    public class SessionLeaseTests
    {
        [Fact]
        public void DeleteIfOwned_OnlyTheLeaseWithTheSameNonce()
        {
            var root = Path.Combine(Path.GetTempPath(), "godot-mcp-lease-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, ".ai-game-dev"));
            try
            {
                var lease = SessionLease.PathFor(root);
                Assert.False(SessionLease.DeleteIfOwned(root, "abc")); // no lease

                File.WriteAllText(lease, "{\"pid\": 1, \"nonce\": \"other\", \"ownedBy\": \"cli\"}");
                Assert.False(SessionLease.DeleteIfOwned(root, "abc"));
                Assert.False(SessionLease.DeleteIfOwned(root, null));
                Assert.True(File.Exists(lease));

                File.WriteAllText(lease, "not json");
                Assert.False(SessionLease.DeleteIfOwned(root, "abc"));

                File.WriteAllText(lease, "{\"pid\": 1, \"nonce\": \"abc\", \"ownedBy\": \"cli\"}");
                Assert.True(SessionLease.DeleteIfOwned(root, "abc"));
                Assert.False(File.Exists(lease));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
