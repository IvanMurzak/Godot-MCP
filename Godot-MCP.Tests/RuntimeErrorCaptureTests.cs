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
using System.Linq;
using System.Text.Json;
using System.Threading;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Runtime;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the in-game runtime error-capture feature (issue #160): the
    /// <see cref="RuntimeError"/> / <see cref="RuntimeErrorsResult"/> models, the bounded ring-buffer
    /// <see cref="RuntimeErrorCollector"/> (monotonic sequence + since-marker poll + eviction), the
    /// <see cref="RuntimeErrorFactory"/> (engine-record + C#-exception → row mapping), the
    /// <c>runtime-errors-*</c> tool handlers, the structured <see cref="ScriptErrorCapture.ErrorSink"/>
    /// forwarding, and the builder opt-in. None touch a live Godot runtime, so they run in the plain xUnit
    /// host with no Godot binary. The capture INSTALLER (<c>RuntimeErrorCapture.Install</c>, which registers
    /// the engine logger via <c>OS.AddLogger</c> + the AppDomain/TaskScheduler hooks) and the 4.5 engine
    /// Logger subclass are verified by the headless Godot runtime smoke (test.md Suite 3).
    /// </summary>
    public class RuntimeErrorCaptureTests
    {
        // ---- RuntimeError / RuntimeErrorsResult serialization ------------------------------------

        [Fact]
        public void RuntimeError_Serializes_WithExpectedJsonNames()
        {
            var e = new RuntimeError(
                source: RuntimeErrorSource.Engine,
                message: "Invalid get index 'x'",
                type: "Script",
                file: "res://scripts/player.gd",
                line: 42,
                function: "_process")
            { Sequence = 7 };

            var json = JsonSerializer.Serialize(e);
            foreach (var key in new[] { "sequence", "message", "type", "source", "file", "line",
                "function", "stackTrace", "timestamp" })
                Assert.Contains($"\"{key}\"", json);

            var restored = JsonSerializer.Deserialize<RuntimeError>(json);
            Assert.NotNull(restored);
            Assert.Equal(7, restored!.Sequence);
            Assert.Equal("Invalid get index 'x'", restored.Message);
            Assert.Equal("Script", restored.Type);
            Assert.Equal(RuntimeErrorSource.Engine, restored.Source);
            Assert.Equal("res://scripts/player.gd", restored.File);
            Assert.Equal(42, restored.Line);
            Assert.Equal("_process", restored.Function);
        }

        [Fact]
        public void RuntimeError_Serializes_DeepFrames_RoundTrip()
        {
            // Issue #163: an engine GDScript error on Godot 4.5+ carries the deep multi-frame backtrace.
            var e = new RuntimeError(
                source: RuntimeErrorSource.Engine,
                message: "Invalid get index 'health'",
                type: "Script",
                file: "res://scripts/player.gd",
                line: 42,
                function: "_take_damage",
                stackTrace: "GDScript backtrace:\nat _take_damage res://scripts/player.gd:42\n" +
                            "at _process res://scripts/player.gd:18",
                frames: new List<RuntimeErrorFrame>
                {
                    new RuntimeErrorFrame("_take_damage", "res://scripts/player.gd", 42),
                    new RuntimeErrorFrame("_process", "res://scripts/player.gd", 18),
                })
            { Sequence = 3 };

            var json = JsonSerializer.Serialize(e);
            Assert.Contains("\"frames\"", json);
            Assert.Contains("\"_take_damage\"", json);
            Assert.Contains("\"_process\"", json);

            var restored = JsonSerializer.Deserialize<RuntimeError>(json);
            Assert.NotNull(restored);
            Assert.NotNull(restored!.Frames);
            Assert.Equal(2, restored.Frames!.Count);
            // Innermost-first ordering is preserved across serialization.
            Assert.Equal("_take_damage", restored.Frames[0].Function);
            Assert.Equal("res://scripts/player.gd", restored.Frames[0].File);
            Assert.Equal(42, restored.Frames[0].Line);
            Assert.Equal("_process", restored.Frames[1].Function);
            Assert.Equal(18, restored.Frames[1].Line);
            Assert.NotNull(restored.StackTrace);
            Assert.Contains("backtrace", restored.StackTrace!);
        }

        [Fact]
        public void RuntimeError_EngineOriginOnly_HasNullFrames_AndNullStackTrace()
        {
            // The < 4.5 / no-tracked-backtrace fallback: origin fields only, no deep stack.
            var e = new RuntimeError(
                source: RuntimeErrorSource.Engine,
                message: "boom",
                type: "Error",
                file: "res://x.gd",
                line: 5);

            Assert.Null(e.Frames);
            Assert.Null(e.StackTrace);

            // An empty frame list normalizes to null (single "no frames" sentinel for consumers).
            var e2 = new RuntimeError(RuntimeErrorSource.Engine, "boom", "Error",
                frames: new List<RuntimeErrorFrame>());
            Assert.Null(e2.Frames);
        }

        [Fact]
        public void RuntimeErrorFrame_Serializes_WithExpectedJsonNames()
        {
            var f = new RuntimeErrorFrame("_ready", "res://scripts/main.gd", 7);

            var json = JsonSerializer.Serialize(f);
            foreach (var key in new[] { "function", "file", "line" })
                Assert.Contains($"\"{key}\"", json);

            var restored = JsonSerializer.Deserialize<RuntimeErrorFrame>(json);
            Assert.NotNull(restored);
            Assert.Equal("_ready", restored!.Function);
            Assert.Equal("res://scripts/main.gd", restored.File);
            Assert.Equal(7, restored.Line);
        }

        [Fact]
        public void RuntimeErrorFrame_ToString_RendersFunctionFileLine()
        {
            Assert.Equal("at _ready res://m.gd:7", new RuntimeErrorFrame("_ready", "res://m.gd", 7).ToString());
            // Unknown line drops the :line suffix; unknown function falls back to <unknown>.
            Assert.Equal("at _ready res://m.gd", new RuntimeErrorFrame("_ready", "res://m.gd", -1).ToString());
            Assert.Equal("at <unknown>", new RuntimeErrorFrame(null, null, -1).ToString());
        }

        [Fact]
        public void RuntimeErrorsResult_Serializes_WithExpectedJsonNames()
        {
            var r = new RuntimeErrorsResult
            {
                Available = true,
                Ok = false,
                Count = 1,
                ErrorCount = 1,
                HighestSequence = 9,
                Errors = { new RuntimeError(RuntimeErrorSource.UnhandledException, "boom", "System.Exception") },
                Note = "1 runtime error.",
            };

            var json = JsonSerializer.Serialize(r);
            foreach (var key in new[] { "available", "ok", "count", "errorCount", "warningCount",
                "highestSequence", "truncated", "errors", "note" })
                Assert.Contains($"\"{key}\"", json);

            var restored = JsonSerializer.Deserialize<RuntimeErrorsResult>(json);
            Assert.NotNull(restored);
            Assert.True(restored!.Available);
            Assert.False(restored.Ok);
            Assert.Equal(9, restored.HighestSequence);
            Assert.Single(restored.Errors);
        }

        // ---- RuntimeErrorCollector: sequence + buffer --------------------------------------------

        [Fact]
        public void Collector_Append_AssignsMonotonicSequence_AndAdvancesHighest()
        {
            var c = new RuntimeErrorCollector();
            Assert.Equal(0, c.HighestSequence);

            var s1 = c.Append(new RuntimeError(RuntimeErrorSource.Engine, "a", "Error"));
            var s2 = c.Append(new RuntimeError(RuntimeErrorSource.Engine, "b", "Error"));

            Assert.Equal(1, s1);
            Assert.Equal(2, s2);
            Assert.Equal(2, c.HighestSequence);
            Assert.Equal(2, c.Count);
        }

        [Fact]
        public void Collector_Append_NullIsIgnored_ReturnsZero()
        {
            var c = new RuntimeErrorCollector();
            Assert.Equal(0, c.Append(null!));
            Assert.Equal(0, c.Count);
        }

        [Fact]
        public void Collector_QuerySince_ReturnsOnlyNewerErrors_OldestFirst()
        {
            var c = new RuntimeErrorCollector();
            for (int i = 0; i < 5; i++)
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, $"e{i}", "Error"));

            var page = c.QuerySince(sinceSequence: 2, maxEntries: 100, out var truncated);

            Assert.False(truncated);
            Assert.Equal(3, page.Length);                       // sequences 3,4,5
            Assert.Equal(new long[] { 3, 4, 5 }, page.Select(e => e.Sequence)); // oldest-first
        }

        [Fact]
        public void Collector_QuerySince_Zero_ReturnsAllRetained()
        {
            var c = new RuntimeErrorCollector();
            c.Append(new RuntimeError(RuntimeErrorSource.Engine, "a", "Error"));
            c.Append(new RuntimeError(RuntimeErrorSource.Engine, "b", "Error"));

            var page = c.QuerySince(0, 100, out _);
            Assert.Equal(2, page.Length);
        }

        [Fact]
        public void Collector_QuerySince_CapsToMaxEntries_KeepingNewest_AndFlagsTruncation()
        {
            var c = new RuntimeErrorCollector();
            for (int i = 0; i < 10; i++)
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, $"e{i}", "Error"));

            var page = c.QuerySince(0, maxEntries: 3, out var truncated);

            Assert.True(truncated);
            Assert.Equal(3, page.Length);
            // The NEWEST 3 (sequences 8,9,10) are kept — never the oldest.
            Assert.Equal(new long[] { 8, 9, 10 }, page.Select(e => e.Sequence));
        }

        [Fact]
        public void Collector_EvictsOldest_AtCapacity_ButHighestSequenceKeepsAdvancing()
        {
            var c = new RuntimeErrorCollector();
            int total = RuntimeErrorCollector.Capacity + 50;
            for (int i = 0; i < total; i++)
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, $"e{i}", "Error"));

            Assert.Equal(RuntimeErrorCollector.Capacity, c.Count);   // bounded
            Assert.Equal(total, c.HighestSequence);                  // sequence still advanced past eviction

            // The earliest sequences were evicted; a since-poll for the very first ones returns only what
            // survived (capacity worth), capped — proving eviction does not corrupt the since semantics.
            var page = c.QuerySince(0, int.MaxValue, out _);
            Assert.Equal(RuntimeErrorCollector.Capacity, page.Length);
            Assert.Equal(51, page.First().Sequence);  // first surviving = total-Capacity+1 = 50+1
            Assert.Equal(total, page.Last().Sequence);
        }

        [Fact]
        public void Collector_Clear_DropsEntries_ButNotTheSequenceCounter()
        {
            var c = new RuntimeErrorCollector();
            c.Append(new RuntimeError(RuntimeErrorSource.Engine, "a", "Error")); // seq 1
            c.Append(new RuntimeError(RuntimeErrorSource.Engine, "b", "Error")); // seq 2
            c.Clear();

            Assert.Equal(0, c.Count);
            Assert.Equal(2, c.HighestSequence); // retained across clear

            var s3 = c.Append(new RuntimeError(RuntimeErrorSource.Engine, "c", "Error"));
            Assert.Equal(3, s3); // counter did NOT reset — no sequence reuse after a clear
        }

        [Fact]
        public void Collector_Append_IsThreadSafe_AllSequencesUniqueAndContiguous()
        {
            var c = new RuntimeErrorCollector();
            const int perThread = 200;
            const int threadCount = 8;
            var threads = new List<Thread>();
            for (int t = 0; t < threadCount; t++)
            {
                var thread = new Thread(() =>
                {
                    for (int i = 0; i < perThread; i++)
                        c.Append(new RuntimeError(RuntimeErrorSource.Engine, "x", "Error"));
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (var thread in threads) thread.Join();

            Assert.Equal(threadCount * perThread, c.HighestSequence); // no lost/duplicated sequences
        }

        // ---- RuntimeErrorFactory: engine + exception mapping -------------------------------------

        [Fact]
        public void Factory_FromEngine_MapsOriginFields_NoStackTrace()
        {
            // The origin-only (< 4.5 / no tracked backtrace) record: the 5-arg ctor leaves frames null.
            var record = new EngineErrorRecord(EngineErrorKind.Script, "res://x.gd", 13, "_ready", "bad index");
            var e = RuntimeErrorFactory.FromEngine(record);

            Assert.Equal(RuntimeErrorSource.Engine, e.Source);
            Assert.Equal("Script", e.Type);            // EngineErrorKind name
            Assert.Equal("res://x.gd", e.File);
            Assert.Equal(13, e.Line);
            Assert.Equal("_ready", e.Function);
            Assert.Equal("bad index", e.Message);
            Assert.Null(e.StackTrace);                 // no backtrace tracked → origin only
            Assert.Null(e.Frames);
        }

        [Fact]
        public void Factory_FromEngine_MapsDeepBacktrace_FramesAndStackTrace()
        {
            // Issue #163: a Godot 4.5+ GDScript error carries the already-materialized deep backtrace. The
            // factory must copy both the structured frames and the formatted string onto the RuntimeError.
            var frames = new List<RuntimeErrorFrame>
            {
                new RuntimeErrorFrame("_take_damage", "res://scripts/player.gd", 42),
                new RuntimeErrorFrame("_process", "res://scripts/player.gd", 18),
            };
            var record = new EngineErrorRecord(
                EngineErrorKind.Script, "res://scripts/player.gd", 42, "_take_damage", "Invalid get index",
                frames: frames,
                stackTrace: "GDScript backtrace:\nat _take_damage res://scripts/player.gd:42");

            var e = RuntimeErrorFactory.FromEngine(record);

            Assert.Equal(RuntimeErrorSource.Engine, e.Source);
            Assert.NotNull(e.Frames);
            Assert.Equal(2, e.Frames!.Count);
            Assert.Equal("_take_damage", e.Frames[0].Function);    // innermost-first preserved
            Assert.Equal(42, e.Frames[0].Line);
            Assert.Equal("_process", e.Frames[1].Function);
            Assert.NotNull(e.StackTrace);
            Assert.Contains("GDScript backtrace", e.StackTrace!);
        }

        [Fact]
        public void Factory_FromEngine_CopiesFrames_DoesNotAliasRecordList()
        {
            // The RuntimeError must own a fresh List<>, not share the record's reference, so later mutation
            // of the source list cannot corrupt a captured row.
            var source = new List<RuntimeErrorFrame> { new RuntimeErrorFrame("a", "res://a.gd", 1) };
            var record = new EngineErrorRecord(EngineErrorKind.Script, "res://a.gd", 1, "a", "msg",
                frames: source, stackTrace: "trace");

            var e = RuntimeErrorFactory.FromEngine(record);
            source.Add(new RuntimeErrorFrame("b", "res://b.gd", 2)); // mutate the source after capture

            Assert.NotNull(e.Frames);
            Assert.Single(e.Frames!);                  // unaffected by the post-capture mutation
            Assert.Equal("a", e.Frames![0].Function);
        }

        [Fact]
        public void EngineErrorRecord_NormalizesEmptyFrames_ToNull()
        {
            // The record collapses an empty backtrace to null so consumers have a single "no frames" sentinel.
            var rec = new EngineErrorRecord(EngineErrorKind.Script, "res://a.gd", 1, "a", "msg",
                frames: new List<RuntimeErrorFrame>(), stackTrace: "");
            Assert.Null(rec.Frames);
            Assert.Null(rec.StackTrace);
        }

        [Fact]
        public void Factory_FromException_MapsTypeMessageAndFullStackTrace()
        {
            Exception caught;
            try { throw new InvalidOperationException("kaboom"); }
            catch (Exception ex) { caught = ex; }

            var e = RuntimeErrorFactory.FromException(RuntimeErrorSource.UnhandledException, caught);

            Assert.Equal(RuntimeErrorSource.UnhandledException, e.Source);
            Assert.Equal("System.InvalidOperationException", e.Type);
            Assert.Equal("kaboom", e.Message);
            Assert.NotNull(e.StackTrace);
            Assert.Contains("InvalidOperationException", e.StackTrace);
            Assert.Contains("kaboom", e.StackTrace);
        }

        [Fact]
        public void Factory_FromException_FlattensInnerException_InStackTrace()
        {
            Exception caught;
            try
            {
                try { throw new ArgumentNullException("inner-arg"); }
                catch (Exception inner) { throw new InvalidOperationException("outer", inner); }
            }
            catch (Exception ex) { caught = ex; }

            var e = RuntimeErrorFactory.FromException(RuntimeErrorSource.UnobservedTaskException, caught);

            Assert.Equal(RuntimeErrorSource.UnobservedTaskException, e.Source);
            Assert.Equal("System.InvalidOperationException", e.Type); // OUTER type/message stay discrete
            Assert.Equal("outer", e.Message);
            Assert.NotNull(e.StackTrace);
            Assert.Contains("ArgumentNullException", e.StackTrace!); // inner is inlined by Exception.ToString()
        }

        [Fact]
        public void Factory_FromException_NullIsSafe_YieldsPlaceholderRow()
        {
            var e = RuntimeErrorFactory.FromException(RuntimeErrorSource.UnhandledException, null);
            Assert.Equal(RuntimeErrorSource.UnhandledException, e.Source);
            Assert.Contains("null", e.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ---- ScriptErrorCapture.ErrorSink: structured forwarding ---------------------------------

        [Fact]
        public void Route_ForwardsStructuredRecordToErrorSink_WithFunction()
        {
            var records = new List<EngineErrorRecord>();
            var capture = new ScriptErrorCapture { ErrorSink = r => records.Add(r) };

            capture.Route(EngineErrorKind.Script, "res://p.gd", 5, "cond", "Parse error", function: "_process");

            var rec = Assert.Single(records);
            Assert.Equal(EngineErrorKind.Script, rec.Kind);
            Assert.Equal("res://p.gd", rec.FilePath);
            Assert.Equal(5, rec.Line);
            Assert.Equal("_process", rec.Function);
            Assert.Equal("Parse error", rec.Message); // rationale preferred
        }

        [Fact]
        public void Route_ForwardsDeepBacktrace_ToErrorSink()
        {
            // Issue #163: the deep backtrace materialized by the 4.5 logger flows through Route() untouched
            // into the structured EngineErrorRecord (Route never reads a live Godot object — only the managed
            // primitives the logger already copied out).
            var records = new List<EngineErrorRecord>();
            var capture = new ScriptErrorCapture { ErrorSink = r => records.Add(r) };

            var frames = new List<RuntimeErrorFrame>
            {
                new RuntimeErrorFrame("_inner", "res://p.gd", 9),
                new RuntimeErrorFrame("_outer", "res://p.gd", 3),
            };
            capture.Route(EngineErrorKind.Script, "res://p.gd", 9, "cond", "Boom",
                function: "_inner", frames: frames, stackTrace: "GDScript backtrace:\n...");

            var rec = Assert.Single(records);
            Assert.NotNull(rec.Frames);
            Assert.Equal(2, rec.Frames!.Count);
            Assert.Equal("_inner", rec.Frames[0].Function);
            Assert.Equal("_outer", rec.Frames[1].Function);
            Assert.Equal("GDScript backtrace:\n...", rec.StackTrace);
        }

        [Fact]
        public void Route_NoBacktrace_LeavesRecordFramesNull()
        {
            // The editor passive-log path (and < 4.5) calls Route without frames — the record stays origin-only.
            var records = new List<EngineErrorRecord>();
            var capture = new ScriptErrorCapture { ErrorSink = r => records.Add(r) };

            capture.Route(EngineErrorKind.Error, "res://a.gd", 1, "c", "generic");

            var rec = Assert.Single(records);
            Assert.Null(rec.Frames);
            Assert.Null(rec.StackTrace);
        }

        [Fact]
        public void Route_To_FromEngine_To_Collector_PreservesDeepBacktrace_EndToEnd()
        {
            // Full in-game wiring shape: Route → ErrorSink(FromEngine) → collector, with the deep backtrace
            // surviving every hop. Mirrors how RuntimeErrorCapture wires the ErrorSink in the running game.
            var collector = new RuntimeErrorCollector();
            var capture = new ScriptErrorCapture
            {
                ErrorSink = record => collector.Append(RuntimeErrorFactory.FromEngine(record)),
            };

            var frames = new List<RuntimeErrorFrame>
            {
                new RuntimeErrorFrame("_take_damage", "res://player.gd", 42),
                new RuntimeErrorFrame("_process", "res://player.gd", 18),
            };
            capture.Route(EngineErrorKind.Script, "res://player.gd", 42, "cond", "Invalid get index",
                function: "_take_damage", frames: frames, stackTrace: "GDScript backtrace:\n...");

            var page = collector.QuerySince(0, 100, out _);
            var captured = Assert.Single(page);
            Assert.Equal(RuntimeErrorSource.Engine, captured.Source);
            Assert.Equal("_take_damage", captured.Function);
            Assert.NotNull(captured.Frames);
            Assert.Equal(2, captured.Frames!.Count);
            Assert.Equal("_take_damage", captured.Frames[0].Function);
            Assert.Equal(42, captured.Frames[0].Line);
            Assert.NotNull(captured.StackTrace);
        }

        [Fact]
        public void Route_ErrorSink_FiresIndependentlyOfValidationSession()
        {
            var records = new List<EngineErrorRecord>();
            var capture = new ScriptErrorCapture { ErrorSink = r => records.Add(r) };

            // No BeginSession — the validation path is silent, but the structured ErrorSink still fires (the
            // runtime captures every engine error, not just script errors during a validation window).
            capture.Route(EngineErrorKind.Error, "res://a.gd", 1, "c", "generic");
            capture.Route(EngineErrorKind.Warning, "res://b.gd", 2, "c", "warn");

            Assert.Equal(2, records.Count);
            Assert.Empty(capture.EndSession()); // confirms no validation session was implicitly opened
        }

        [Fact]
        public void Route_BothSinks_CanCoexist()
        {
            var logs = new List<(GodotLogType, string)>();
            var records = new List<EngineErrorRecord>();
            var capture = new ScriptErrorCapture
            {
                LogSink = (t, m) => logs.Add((t, m)),
                ErrorSink = r => records.Add(r),
            };

            capture.Route(EngineErrorKind.Script, "res://p.gd", 5, "c", "boom");

            Assert.Single(logs);
            Assert.Single(records);
        }

        // ---- Tool_RuntimeErrors.Get / Clear ------------------------------------------------------

        [Fact]
        public void Tool_Get_ReportsUnavailable_WhenNoCollectorInstalled()
        {
            WithNoCollector(() =>
            {
                var result = new Tool_RuntimeErrors().Get();
                Assert.False(result.Available);
                Assert.True(result.Ok); // ok defaults true so an unavailable result is not read as a failure
                Assert.Empty(result.Errors);
                Assert.Contains("not enabled", result.Note, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void Tool_Get_ReturnsErrors_AndComputesCounts_WhenAvailable()
        {
            WithCollector(c =>
            {
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, "warn", nameof(EngineErrorKind.Warning)));
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, "err", nameof(EngineErrorKind.Error)));
                c.Append(new RuntimeError(RuntimeErrorSource.UnhandledException, "boom", "System.Exception"));

                var result = new Tool_RuntimeErrors().Get();

                Assert.True(result.Available);
                Assert.False(result.Ok);                 // has error-severity rows
                Assert.Equal(3, result.Count);
                Assert.Equal(2, result.ErrorCount);      // engine Error + managed fault
                Assert.Equal(1, result.WarningCount);    // engine Warning only
                Assert.Equal(3, result.HighestSequence);
            });
        }

        [Fact]
        public void Tool_Get_OkTrue_WhenOnlyWarnings()
        {
            WithCollector(c =>
            {
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, "w1", nameof(EngineErrorKind.Warning)));
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, "w2", nameof(EngineErrorKind.Warning)));

                var result = new Tool_RuntimeErrors().Get();
                Assert.True(result.Available);
                Assert.True(result.Ok);                  // warnings do not flip ok
                Assert.Equal(0, result.ErrorCount);
                Assert.Equal(2, result.WarningCount);
            });
        }

        [Fact]
        public void Tool_Get_SinceSequence_ReturnsOnlyNewer()
        {
            WithCollector(c =>
            {
                for (int i = 0; i < 4; i++)
                    c.Append(new RuntimeError(RuntimeErrorSource.Engine, $"e{i}", nameof(EngineErrorKind.Error)));

                var result = new Tool_RuntimeErrors().Get(sinceSequence: 2);
                Assert.Equal(2, result.Count);          // sequences 3,4
                Assert.All(result.Errors, e => Assert.True(e.Sequence > 2));
                Assert.Equal(4, result.HighestSequence); // still reflects ALL captured, for the next poll
            });
        }

        [Fact]
        public void Tool_Get_MaxEntries_TruncatesKeepingNewest()
        {
            WithCollector(c =>
            {
                for (int i = 0; i < 6; i++)
                    c.Append(new RuntimeError(RuntimeErrorSource.Engine, $"e{i}", nameof(EngineErrorKind.Error)));

                var result = new Tool_RuntimeErrors().Get(maxEntries: 2);
                Assert.Equal(2, result.Count);
                Assert.True(result.Truncated);
                Assert.Equal(new long[] { 5, 6 }, result.Errors.Select(e => e.Sequence));
            });
        }

        [Fact]
        public void Tool_Get_RejectsNonPositiveMaxEntries()
        {
            WithCollector(_ =>
                Assert.Throws<ArgumentException>(() => new Tool_RuntimeErrors().Get(maxEntries: 0)));
        }

        [Fact]
        public void Tool_Clear_EmptiesBuffer_WhenAvailable()
        {
            WithCollector(c =>
            {
                c.Append(new RuntimeError(RuntimeErrorSource.Engine, "e", nameof(EngineErrorKind.Error)));
                Assert.Equal(1, c.Count);

                new Tool_RuntimeErrors().Clear();
                Assert.Equal(0, c.Count);

                // available stays true after clear (capture still wired); the get just sees an empty buffer.
                var result = new Tool_RuntimeErrors().Get();
                Assert.True(result.Available);
                Assert.True(result.Ok);
                Assert.Empty(result.Errors);
            });
        }

        [Fact]
        public void Tool_Clear_IsNoOp_WhenUnavailable()
        {
            WithNoCollector(() =>
            {
                var ex = Record.Exception(() => new Tool_RuntimeErrors().Clear());
                Assert.Null(ex); // must not throw when capture was never enabled
            });
        }

        // ---- Builder opt-in ----------------------------------------------------------------------

        [Fact]
        public void Builder_WithRuntimeErrorCapture_SetsFlag_AndRegistersTool()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithRuntimeErrorCapture());

            // The flag is internal — reach it reflectively (same assembly).
            var flag = (bool)typeof(GodotMcpRuntimeBuilder)
                .GetProperty("CaptureRuntimeErrors",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(builder)!;
            Assert.True(flag);

            // The runtime-errors tool is auto-registered so the captured errors are reachable.
            Assert.False(builder.HasNoTools);
            Assert.Contains(typeof(Tool_RuntimeErrors), builder.ToolTypes);
        }

        [Fact]
        public void Builder_WithoutOptIn_DoesNotEnableCapture_OrRegisterTool()
        {
            var builder = GodotMcpRuntime.Initialize();

            var flag = (bool)typeof(GodotMcpRuntimeBuilder)
                .GetProperty("CaptureRuntimeErrors",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(builder)!;
            Assert.False(flag);                              // default OFF
            Assert.True(builder.HasNoTools);
            Assert.DoesNotContain(typeof(Tool_RuntimeErrors), builder.ToolTypes);
        }

        [Fact]
        public void Builder_WithRuntimeErrorCapture_IsIdempotent_RegistersToolOnce()
        {
            var builder = GodotMcpRuntime.Initialize(b => b
                .WithRuntimeErrorCapture()
                .WithRuntimeErrorCapture()
                .WithTools(typeof(Tool_RuntimeErrors))); // explicit dup too

            Assert.Single(builder.ToolTypes, t => t == typeof(Tool_RuntimeErrors));
        }

        // ---- helpers: install/teardown the process-wide collector around a test --------------------

        static void WithCollector(Action<RuntimeErrorCollector> body)
        {
            var prior = RuntimeErrorCollector.Current;
            var c = new RuntimeErrorCollector();
            RuntimeErrorCollector.Current = c;
            try { body(c); }
            finally { RuntimeErrorCollector.Current = prior; }
        }

        static void WithNoCollector(Action body)
        {
            var prior = RuntimeErrorCollector.Current;
            RuntimeErrorCollector.Current = null;
            try { body(); }
            finally { RuntimeErrorCollector.Current = prior; }
        }
    }
}
